using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Archeus.Battle.Presentation.Presenters
{
    public sealed class HandCardView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text runtimeIDText;

        [SerializeField]
        private TMP_Text definitionIDText;

        [SerializeField]
        private Button button;

        private uint currentRuntimeID;
        private bool hasCard;

        public uint RuntimeID => currentRuntimeID;

        public event Action<uint> Clicked;

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClicked);
            }

            Hide();
        }

        public void Show(uint runtimeID, uint definitionID)
        {
            currentRuntimeID = runtimeID;
            hasCard = true;

            gameObject.SetActive(true);

            if (runtimeIDText != null)
            {
                runtimeIDText.text = $"Runtime: {runtimeID}";
            }

            if (definitionIDText != null)
            {
                definitionIDText.text = $"Definition: {definitionID}";
            }
        }

        public void Hide()
        {
            currentRuntimeID = 0;
            hasCard = false;

            gameObject.SetActive(false);
        }

        private void HandleClicked()
        {
            if (!hasCard)
            {
                return;
            }

            Clicked?.Invoke(currentRuntimeID);
        }
    }
}
