using UnityEngine;
using Unity.Entities;
using DBUS.Battle.Components.Requests;

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
            BattleSeed = 123456UL,
            BattleConfigID = 0
        });

        var e2 = em.CreateEntity();
        em.AddComponentData(e2, new StartBattleRequest
        {
            BattleID = 2,
            BattleSeed = 123456UL,
            BattleConfigID = 1
        });

    }
}
