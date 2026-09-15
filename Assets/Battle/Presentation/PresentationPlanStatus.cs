using Unity.Entities;

namespace Archeus.Battle.Components.Presentation
{
    public enum PresentationPlanStatus : byte
    {
        Ready = 0,
        Running = 1,
        Completed = 2,
        Failed = 3,
    }

    public struct PresentationPlanPlaybackState : IComponentData
    {
        public PresentationPlanStatus Status;
    }
}
