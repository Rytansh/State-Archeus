using Archeus.Battle.Components.Ownership;
using UnityEngine;

namespace Archeus.Battle.Presentation.Presenters
{
    public sealed class BattleUIView : MonoBehaviour
    {
        [SerializeField]
        private PlayerView[] players;

        [SerializeField]
        private BattleSide displayedSide = BattleSide.SideA;

        [SerializeField]
        private bool showBothPlayersForDebug;

        public void BeginRefresh()
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null)
                {
                    players[i].BeginRefresh();
                }
            }
        }

        public void EndRefresh()
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null)
                {
                    players[i].EndRefresh();
                }
            }

            RefreshVisibility();
        }

        public bool TryGetPlayer(BattleSide side, out PlayerView player)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].Side == side)
                {
                    player = players[i];
                    return true;
                }
            }

            player = null;
            return false;
        }

        public void SetDisplayedSide(BattleSide side)
        {
            displayedSide = side;
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            for (int i = 0; i < players.Length; i++)
            {
                PlayerView player = players[i];

                if (player == null)
                {
                    continue;
                }

                bool visible = showBothPlayersForDebug || player.Side == displayedSide;

                player.SetVisible(visible);
            }
        }
    }
}
