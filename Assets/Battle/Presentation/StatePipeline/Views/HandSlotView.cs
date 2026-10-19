using Archeus.Battle.Components.Combat;
using Archeus.Battle.Presentation.Views;
using UnityEngine;

namespace Archeus.Battle.Presentation.Presenters
{
    public sealed class HandSlotView : MonoBehaviour
    {
        [SerializeField]
        private HandPosition position;

        [SerializeField]
        private HandCardView cardView;

        private bool occupiedThisRefresh;

        public HandPosition Position => position;

        public void BeginRefresh()
        {
            occupiedThisRefresh = false;
        }

        public void Present(uint runtimeID, uint definitionID)
        {
            occupiedThisRefresh = true;

            if (cardView == null)
            {
                Debug.LogError($"{name}: HandSlotView has no HandCardView assigned.");

                return;
            }

            cardView.Show(runtimeID, definitionID);
        }

        public void EndRefresh()
        {
            if (!occupiedThisRefresh && cardView != null)
            {
                cardView.Hide();
            }
        }

        public void SetAvailable(bool available)
        {
            gameObject.SetActive(available);
        }
    }
}
