using DBUS.Core.Components.GameState;
using Unity.Entities;
using UnityEngine.SceneManagement;

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
