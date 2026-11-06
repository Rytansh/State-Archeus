using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Snapshots;
using Archeus.Game.Bootstrap;
using Archeus.Game.Input;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public sealed class BattleSnapshotSmokePanel : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField]
    private bool showDebugPanel = true;

    private void OnGUI()
    {
        if (!showDebugPanel)
            return;

        World world = BattleSimulationBootstrap.SimulationEcsWorld;

        if (
            world == null
            || !world.IsCreated
            || RunBootstrap.RootContext == null
            || !RunBootstrap.RootContext.TryResolve<BattleInputGateway>(out var gateway)
        )
        {
            GUI.Box(new Rect(10, 10, 430, 55), "Waiting for battle bootstrap...");
            return;
        }

        EntityManager em = world.EntityManager;

        using var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<BattleTag>(),
            ComponentType.ReadOnly<BattleID>(),
            ComponentType.ReadOnly<BattleState>(),
            ComponentType.ReadOnly<ActiveTurnPlayer>(),
            ComponentType.ReadOnly<StageSnapshotStatus>()
        );

        using NativeArray<Entity> battles = query.ToEntityArray(Allocator.Temp);

        if (battles.Length == 0)
        {
            GUI.Box(new Rect(10, 10, 430, 55), "Waiting for an active battle...");
            return;
        }

        Entity battle = battles[0];
        Entity player = em.GetComponentData<ActiveTurnPlayer>(battle).Player;

        if (!em.Exists(player) || !em.HasComponent<BattleSideAssignment>(player))
            return;

        ulong battleID = em.GetComponentData<BattleID>(battle).Value;

        BattleSide side = em.GetComponentData<BattleSideAssignment>(player).Side;

        BattlePhase phase = em.GetComponentData<BattleState>(battle).Phase;

        StageSnapshotStatus status = em.GetComponentData<StageSnapshotStatus>(battle);

        int ap = em.HasComponent<RemainingActionPoints>(player)
            ? em.GetComponentData<RemainingActionPoints>(player).Value
            : -1;

        bool hasHand = em.HasBuffer<HandCard>(player);
        bool hasField = em.HasBuffer<FieldCard>(player);
        bool hasDeck = em.HasBuffer<DeckCard>(player);

        int handCount = hasHand ? em.GetBuffer<HandCard>(player).Length : 0;

        int fieldCount = hasField ? em.GetBuffer<FieldCard>(player).Length : 0;

        int deckCount = hasDeck ? em.GetBuffer<DeckCard>(player).Length : 0;

        GUILayout.BeginArea(new Rect(10, 10, 440, 700), GUI.skin.box);

        GUILayout.Label("STATE: ARCHEUS - SNAPSHOT DEBUG");
        GUILayout.Space(5);

        GUILayout.Label($"Battle ID: {battleID}");
        GUILayout.Label($"Active Player: {side}");
        GUILayout.Label($"Stage: {phase}");
        GUILayout.Label($"Snapshot Valid: {status.IsValid}");
        GUILayout.Label($"Snapshot Revision: {status.Revision}");
        GUILayout.Label($"Remaining AP: {ap}");

        GUILayout.Space(5);
        GUILayout.Label($"Deck: {deckCount} cards");
        GUILayout.Label($"Hand: {handCount} cards");
        GUILayout.Label($"Field: {fieldCount} cards");

        if (hasHand)
        {
            var hand = em.GetBuffer<HandCard>(player);

            for (int i = 0; i < hand.Length; i++)
            {
                GUILayout.Label($"Hand [{i}]: Card ID {GetCardID(em, hand[i].Card)}");
            }
        }

        if (hasField)
        {
            var field = em.GetBuffer<FieldCard>(player);

            for (int i = 0; i < field.Length; i++)
            {
                GUILayout.Label(
                    $"Field [{i}]: {field[i].Position} - "
                        + $"Card ID {GetCardID(em, field[i].Card)}"
                );
            }
        }

        GUILayout.Space(12);
        GUILayout.Label("--- DRAWING STAGE ---");

        bool oldEnabled = GUI.enabled;
        bool canDraw = status.IsValid && phase == BattlePhase.Drawing;

        GUI.enabled = canDraw;

        if (GUILayout.Button("DRAW / REPLENISH"))
        {
            gateway.RequestDrawCards(battleID, side);
        }

        // Place the first hand card into the first free
        // attacking-force slot.
        if (hasHand && handCount > 0)
        {
            if (GUILayout.Button("PLACE FIRST HAND CARD"))
            {
                Entity card = em.GetBuffer<HandCard>(player)[0].Card;

                if (
                    em.Exists(card)
                    && em.HasComponent<CardRuntimeID>(card)
                    && TryFindFreeFieldSlot(em, player, out var position)
                )
                {
                    uint cardID = em.GetComponentData<CardRuntimeID>(card).Value;

                    gateway.RequestPlaceCard(battleID, side, cardID, position);
                }
                else
                {
                    Debug.LogWarning("No valid card or free attacking-force slot.");
                }
            }
        }

        if (hasField && fieldCount > 0)
        {
            DynamicBuffer<FieldCard> field = em.GetBuffer<FieldCard>(player);

            Entity retreatCandidate = Entity.Null;

            // Find the first actual attacking-force character.
            for (int i = 0; i < field.Length; i++)
            {
                FieldCard entry = field[i];

                bool isAttackingForce =
                    entry.Position == FieldPosition.AttackingForceSlot1
                    || entry.Position == FieldPosition.AttackingForceSlot2
                    || entry.Position == FieldPosition.AttackingForceSlot3;

                if (!isAttackingForce)
                    continue;

                Entity candidate = entry.Card;

                if (!em.Exists(candidate) || !em.HasComponent<CardRuntimeID>(candidate))
                    continue;

                // Never allow the designated lead to be selected.
                if (
                    em.HasComponent<LeadCharacter>(player)
                    && em.GetComponentData<LeadCharacter>(player).Value == candidate
                )
                    continue;

                // Respect individual card restrictions too.
                if (em.HasComponent<CannotRetreatTag>(candidate))
                    continue;

                retreatCandidate = candidate;
                break;
            }

            if (retreatCandidate != Entity.Null)
            {
                if (GUILayout.Button("RETREAT FIRST ATTACKING FORCE CARD"))
                {
                    uint cardID = em.GetComponentData<CardRuntimeID>(retreatCandidate).Value;

                    gateway.RequestRetreatCard(battleID, side, cardID);
                }
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("--- SNAPSHOT CONTROL ---");

        GUI.enabled =
            status.IsValid && (phase == BattlePhase.Drawing || phase == BattlePhase.Planning);

        if (GUILayout.Button("REVERT CURRENT STAGE"))
        {
            gateway.RequestRevertStage(battleID, side);
        }

        GUILayout.Space(10);
        GUILayout.Label("--- STAGE TRANSITIONS ---");

        GUI.enabled = canDraw;

        if (GUILayout.Button("COMMIT DRAWING"))
        {
            gateway.RequestEndDrawing(battleID, side);
        }

        GUI.enabled = status.IsValid && phase == BattlePhase.Planning;

        if (GUILayout.Button("COMMIT PLANNING"))
        {
            gateway.RequestEndPlanning(battleID, side);
        }

        GUI.enabled = oldEnabled;

        GUILayout.EndArea();
    }

    private static string GetCardID(EntityManager em, Entity card)
    {
        if (!em.Exists(card) || !em.HasComponent<CardRuntimeID>(card))
            return "Invalid";

        return em.GetComponentData<CardRuntimeID>(card).Value.ToString();
    }

    private static bool TryFindFreeFieldSlot(
        EntityManager em,
        Entity player,
        out FieldPosition position
    )
    {
        var field = em.GetBuffer<FieldCard>(player);

        FieldPosition[] candidates =
        {
            FieldPosition.AttackingForceSlot1,
            FieldPosition.AttackingForceSlot2,
            FieldPosition.AttackingForceSlot3,
        };

        foreach (FieldPosition candidate in candidates)
        {
            bool occupied = false;

            for (int i = 0; i < field.Length; i++)
            {
                if (field[i].Position == candidate)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied)
            {
                position = candidate;
                return true;
            }
        }

        position = default;
        return false;
    }
}
