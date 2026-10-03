using Archeus.Battle.Components.Combat;
using UnityEngine;

namespace Archeus.Battle.Presentation.Presenters
{
    public sealed class HandView : MonoBehaviour
    {
        [SerializeField]
        private HandSlotView[] slots;

        public void BeginRefresh()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].BeginRefresh();
                }
            }
        }

        public void EndRefresh()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].EndRefresh();
                }
            }
        }

        public bool TryPresentCard(HandPosition position, uint runtimeID, uint definitionID)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                HandSlotView slot = slots[i];

                if (slot == null || slot.Position != position)
                {
                    continue;
                }

                slot.Present(runtimeID, definitionID);

                return true;
            }

            Debug.LogWarning($"{name}: No HandSlotView found for {position}.");

            return false;
        }

        public void SetMaxSize(int maxSize)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                slots[i].SetAvailable(i < maxSize);
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}
