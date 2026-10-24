namespace tPoseidon.Core.Game.Scripting.Compile;

public enum OpCode : byte
{
    Push,
    LoadLocal,
    StoreLocal,
    LoadVariable,
    StoreVariable,
    Binary,
    Not,
    Negate,
    Jump,
    JumpIfFalse,
    JumpIfFalseKeep,
    JumpIfTrueKeep,
    Pop,
    CallBuiltin,
    CallFunction,
    Return,
    Mes,
    Next,
    Close,
    End,
    Fallback,
    Menu,
}

public enum BinaryOperator : byte
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Remainder,
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
}

public readonly record struct Instruction(OpCode Op, int A, int B, int Line);

public sealed class Routine
{
    public Routine(string name, string file, int parameterCount, int localCount, IReadOnlyList<Instruction> code, IReadOnlyList<ScriptValue> constants, IReadOnlyList<string> names)
    {
        Name = name;
        File = file;
        ParameterCount = parameterCount;
        LocalCount = localCount;
        Code = code;
        Constants = constants;
        Names = names;
    }

    public string Name { get; }

    public string File { get; }

    public int ParameterCount { get; }

    public int LocalCount { get; }

    public IReadOnlyList<Instruction> Code { get; }

    public IReadOnlyList<ScriptValue> Constants { get; }

    public IReadOnlyList<string> Names { get; }
}
