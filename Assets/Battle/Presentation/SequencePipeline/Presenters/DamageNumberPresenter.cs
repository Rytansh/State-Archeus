using UnityEngine;

namespace Archeus.Battle.Presentation.Presenters
{
    public sealed class DamageNumberPresenter : MonoBehaviour
    {
        [SerializeField]
        private DamageNumberView prefab;

        public void Show(CharacterPresenter target, float value, bool isCrit)
        {
            if (target == null || target.HitCenter == null || prefab == null)
            {
                return;
            }

            DamageNumberView view = Instantiate(
                prefab,
                target.HitCenter.position,
                Quaternion.identity,
                transform
            );

            view.Initialise(value, isCrit);
        }
    }
}
