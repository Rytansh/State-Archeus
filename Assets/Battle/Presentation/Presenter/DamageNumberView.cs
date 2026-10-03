using TMPro;
using UnityEngine;

namespace Archeus.Battle.Presentation.Presenters
{
    public sealed class DamageNumberView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text label;

        [SerializeField]
        private float lifetime = 0.8f;

        [SerializeField]
        private float riseSpeed = 1.0f;

        [SerializeField]
        private Color normalColour = Color.white;

        [SerializeField]
        private Color critColour = Color.yellow;

        [SerializeField]
        private float critScaleMultiplier = 1.25f;

        private float elapsed;

        private Vector3 baseScale;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        public void Initialise(float value, bool isCrit)
        {
            elapsed = 0f;

            label.text = Mathf.RoundToInt(value).ToString();

            if (isCrit)
            {
                label.fontStyle = FontStyles.Bold;

                label.color = critColour;

                transform.localScale = baseScale * critScaleMultiplier;
            }
            else
            {
                label.fontStyle = FontStyles.Normal;

                label.color = normalColour;

                transform.localScale = baseScale;
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            transform.position += Vector3.up * riseSpeed * Time.deltaTime;

            float normalised = Mathf.Clamp01(elapsed / lifetime);

            Color colour = label.color;

            colour.a = 1f - normalised;

            label.color = colour;

            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
