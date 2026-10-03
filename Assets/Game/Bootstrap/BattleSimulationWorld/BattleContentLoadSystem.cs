using Archeus.Content.Registries;
using Archeus.Core.Debugging;
using Unity.Entities;
using Unity.Scenes;

namespace Archeus.Game.Bootstrap
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct BattleContentLoadSystem : ISystem
    {
        private bool loadRequested;
        private Entity gameConfigSceneEntity;

        public void OnUpdate(ref SystemState state)
        {
            if (!loadRequested)
            {
                var sceneGuid = SceneSystem.GetSceneGUID(
                    ref state,
                    "Assets/Other/Scenes/GameConfig.unity"
                );

                gameConfigSceneEntity = SceneSystem.LoadSceneAsync(state.WorldUnmanaged, sceneGuid);

                loadRequested = true;

                return;
            }

            if (!SceneSystem.IsSceneLoaded(state.WorldUnmanaged, gameConfigSceneEntity))
            {
                return;
            }

            bool registryExists = SystemAPI.HasSingleton<ContentBlobRegistryComponent>();

            Logging.Info(
                LogCategory.Testing,
                $"GameConfig loaded. " + $"Registry found: {registryExists}"
            );

            state.Enabled = false;
        }
    }
}
