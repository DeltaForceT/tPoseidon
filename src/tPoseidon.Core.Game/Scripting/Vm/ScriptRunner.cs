using tPoseidon.Core.Game.Scripting.Compile;
using tPoseidon.Core.Game.Scripting.Host;
using tPoseidon.Core.Game.Scripting.Library;

namespace tPoseidon.Core.Game.Scripting.Vm;

public static class ScriptRunner
{
    public static bool TryRun(Routine routine, ScriptLibrary library, IScriptHost host, Action<string> log, int instructionBudget = ScriptMachine.DefaultInstructionBudget)
    {
        ArgumentNullException.ThrowIfNull(routine);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(log);
        try
        {
            var result = new ScriptMachine(routine, library.FindFunction, instructionBudget).Start(host);
            if (result.State is RunState.AwaitingNext or RunState.AwaitingMenu or RunState.Closed)
            {
                log($"{routine.File}: {routine.Name} showed dialog text, which only NPC dialogs can do; use tell() instead");
                return false;
            }
            return true;
        }
        catch (ScriptRuntimeException exception)
        {
            log(exception.Message);
            return false;
        }
    }
}
