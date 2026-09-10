using UnityEngine;

namespace Archeus.Battle.Presentation.Presenters
{
    public sealed class CharacterPresenter : MonoBehaviour
    {
        [SerializeField]
        private uint runtimeID;

        // [SerializeField]
        // private Animator animator;

        // [SerializeField]
        // private Transform root;

        // [SerializeField]
        // private Transform hitCenter;

        // [SerializeField]
        // private Transform vfxOrigin;

        public uint RuntimeID => runtimeID;

        // public Animator Animator => animator;

        // public Transform Root => root != null ? root : transform;
        public Transform Root => transform;

        // public Transform HitCenter => hitCenter != null ? hitCenter : transform;
        public Transform HitCenter => transform;

        // public Transform VFXOrigin => vfxOrigin != null ? vfxOrigin : transform;
        public Transform VFXOrigin => transform;

        public void Initialise(uint newRuntimeID)
        {
            runtimeID = newRuntimeID;
        }
    }
}
