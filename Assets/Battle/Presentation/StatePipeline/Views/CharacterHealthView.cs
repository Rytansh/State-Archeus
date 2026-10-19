using UnityEngine;
using UnityEngine.UI;

namespace Archeus.Battle.Presentation.Views
{
    public sealed class CharacterHealthView : MonoBehaviour
    {
        [SerializeField]
        private Image fillImage;

        [SerializeField]
        [Min(0f)]
        private float fillSpeed = 3f;

        private float targetFill = 1f;

        private bool initialized;

        public void SetHealth(float currentHealth, float maxHealth)
        {
            if (fillImage == null)
            {
                return;
            }

            targetFill = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

            /*
             * First state assignment should not animate
             * from some arbitrary prefab value.
             */
            if (!initialized)
            {
                fillImage.fillAmount = targetFill;

                initialized = true;
            }
        }

        private void Update()
        {
            if (!initialized || fillImage == null)
            {
                return;
            }

            fillImage.fillAmount = Mathf.MoveTowards(
                fillImage.fillAmount,
                targetFill,
                fillSpeed * Time.deltaTime
            );
        }
    }
}
