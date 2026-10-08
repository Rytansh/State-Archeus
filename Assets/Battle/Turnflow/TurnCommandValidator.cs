using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Turns;
using Unity.Entities;

namespace Archeus.Battle.Runtime
{
    public static class TurnCommandValidator
    {
        public static bool TryGetActiveBattle(
            ref SystemState state,
            Entity player,
            BattlePhase requiredPhase,
            out Entity battle
        )
        {
            battle = Entity.Null;

            EntityManager entityManager = state.EntityManager;

            if (!entityManager.Exists(player))
                return false;

            if (!entityManager.HasComponent<OwnedBattle>(player))
                return false;

            battle = entityManager.GetComponentData<OwnedBattle>(player).Battle;

            if (
                battle == Entity.Null
                || !entityManager.Exists(battle)
                || !entityManager.HasComponent<BattleState>(battle)
                || !entityManager.HasComponent<ActiveTurnPlayer>(battle)
            )
            {
                battle = Entity.Null;
                return false;
            }

            BattleState battleState = entityManager.GetComponentData<BattleState>(battle);

            if (battleState.Phase != requiredPhase)
                return false;

            ActiveTurnPlayer activeTurnPlayer = entityManager.GetComponentData<ActiveTurnPlayer>(
                battle
            );

            if (activeTurnPlayer.Player != player)
                return false;

            return true;
        }
    }
}
