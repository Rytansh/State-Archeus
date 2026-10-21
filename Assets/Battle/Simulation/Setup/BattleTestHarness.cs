using Archeus.Battle.Components.Core;
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

        // BATTLE REQUEST
        world.AddComponentData(
            request,
            new StartBattleRequest
            {
                BattleID = 1,
                BattleSeed = 12345678,
                BattleConfigID = 0,
            }
        );

        // PLAYER CONFIGURATIONS
        DynamicBuffer<BattlePlayerEntry> players = world.AddBuffer<BattlePlayerEntry>(request);

        players.Add(
            new BattlePlayerEntry
            {
                PlayerID = 1001,
                LeadCharacterDefinitionID = StableHash32.HashFromString("C1"),
            }
        );

        players.Add(
            new BattlePlayerEntry
            {
                PlayerID = 1002,
                LeadCharacterDefinitionID = StableHash32.HashFromString("C2"),
            }
        );

        // PLAYER DECK CONFIGURATIONS
        DynamicBuffer<PlayerDeckEntry> deck = world.AddBuffer<PlayerDeckEntry>(request);

        // PLAYER 1001
        deck.Add(
            new PlayerDeckEntry
            {
                PlayerID = 1001,
                CardDefinitionID = StableHash32.HashFromString("C1"),
                CardType = RuntimeCardType.Character,
            }
        );

        deck.Add(
            new PlayerDeckEntry
            {
                PlayerID = 1001,
                CardDefinitionID = StableHash32.HashFromString("C2"),
                CardType = RuntimeCardType.Character,
            }
        );

        // Repeated definitions are permitted in this test fixture.
        deck.Add(
            new PlayerDeckEntry
            {
                PlayerID = 1001,
                CardDefinitionID = StableHash32.HashFromString("C1"),
                CardType = RuntimeCardType.Character,
            }
        );

        // PLAYER 1002
        deck.Add(
            new PlayerDeckEntry
            {
                PlayerID = 1002,
                CardDefinitionID = StableHash32.HashFromString("C2"),
                CardType = RuntimeCardType.Character,
            }
        );

        deck.Add(
            new PlayerDeckEntry
            {
                PlayerID = 1002,
                CardDefinitionID = StableHash32.HashFromString("C1"),
                CardType = RuntimeCardType.Character,
            }
        );
    }
}
