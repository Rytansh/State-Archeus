using Archeus.Battle.Presentation.Presenters;
using Archeus.Battle.Presentation.State;
using Unity.Entities;
using UnityEngine;

namespace Archeus.Battle.Systems.Presentation
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattlePresentationGroup), OrderLast = true)]
    public partial class PlayerViewBindingSystem : SystemBase
    {
        private BattleUIView battleUIView;

        protected override void OnUpdate()
        {
            EnsureBattleUIView();

            if (battleUIView == null)
            {
                return;
            }

            battleUIView.BeginRefresh();

            BindPlayers();

            BindHands();

            BindCards();

            battleUIView.EndRefresh();
        }

        private void BindPlayers()
        {
            foreach (
                RefRO<PlayerPresentationState> state in SystemAPI.Query<
                    RefRO<PlayerPresentationState>
                >()
            )
            {
                if (!battleUIView.TryGetPlayer(state.ValueRO.Side, out PlayerView player))
                {
                    continue;
                }

                player.SetActionPoints(state.ValueRO.ActionPoints);
            }
        }

        private void BindHands()
        {
            foreach (
                RefRO<HandPresentationState> state in SystemAPI.Query<
                    RefRO<HandPresentationState>
                >()
            )
            {
                if (!battleUIView.TryGetPlayer(state.ValueRO.Side, out PlayerView player))
                {
                    continue;
                }

                player.SetMaxHandSize(state.ValueRO.MaxSize);
            }
        }

        private void BindCards()
        {
            foreach (
                RefRO<CardPresentationState> state in SystemAPI.Query<
                    RefRO<CardPresentationState>
                >()
            )
            {
                CardPresentationState card = state.ValueRO;

                if (!card.Location.TryGetHandPosition(out var handPosition))
                {
                    continue;
                }

                if (!battleUIView.TryGetPlayer(card.Side, out PlayerView player))
                {
                    continue;
                }

                if (player.Hand == null)
                {
                    continue;
                }

                player.Hand.TryPresentCard(handPosition, card.RuntimeID, card.DefinitionID);
            }
        }

        private void EnsureBattleUIView()
        {
            if (battleUIView != null)
            {
                return;
            }

            battleUIView = Object.FindFirstObjectByType<BattleUIView>();
        }
    }
}
