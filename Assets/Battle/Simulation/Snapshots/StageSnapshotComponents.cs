using Unity.Entities;

namespace Archeus.Battle.Snapshots
{
    /// <summary>Operational input fence, not gameplay state. Never rewound.</summary>
    public struct StageSnapshotStatus : IComponentData
    {
        public ulong Revision;
        public bool IsValid;
    }

    public struct RevertStageRequest : IComponentData
    {
        public Entity Player;
        public ulong ExpectedRevision;
    }

    /// <summary>Attach to every routed transient request, so new request types inherit fencing.</summary>
    public struct StageCommandOwner : IComponentData
    {
        public Entity Battle;
        public ulong Revision;
    }
}
