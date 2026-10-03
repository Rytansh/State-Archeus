using Archeus.Battle.Presentation.Plans;
using Unity.Entities;

namespace Archeus.Battle.Components.Presentation
{
    public sealed class PresentationExecutionPlanReference : IComponentData
    {
        public PresentationExecutionPlan Plan;
    }
}
