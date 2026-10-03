using TMPro;
using UnityEngine;

namespace Archeus.Battle.Presentation.Views
{
    public sealed class ActionPointView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text text;

        public void SetActionPoints(int value)
        {
            if (text == null)
            {
                Debug.LogError($"{name}: ActionPointView has no TMP_Text assigned.");

                return;
            }

            text.text = value.ToString();
        }
    }
}
