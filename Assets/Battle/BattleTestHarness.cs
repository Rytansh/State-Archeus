using UnityEngine;
using Unity.Entities;
using Archeus.Battle.Components.Requests;
using Archeus.Game.Bootstrap;

public class BattleTestHarness : MonoBehaviour
{
    void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        var em = world.EntityManager;

        ISeedService seedService = RunBootstrap.RootContext.Resolve<ISeedService>();

        var e1 = em.CreateEntity();
        em.AddComponentData(e1, new StartBattleRequest
        {
            BattleID = 1,
            BattleSeed = seedService.CreateDerivedSeed("battle"),
            BattleConfigID = 0
        });

    }
}
