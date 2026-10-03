using TMPro;
using UnityEngine;

namespace Archeus.Battle.Presentation.Presenters
{
    public sealed class HandCardView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text runtimeIDText;

        [SerializeField]
        private TMP_Text definitionIDText;

        private uint currentRuntimeID;

        public uint RuntimeID => currentRuntimeID;

        public void Show(uint runtimeID, uint definitionID)
        {
            currentRuntimeID = runtimeID;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

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

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void Awake()
        {
            if (runtimeIDText == null)
            {
                Debug.LogError($"{name}: HandCardView has no Runtime ID TMP reference assigned.");
            }

            if (definitionIDText == null)
            {
                Debug.LogError(
                    $"{name}: HandCardView has no Definition ID TMP reference assigned."
                );
            }

            Hide();
        }
    }
}
