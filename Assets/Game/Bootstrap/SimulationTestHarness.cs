using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Tags;
using Archeus.Core.Debugging;
using Archeus.Game.Bootstrap;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public sealed class SimulationTestHarness : MonoBehaviour
{
    private EntityManager entityManager;

    [SerializeField]
    private BattleSide controlledSide = BattleSide.SideA;

    [SerializeField]
    private HandPosition selectedHandPosition = HandPosition.Slot1;

    [SerializeField]
    private FieldPosition selectedFieldPosition = FieldPosition.AttackingForceSlot1;
    private Entity player = Entity.Null;

    private void Start()
    {
        entityManager = BattleSimulationBootstrap.SimulationEcsWorld.EntityManager;
    }

    private void Update()
    {
        if (!TryResolvePlayer())
            return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            CreateRequest(new EndPlanningRequest { Player = player });
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            CreateRequest(new PlayActionRequest { Player = player });
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            if (!entityManager.HasBuffer<HandCard>(player))
                return;

            DynamicBuffer<HandCard> hand = entityManager.GetBuffer<HandCard>(player);

            Entity selectedCard = Entity.Null;

            for (int i = 0; i < hand.Length; i++)
            {
                if (hand[i].Position != selectedHandPosition)
                    continue;

                selectedCard = hand[i].Card;
                break;
            }

            if (selectedCard == Entity.Null)
            {
                Logging.Warn(LogCategory.Combat, $"No card exists in {selectedHandPosition}!");

                return;
            }

            CreateRequest(
                new PlaceCardRequest
                {
                    Player = player,
                    CardToPlace = selectedCard,
                    Position = selectedFieldPosition,
                }
            );
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            CreateRequest(new CycleTargetRequest { Player = player, Direction = -1 });
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            CreateRequest(new CycleTargetRequest { Player = player, Direction = 1 });
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            CreateRequest(new CycleCharacterRequest { Player = player, Direction = 1 });
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            CreateRequest(new CycleCharacterRequest { Player = player, Direction = -1 });
        }
    }

    private bool TryResolvePlayer()
    {
        if (player != Entity.Null && entityManager.Exists(player))
        {
            Team currentTeam = entityManager.GetComponentData<Team>(player);

            if (currentTeam.Side == controlledSide)
            {
                return true;
            }

            player = Entity.Null;
        }

        EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerTag>(),
            ComponentType.ReadOnly<OwnedBattle>(),
            ComponentType.ReadOnly<Team>()
        );

        using NativeArray<Entity> players = query.ToEntityArray(Allocator.Temp);

        Entity resolvedPlayer = Entity.Null;

        for (int i = 0; i < players.Length; i++)
        {
            Entity candidate = players[i];

            Team team = entityManager.GetComponentData<Team>(candidate);

            if (team.Side != controlledSide)
            {
                continue;
            }

            if (resolvedPlayer != Entity.Null)
            {
                Debug.LogError(
                    $"SimulationTestHarness found more than one player on {controlledSide}."
                );

                query.Dispose();
                return false;
            }

            resolvedPlayer = candidate;
        }

        query.Dispose();

        if (resolvedPlayer == Entity.Null)
        {
            return false;
        }

        player = resolvedPlayer;

        return true;
    }

    private void CreateRequest<T>(T request)
        where T : unmanaged, IComponentData
    {
        Entity entity = entityManager.CreateEntity();

        entityManager.AddComponentData(entity, request);

        entityManager.AddComponent<InputSystemTag>(entity);
    }
}
