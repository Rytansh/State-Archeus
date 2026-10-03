using Unity.Entities;

public enum PresentationMirrorSyncStatus : byte
{
    Synchronized = 0,
    Recovering = 1,
    RecoveryFailed = 2,
}

public struct PresentationMirrorRevision : IComponentData
{
    public ulong BattleRuntimeID;

    public ulong Value;

    public PresentationMirrorSyncStatus Status;
}
