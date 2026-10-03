using Archeus.Content.Registries;
using Unity.Entities;
using UnityEngine.SceneManagement;

namespace Archeus.Game.Bootstrap
{
    public partial struct GameStartGateSystem : ISystem
    {
        private bool started;

        public void OnUpdate(ref SystemState state)
        {
            if (started)
                return;

            bool bootstrapComplete =
                SystemAPI
                    .QueryBuilder()
                    .WithAll<GameBootstrapCompleteTag>()
                    .Build()
                    .CalculateEntityCount() > 0;

            if (!bootstrapComplete)
                return;

            SceneManager.LoadSceneAsync("MenuScene", LoadSceneMode.Additive);

            started = true;
        }
    }
}
