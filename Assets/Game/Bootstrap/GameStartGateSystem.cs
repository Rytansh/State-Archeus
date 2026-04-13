using Unity.Entities;
using UnityEngine.SceneManagement;
using Archeus.Content.Registries;

namespace Archeus.Game.Bootstrap
{
    public partial struct GameStartGateSystem : ISystem
    {
        private bool started;

        public void OnUpdate(ref SystemState state)
        {
            if (started)
                return;

            bool registryExists =
                SystemAPI.QueryBuilder()
                    .WithAll<ContentBlobRegistryComponent>()
                    .Build()
                    .CalculateEntityCount() > 0;

            bool bootstrapComplete =
                SystemAPI.QueryBuilder()
                    .WithAll<GameBootstrapCompleteTag>()
                    .Build()
                    .CalculateEntityCount() > 0;

            if (!registryExists || !bootstrapComplete)
                return;

            SceneManager.LoadSceneAsync("MenuScene", LoadSceneMode.Additive);

            started = true;
        }
    }
}
