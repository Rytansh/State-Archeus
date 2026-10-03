using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Game.Bootstrap;
using Unity.Entities;
using UnityEngine;

public class BattleTestHarness : MonoBehaviour
{
    private void Start()
    {
        EntityManager world = BattleSimulationBootstrap.SimulationEcsWorld.EntityManager;

        Entity request = world.CreateEntity();

        world.AddComponentData(
            request,
            new StartBattleRequest
            {
                BattleID = 1,
                BattleSeed = 12345678,
                BattleConfigID = 0,
            }
        );

        DynamicBuffer<BattleLoadoutEntry> loadout = world.AddBuffer<BattleLoadoutEntry>(request);

        // SIDE A
        loadout.Add(
            new BattleLoadoutEntry
            {
                Side = BattleSide.SideA,
                CardDefinitionID = StableHash32.HashFromString("C1"),
                CardType = RuntimeCardType.Character,
            }
        );

        loadout.Add(
            new BattleLoadoutEntry
            {
                Side = BattleSide.SideA,
                CardDefinitionID = StableHash32.HashFromString("C2"),
                CardType = RuntimeCardType.Character,
            }
        );

        // Repeated definitions are completely fine for this test.
        loadout.Add(
            new BattleLoadoutEntry
            {
                Side = BattleSide.SideA,
                CardDefinitionID = StableHash32.HashFromString("C1"),
                CardType = RuntimeCardType.Character,
            }
        );

        // SIDE B
        loadout.Add(
            new BattleLoadoutEntry
            {
                Side = BattleSide.SideB,
                CardDefinitionID = StableHash32.HashFromString("C2"),
                CardType = RuntimeCardType.Character,
            }
        );

        loadout.Add(
            new BattleLoadoutEntry
            {
                Side = BattleSide.SideB,
                CardDefinitionID = StableHash32.HashFromString("C1"),
                CardType = RuntimeCardType.Character,
            }
        );
    }
}
