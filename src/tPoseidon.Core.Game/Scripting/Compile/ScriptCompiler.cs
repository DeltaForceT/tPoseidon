using tPoseidon.Core.Game.Scripting.Host;
using tPoseidon.Core.Game.Scripting.Lang;

namespace tPoseidon.Core.Game.Scripting.Compile;

public sealed record FunctionReference(string Name, int ArgumentCount, int Line, int Column);

public sealed record CompiledNpc(NpcDeclaration Declaration, Routine Talk);

public sealed record CompiledOverride(OverrideDeclaration Declaration, Routine Talk);

public sealed record CompiledEvent(EventDeclaration Declaration, Routine Body);

public sealed record CompiledTimer(TimerDeclaration Declaration, Routine Body);

public sealed record CompiledCommand(CommandDeclaration Declaration, Routine Body);

public sealed record CompiledFile(
    string File,
    IReadOnlyList<CompiledNpc> Npcs,
    IReadOnlyList<CompiledOverride> Overrides,
    IReadOnlyList<CompiledEvent> Events,
    IReadOnlyList<CompiledTimer> Timers,
    IReadOnlyList<CompiledCommand> Commands,
    IReadOnlyList<Routine> Functions,
    IReadOnlyList<(FunctionReference Reference, string File)> References);

public static class ScriptCompiler
{
    public static CompiledFile Compile(string file, string source)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(source);
        var ast = Parser.Parse(source);
        return Compile(file, ast);
    }

    public static CompiledFile Compile(string file, ScriptFileAst ast)
    {
        var references = new List<FunctionReference>();
        var npcs = new List<CompiledNpc>();
        var overrides = new List<CompiledOverride>();
        var events = new List<CompiledEvent>();
        var timers = new List<CompiledTimer>();
        var commands = new List<CompiledCommand>();
        var functions = new List<Routine>();
        foreach (var declaration in ast.Declarations)
        {
            switch (declaration)
            {
                case NpcDeclaration npc:
                    npcs.Add(new CompiledNpc(npc, Build(file, $"npc {npc.Name}", Array.Empty<string>(), npc.Body, references)));
                    break;
                case OverrideDeclaration over:
                    overrides.Add(new CompiledOverride(over, Build(file, $"override {over.Sprite}", Array.Empty<string>(), over.Body, references)));
                    break;
                case EventDeclaration ev:
                    events.Add(new CompiledEvent(ev, Build(file, $"on {ev.Kind}", Array.Empty<string>(), ev.Body, references)));
                    break;
                case TimerDeclaration timer:
                    timers.Add(new CompiledTimer(timer, Build(file, $"timer {timer.Spec}", Array.Empty<string>(), timer.Body, references)));
                    break;
                case CommandDeclaration command:
                    commands.Add(new CompiledCommand(command, Build(file, $"command {command.Name}", Array.Empty<string>(), command.Body, references)));
                    break;
                case FunctionDeclaration function:
                    functions.Add(Build(file, function.Name, function.Parameters, function.Body, references));
                    break;
                default:
                    throw new ScriptSyntaxException($"Unknown declaration {declaration.GetType().Name}", declaration.Line, declaration.Column);
            }
        }
        return new CompiledFile(file, npcs, overrides, events, timers, commands, functions, references.Select(reference => (reference, file)).ToList());
    }

    private static Routine Build(string file, string name, IReadOnlyList<string> parameters, IReadOnlyList<Statement> body, List<FunctionReference> references)
    {
        var emitter = new Emitter(file, name, references);
        return emitter.Build(parameters, body);
    }

    private sealed class LoopContext
    {
        public List<int> Breaks { get; } = new();

        public List<int> Continues { get; } = new();
    }

    private sealed class Emitter
    {
        private readonly string _file;
        private readonly string _name;
        private readonly List<FunctionReference> _references;
        private readonly List<Instruction> _code = new();
        private readonly List<ScriptValue> _constants = new();
        private readonly Dictionary<string, int> _numberConstants = new(StringComparer.Ordinal);
        private readonly List<string> _names = new();
        private readonly Dictionary<string, int> _nameIndexes = new(StringComparer.Ordinal);
        private readonly List<Dictionary<string, int>> _scopes = new();
        private readonly Stack<LoopContext> _loops = new();
        private int _localCount;

        public Emitter(string file, string name, List<FunctionReference> references)
        {
            _file = file;
            _name = name;
            _references = references;
        }

        public Routine Build(IReadOnlyList<string> parameters, IReadOnlyList<Statement> body)
        {
            _scopes.Add(new Dictionary<string, int>(StringComparer.Ordinal));
            foreach (var parameter in parameters) Declare(parameter, 0, 0);
            foreach (var statement in body) EmitStatement(statement);
            var line = body.Count > 0 ? body[^1].Line : 0;
            Emit(OpCode.Push, ConstantIndex(ScriptValue.Zero), 0, line);
            Emit(OpCode.Return, 0, 0, line);
            return new Routine(_name, _file, parameters.Count, _localCount, _code.ToArray(), _constants.ToArray(), _names.ToArray());
        }

        private int Emit(OpCode op, int a, int b, int line)
        {
            _code.Add(new Instruction(op, a, b, line));
            return _code.Count - 1;
        }

        private void Patch(int at, int target) => _code[at] = _code[at] with { A = target };

        private int ConstantIndex(ScriptValue value)
        {
            var key = value.IsText ? "t:" + value.Text : "n:" + value.Number;
            if (_numberConstants.TryGetValue(key, out var existing)) return existing;
            _constants.Add(value);
            _numberConstants[key] = _constants.Count - 1;
            return _constants.Count - 1;
        }

        private int NameIndex(string name)
        {
            if (_nameIndexes.TryGetValue(name, out var existing)) return existing;
            _names.Add(name);
            _nameIndexes[name] = _names.Count - 1;
            return _names.Count - 1;
        }

        private int Declare(string name, int line, int column)
        {
            if (_scopes[^1].ContainsKey(name))
            {
                throw new ScriptSyntaxException($"The variable '{name}' is already declared in this block", line, column);
            }
            var slot = _localCount++;
            _scopes[^1][name] = slot;
            return slot;
        }

        private int? Lookup(string name)
        {
            for (var i = _scopes.Count - 1; i >= 0; i--)
            {
                if (_scopes[i].TryGetValue(name, out var slot)) return slot;
            }
            return null;
        }

        private int NewTemporary() => _localCount++;

        private void EmitBlock(IReadOnlyList<Statement> statements)
        {
            _scopes.Add(new Dictionary<string, int>(StringComparer.Ordinal));
            foreach (var statement in statements) EmitStatement(statement);
            _scopes.RemoveAt(_scopes.Count - 1);
        }

        private void EmitStatement(Statement statement)
        {
            switch (statement)
            {
                case LetStatement let:
                    EmitExpression(let.Value);
                    Emit(OpCode.StoreLocal, Declare(let.Name, let.Line, let.Column), 0, let.Line);
                    break;
                case AssignStatement assign:
                    EmitAssign(assign);
                    break;
                case ExpressionStatement expression:
                    EmitExpression(expression.Value);
                    Emit(OpCode.Pop, 0, 0, expression.Line);
                    break;
                case IfStatement ifStatement:
                    EmitIf(ifStatement);
                    break;
                case WhileStatement whileStatement:
                    EmitWhile(whileStatement);
                    break;
                case BreakStatement breakStatement:
                    if (_loops.Count == 0) throw new ScriptSyntaxException("'break' is only valid inside a while loop", breakStatement.Line, breakStatement.Column);
                    _loops.Peek().Breaks.Add(Emit(OpCode.Jump, 0, 0, breakStatement.Line));
                    break;
                case ContinueStatement continueStatement:
                    if (_loops.Count == 0) throw new ScriptSyntaxException("'continue' is only valid inside a while loop", continueStatement.Line, continueStatement.Column);
                    _loops.Peek().Continues.Add(Emit(OpCode.Jump, 0, 0, continueStatement.Line));
                    break;
                case ReturnStatement ret:
                    if (ret.Value is null) Emit(OpCode.Push, ConstantIndex(ScriptValue.Zero), 0, ret.Line);
                    else EmitExpression(ret.Value);
                    Emit(OpCode.Return, 0, 0, ret.Line);
                    break;
                case MesStatement mes:
                    EmitExpression(mes.Text);
                    Emit(OpCode.Mes, 0, 0, mes.Line);
                    break;
                case NextStatement next:
                    Emit(OpCode.Next, 0, 0, next.Line);
                    break;
                case CloseStatement close:
                    Emit(OpCode.Close, 0, 0, close.Line);
                    break;
                case EndStatement end:
                    Emit(OpCode.End, 0, 0, end.Line);
                    break;
                case FallbackStatement fallback:
                    Emit(OpCode.Fallback, 0, 0, fallback.Line);
                    break;
                case MenuStatement menu:
                    EmitMenu(menu);
                    break;
                default:
                    throw new ScriptSyntaxException($"Unknown statement {statement.GetType().Name}", statement.Line, statement.Column);
            }
        }

        private void EmitAssign(AssignStatement assign)
        {
            if (assign.Operator != "=")
            {
                EmitExpression(assign.Target);
                EmitExpression(assign.Value);
                Emit(OpCode.Binary, (int)BinaryFor(assign.Operator[..1], assign.Line, assign.Column), 0, assign.Line);
            }
            else
            {
                EmitExpression(assign.Value);
            }
            switch (assign.Target)
            {
                case LocalReference local:
                    if (Lookup(local.Name) is not int slot) throw new ScriptSyntaxException($"The variable '{local.Name}' is not declared; use let first", local.Line, local.Column);
                    Emit(OpCode.StoreLocal, slot, 0, assign.Line);
                    break;
                case ScopedReference scoped:
                    Emit(OpCode.StoreVariable, NameIndex(scoped.Name), (int)scoped.Scope, assign.Line);
                    break;
                default:
                    throw new ScriptSyntaxException("Only a variable can be assigned to", assign.Line, assign.Column);
            }
        }

        private void EmitIf(IfStatement statement)
        {
            EmitExpression(statement.Condition);
            var skipThen = Emit(OpCode.JumpIfFalse, 0, 0, statement.Line);
            EmitBlock(statement.Then);
            if (statement.Else is null)
            {
                Patch(skipThen, _code.Count);
                return;
            }
            var skipElse = Emit(OpCode.Jump, 0, 0, statement.Line);
            Patch(skipThen, _code.Count);
            EmitBlock(statement.Else);
            Patch(skipElse, _code.Count);
        }

        private void EmitWhile(WhileStatement statement)
        {
            var top = _code.Count;
            EmitExpression(statement.Condition);
            var exit = Emit(OpCode.JumpIfFalse, 0, 0, statement.Line);
            var loop = new LoopContext();
            _loops.Push(loop);
            EmitBlock(statement.Body);
            _loops.Pop();
            Emit(OpCode.Jump, top, 0, statement.Line);
            var end = _code.Count;
            Patch(exit, end);
            foreach (var at in loop.Breaks) Patch(at, end);
            foreach (var at in loop.Continues) Patch(at, top);
        }

        private void EmitMenu(MenuStatement menu)
        {
            foreach (var option in menu.Options)
            {
                EmitExpression(option.Label);
                if (option.Condition is null) Emit(OpCode.Push, ConstantIndex(ScriptValue.FromNumber(1)), 0, option.Line);
                else EmitExpression(option.Condition);
            }
            Emit(OpCode.Menu, menu.Options.Count, 0, menu.Line);
            var choice = NewTemporary();
            Emit(OpCode.StoreLocal, choice, 0, menu.Line);
            var endJumps = new List<int>();
            for (var i = 0; i < menu.Options.Count; i++)
            {
                Emit(OpCode.LoadLocal, choice, 0, menu.Line);
                Emit(OpCode.Push, ConstantIndex(ScriptValue.FromNumber(i + 1)), 0, menu.Line);
                Emit(OpCode.Binary, (int)BinaryOperator.Equal, 0, menu.Line);
                var skip = Emit(OpCode.JumpIfFalse, 0, 0, menu.Line);
                EmitBlock(menu.Options[i].Body);
                endJumps.Add(Emit(OpCode.Jump, 0, 0, menu.Line));
                Patch(skip, _code.Count);
            }
            foreach (var at in endJumps) Patch(at, _code.Count);
        }

        private void EmitExpression(Expression expression)
        {
            switch (expression)
            {
                case NumberLiteral number:
                    Emit(OpCode.Push, ConstantIndex(ScriptValue.FromNumber(number.Value)), 0, number.Line);
                    break;
                case TextLiteral text:
                    Emit(OpCode.Push, ConstantIndex(ScriptValue.FromText(text.Value)), 0, text.Line);
                    break;
                case LocalReference local:
                    if (Lookup(local.Name) is not int slot) throw new ScriptSyntaxException($"The variable '{local.Name}' is not declared; use let first", local.Line, local.Column);
                    Emit(OpCode.LoadLocal, slot, 0, local.Line);
                    break;
                case ScopedReference scoped:
                    Emit(OpCode.LoadVariable, NameIndex(scoped.Name), (int)scoped.Scope, scoped.Line);
                    break;
                case UnaryExpression unary:
                    EmitExpression(unary.Operand);
                    Emit(unary.Operator == "!" ? OpCode.Not : OpCode.Negate, 0, 0, unary.Line);
                    break;
                case BinaryExpression binary:
                    EmitBinary(binary);
                    break;
                case CallExpression call:
                    EmitCall(call);
                    break;
                case MenuExpression menu:
                    foreach (var label in menu.Labels)
                    {
                        EmitExpression(label);
                        Emit(OpCode.Push, ConstantIndex(ScriptValue.FromNumber(1)), 0, menu.Line);
                    }
                    Emit(OpCode.Menu, menu.Labels.Count, 0, menu.Line);
                    break;
                default:
                    throw new ScriptSyntaxException($"Unknown expression {expression.GetType().Name}", expression.Line, expression.Column);
            }
        }

        private void EmitBinary(BinaryExpression binary)
        {
            if (binary.Operator is "&&" or "||")
            {
                EmitExpression(binary.Left);
                var shortCircuit = Emit(binary.Operator == "&&" ? OpCode.JumpIfFalseKeep : OpCode.JumpIfTrueKeep, 0, 0, binary.Line);
                Emit(OpCode.Pop, 0, 0, binary.Line);
                EmitExpression(binary.Right);
                Patch(shortCircuit, _code.Count);
                return;
            }
            EmitExpression(binary.Left);
            EmitExpression(binary.Right);
            Emit(OpCode.Binary, (int)BinaryFor(binary.Operator, binary.Line, binary.Column), 0, binary.Line);
        }

        private static BinaryOperator BinaryFor(string op, int line, int column) => op switch
        {
            "+" => BinaryOperator.Add,
            "-" => BinaryOperator.Subtract,
            "*" => BinaryOperator.Multiply,
            "/" => BinaryOperator.Divide,
            "%" => BinaryOperator.Remainder,
            "==" => BinaryOperator.Equal,
            "!=" => BinaryOperator.NotEqual,
            "<" => BinaryOperator.Less,
            "<=" => BinaryOperator.LessOrEqual,
            ">" => BinaryOperator.Greater,
            ">=" => BinaryOperator.GreaterOrEqual,
            _ => throw new ScriptSyntaxException($"Unknown operator '{op}'", line, column),
        };

        private void EmitCall(CallExpression call)
        {
            foreach (var argument in call.Arguments) EmitExpression(argument);
            if (Builtins.TryFind(call.Name, out var id))
            {
                var builtin = Builtins.ById(id);
                if (call.Arguments.Count < builtin.MinimumArguments || call.Arguments.Count > builtin.MaximumArguments)
                {
                    var expected = builtin.MinimumArguments == builtin.MaximumArguments
                        ? builtin.MinimumArguments.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : $"{builtin.MinimumArguments} to {builtin.MaximumArguments}";
                    throw new ScriptSyntaxException($"{call.Name}() takes {expected} argument(s) but was given {call.Arguments.Count}", call.Line, call.Column);
                }
                Emit(OpCode.CallBuiltin, id, call.Arguments.Count, call.Line);
                return;
            }
            _references.Add(new FunctionReference(call.Name, call.Arguments.Count, call.Line, call.Column));
            Emit(OpCode.CallFunction, NameIndex(call.Name), call.Arguments.Count, call.Line);
        }
    }
}
