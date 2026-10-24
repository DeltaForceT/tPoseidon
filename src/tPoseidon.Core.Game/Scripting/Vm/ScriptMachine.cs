using tPoseidon.Core.Game.Scripting.Compile;
using tPoseidon.Core.Game.Scripting.Host;
using tPoseidon.Core.Game.Scripting.Lang;

namespace tPoseidon.Core.Game.Scripting.Vm;

public enum RunState
{
    AwaitingNext,
    AwaitingMenu,
    Closed,
    Ended,
    Fallback,
}

public enum PageKind
{
    Next,
    Menu,
    Close,
}

public sealed record ScriptPage(PageKind Kind, IReadOnlyList<string> Lines, IReadOnlyList<string> Options);

public sealed record RunResult(RunState State, ScriptPage? Page)
{
    public bool IsFinished => State is RunState.Closed or RunState.Ended or RunState.Fallback;
}

public sealed class ScriptMachine
{
    public const int DefaultInstructionBudget = 20_000;
    public const int MaximumCallDepth = 24;
    public const int MaximumStackDepth = 512;
    public const int MaximumPageLines = 40;

    private readonly Func<string, Routine?> _functions;
    private readonly int _budget;
    private readonly List<Frame> _frames = new();
    private readonly List<ScriptValue> _stack = new();
    private readonly List<string> _lines = new();
    private int[] _menuMap = Array.Empty<int>();
    private RunState? _waiting;
    private bool _started;
    private bool _finished;

    public ScriptMachine(Routine entry, Func<string, Routine?> functions, int instructionBudget = DefaultInstructionBudget)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(functions);
        ArgumentOutOfRangeException.ThrowIfLessThan(instructionBudget, 1);
        _functions = functions;
        _budget = instructionBudget;
        _frames.Add(new Frame(entry));
    }

    public bool IsFinished => _finished;

    public RunResult Start(IScriptHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (_started) throw new InvalidOperationException("The script was already started");
        _started = true;
        return Execute(host);
    }

    public RunResult Continue(IScriptHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (_waiting != RunState.AwaitingNext) throw new InvalidOperationException("The script is not waiting for a click on Next");
        _waiting = null;
        return Execute(host);
    }

    public RunResult Choose(IScriptHost host, int choice)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (_waiting != RunState.AwaitingMenu) throw new InvalidOperationException("The script is not waiting for a menu choice");
        if (choice < 1 || choice > _menuMap.Length) throw new ScriptRuntimeException($"Menu choice {choice} is not one of the {_menuMap.Length} options that were shown");
        _waiting = null;
        Push(ScriptValue.FromNumber(_menuMap[choice - 1]));
        return Execute(host);
    }

    private sealed class Frame
    {
        public Frame(Routine routine)
        {
            Routine = routine;
            Locals = new ScriptValue[routine.LocalCount];
        }

        public Routine Routine { get; }

        public ScriptValue[] Locals { get; }

        public int Counter { get; set; }
    }

    private void Push(ScriptValue value)
    {
        if (_stack.Count >= MaximumStackDepth) throw new ScriptRuntimeException("The script used too much stack");
        _stack.Add(value);
    }

    private ScriptValue Pop()
    {
        var value = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        return value;
    }

    private RunResult Execute(IScriptHost host)
    {
        var budget = _budget;
        while (_frames.Count > 0)
        {
            var frame = _frames[^1];
            var code = frame.Routine.Code;
            if (frame.Counter >= code.Count) throw new ScriptRuntimeException($"{frame.Routine.File}: the script {frame.Routine.Name} ran past its end");
            var instruction = code[frame.Counter++];
            if (--budget < 0)
            {
                throw Fault(frame, instruction, $"The script used more than {_budget} steps in one go; it probably loops forever");
            }
            try
            {
                var result = Step(host, frame, instruction);
                if (result is not null) return result;
            }
            catch (ScriptRuntimeException exception)
            {
                throw Fault(frame, instruction, exception.Message);
            }
            catch (OverflowException)
            {
                throw Fault(frame, instruction, "A number got too large");
            }
        }
        return Finish(RunState.Ended);
    }

    private static ScriptRuntimeException Fault(Frame frame, Instruction instruction, string message) =>
        new($"{frame.Routine.File}:{instruction.Line}: {message}");

    private RunResult? Step(IScriptHost host, Frame frame, Instruction instruction)
    {
        var routine = frame.Routine;
        switch (instruction.Op)
        {
            case OpCode.Push:
                Push(routine.Constants[instruction.A]);
                break;
            case OpCode.LoadLocal:
                Push(frame.Locals[instruction.A]);
                break;
            case OpCode.StoreLocal:
                frame.Locals[instruction.A] = Pop();
                break;
            case OpCode.LoadVariable:
                Push(host.GetVariable((VariableScope)instruction.B, routine.Names[instruction.A]));
                break;
            case OpCode.StoreVariable:
                host.SetVariable((VariableScope)instruction.B, routine.Names[instruction.A], Pop());
                break;
            case OpCode.Binary:
                {
                    var right = Pop();
                    var left = Pop();
                    Push(Evaluate((BinaryOperator)instruction.A, left, right));
                    break;
                }
            case OpCode.Not:
                Push(ScriptValue.FromBool(!Pop().IsTrue));
                break;
            case OpCode.Negate:
                Push(ScriptValue.FromNumber(checked(-Pop().Number)));
                break;
            case OpCode.Jump:
                frame.Counter = instruction.A;
                break;
            case OpCode.JumpIfFalse:
                if (!Pop().IsTrue) frame.Counter = instruction.A;
                break;
            case OpCode.JumpIfFalseKeep:
                if (!_stack[^1].IsTrue) frame.Counter = instruction.A;
                break;
            case OpCode.JumpIfTrueKeep:
                if (_stack[^1].IsTrue) frame.Counter = instruction.A;
                break;
            case OpCode.Pop:
                Pop();
                break;
            case OpCode.CallBuiltin:
                {
                    var arguments = new ScriptValue[instruction.B];
                    for (var i = instruction.B - 1; i >= 0; i--) arguments[i] = Pop();
                    Push(Builtins.ById(instruction.A).Invoke(host, arguments));
                    break;
                }
            case OpCode.CallFunction:
                CallFunction(routine.Names[instruction.A], instruction.B);
                break;
            case OpCode.Return:
                {
                    var value = Pop();
                    _frames.RemoveAt(_frames.Count - 1);
                    if (_frames.Count == 0) return Finish(PendingLinesState());
                    Push(value);
                    break;
                }
            case OpCode.Mes:
                AppendLines(Pop().Text);
                break;
            case OpCode.Next:
                return Wait(RunState.AwaitingNext, PageKind.Next, Array.Empty<string>());
            case OpCode.Close:
                return Finish(RunState.Closed);
            case OpCode.End:
                return Finish(PendingLinesState());
            case OpCode.Fallback:
                _lines.Clear();
                return Finish(RunState.Fallback);
            case OpCode.Menu:
                return OpenMenu(instruction.A);
            default:
                throw new ScriptRuntimeException($"Unknown instruction {instruction.Op}");
        }
        return null;
    }

    private RunState PendingLinesState() => _lines.Count > 0 ? RunState.Closed : RunState.Ended;

    private void AppendLines(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            if (_lines.Count >= MaximumPageLines) throw new ScriptRuntimeException($"A page holds at most {MaximumPageLines} lines; use next; to start a new page");
            _lines.Add(line);
        }
    }

    private void CallFunction(string name, int argumentCount)
    {
        var target = _functions(name) ?? throw new ScriptRuntimeException($"There is no function named '{name}'");
        if (target.ParameterCount != argumentCount)
        {
            throw new ScriptRuntimeException($"{name}() takes {target.ParameterCount} argument(s) but was given {argumentCount}");
        }
        if (_frames.Count >= MaximumCallDepth) throw new ScriptRuntimeException($"Functions are nested more than {MaximumCallDepth} deep");
        var frame = new Frame(target);
        for (var i = argumentCount - 1; i >= 0; i--) frame.Locals[i] = Pop();
        _frames.Add(frame);
    }

    private RunResult OpenMenu(int optionCount)
    {
        var labels = new string[optionCount];
        var visible = new bool[optionCount];
        for (var i = optionCount - 1; i >= 0; i--)
        {
            visible[i] = Pop().IsTrue;
            labels[i] = Pop().Text;
        }
        var options = new List<string>(optionCount);
        var map = new List<int>(optionCount);
        for (var i = 0; i < optionCount; i++)
        {
            if (!visible[i]) continue;
            options.Add(labels[i]);
            map.Add(i + 1);
        }
        if (options.Count == 0) throw new ScriptRuntimeException("Every option of the menu is hidden");
        _menuMap = map.ToArray();
        return Wait(RunState.AwaitingMenu, PageKind.Menu, options);
    }

    private RunResult Wait(RunState state, PageKind kind, IReadOnlyList<string> options)
    {
        _waiting = state;
        var page = new ScriptPage(kind, _lines.ToArray(), options);
        _lines.Clear();
        return new RunResult(state, page);
    }

    private RunResult Finish(RunState state)
    {
        _finished = true;
        _frames.Clear();
        ScriptPage? page = null;
        if (state == RunState.Closed)
        {
            page = new ScriptPage(PageKind.Close, _lines.ToArray(), Array.Empty<string>());
        }
        _lines.Clear();
        return new RunResult(state, page);
    }

    private static ScriptValue Evaluate(BinaryOperator op, ScriptValue left, ScriptValue right)
    {
        switch (op)
        {
            case BinaryOperator.Add:
                return left.IsText || right.IsText
                    ? ScriptValue.FromText(left.Text + right.Text)
                    : ScriptValue.FromNumber(checked(left.Number + right.Number));
            case BinaryOperator.Subtract:
                return ScriptValue.FromNumber(checked(Number(left) - Number(right)));
            case BinaryOperator.Multiply:
                return ScriptValue.FromNumber(checked(Number(left) * Number(right)));
            case BinaryOperator.Divide:
                if (Number(right) == 0) throw new ScriptRuntimeException("Division by zero");
                return ScriptValue.FromNumber(Number(left) / Number(right));
            case BinaryOperator.Remainder:
                if (Number(right) == 0) throw new ScriptRuntimeException("Division by zero");
                return ScriptValue.FromNumber(Number(left) % Number(right));
            case BinaryOperator.Equal:
                return ScriptValue.FromBool(left.Equals(right));
            case BinaryOperator.NotEqual:
                return ScriptValue.FromBool(!left.Equals(right));
            default:
                var order = left.IsText || right.IsText
                    ? string.CompareOrdinal(left.Text, right.Text)
                    : left.Number.CompareTo(right.Number);
                return ScriptValue.FromBool(op switch
                {
                    BinaryOperator.Less => order < 0,
                    BinaryOperator.LessOrEqual => order <= 0,
                    BinaryOperator.Greater => order > 0,
                    _ => order >= 0,
                });
        }
    }

    private static long Number(ScriptValue value)
    {
        if (value.IsText) throw new ScriptRuntimeException("Arithmetic needs numbers but got the text \"" + value.Text + "\"");
        return value.Number;
    }
}
