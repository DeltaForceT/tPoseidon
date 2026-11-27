namespace tPoseidon.Core.Net.Dialogs;

public sealed class EventSceneState
{
    public const ushort LandedOpcode = 0x04E9;

    private bool _landed;

    public bool Landed => _landed;

    public void Land() => _landed = true;

    public void Leave() => _landed = false;

    public bool AnswersEnd(bool sceneStarted) => _landed || sceneStarted;
}
