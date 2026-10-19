using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Presentation.Views;
using UnityEngine;

namespace Archeus.Battle.Presentation.Presenters
{
    public sealed class PlayerView : MonoBehaviour
    {
        [SerializeField]
        private BattleSide side;

        [SerializeField]
        private HandView handView;

        [SerializeField]
        private ActionPointView actionPointView;

        public BattleSide Side => side;

        public HandView Hand => handView;

        public void BeginRefresh()
        {
            if (handView != null)
            {
                handView.BeginRefresh();
            }
        }

        public void EndRefresh()
        {
            if (handView != null)
            {
                handView.EndRefresh();
            }
        }

        public void SetActionPoints(int value)
        {
            if (actionPointView != null)
            {
                actionPointView.SetActionPoints(value);
            }
        }

        public void SetMaxHandSize(int value)
        {
            if (handView != null)
            {
                handView.SetMaxSize(value);
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}
