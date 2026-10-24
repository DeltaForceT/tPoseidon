namespace tPoseidon.Core.Net.World;

public interface IZoneCreator
{
    void Build(Zone zone);
}

public interface IZoneEntityCreator
{
    int Spawn(Zone zone);
}
