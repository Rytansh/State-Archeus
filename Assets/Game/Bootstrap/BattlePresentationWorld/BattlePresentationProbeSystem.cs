using Archeus.Core.Debugging;
using Unity.Entities;

namespace Archeus.Battle.Systems.Presentation
{
    [DisableAutoCreation]
    public partial struct BattlePresentationProbeSystem : ISystem
    {
        private bool hasLogged;

        public void OnUpdate(ref SystemState state)
        {
            if (hasLogged)
                return;

            Logging.Info(
                LogCategory.Setup,
                $"Battle Presentation ECS World updating: " + $"{state.WorldUnmanaged.Name}"
            );

            hasLogged = true;
        }
    }
}
