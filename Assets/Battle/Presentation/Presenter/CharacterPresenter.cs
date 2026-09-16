using UnityEngine;

namespace Archeus.Battle.Presentation.Presenters
{
    public sealed class CharacterPresenter : MonoBehaviour
    {
        [SerializeField]
        private uint runtimeID;

        [SerializeField]
        private Animator animator;

        [SerializeField]
        private Transform visualRoot;

        [SerializeField]
        private Transform hitCenter;

        [SerializeField]
        private Transform vfxOrigin;

        public uint RuntimeID => runtimeID;

        public Animator Animator => animator;

        public Transform VisualRoot => visualRoot != null ? visualRoot : transform;

        public Transform HitCenter => hitCenter != null ? hitCenter : VisualRoot;

        public Transform VFXOrigin => vfxOrigin != null ? vfxOrigin : VisualRoot;

        public void Initialise(uint newRuntimeID)
        {
            runtimeID = newRuntimeID;
        }
    }
}
