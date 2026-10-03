using System;
using Archeus.Battle.Presentation.Plans;
using Archeus.Battle.Presentation.Presenters;
using Archeus.Battle.Presentation.Timeline;
using UnityEngine;
using UnityEngine.Playables;

namespace Archeus.Battle.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayableDirector))]
    public sealed class BattlePresentationRunner : MonoBehaviour, INotificationReceiver
    {
        [SerializeField]
        private PlayableDirector primaryDirector;

        [SerializeField]
        private DamageNumberPresenter damageNumberPresenter;

        public DamageNumberPresenter DamageNumberPresenter => damageNumberPresenter;

        public PlayableDirector PrimaryDirector => primaryDirector;

        public PresentationExecutionPlan ActivePlan { get; private set; }

        public event Action<int> ImpactRequested;

        public event Action<PresentationExecutionPlan> PlaybackStopped;

        private void Awake()
        {
            if (primaryDirector == null)
            {
                primaryDirector = GetComponent<PlayableDirector>();
            }

            primaryDirector.playOnAwake = false;

            primaryDirector.extrapolationMode = DirectorWrapMode.None;

            primaryDirector.stopped += OnDirectorStopped;
        }

        private void OnDestroy()
        {
            if (primaryDirector != null)
            {
                primaryDirector.stopped -= OnDirectorStopped;
            }
        }

        public void OnNotify(Playable origin, INotification notification, object context)
        {
            if (notification is not PresentationConsumptionMarker marker)
            {
                return;
            }

            ImpactRequested?.Invoke(marker.ImpactIndex);
        }

        public bool TryPrepare(PresentationExecutionPlan plan)
        {
            if (plan == null)
                return false;

            if (plan.Timeline == null)
                return false;

            if (ActivePlan != null)
                return false;

            ActivePlan = plan;

            primaryDirector.playableAsset = plan.Timeline;

            primaryDirector.time = 0;

            return true;
        }

        public bool PlayPrepared()
        {
            if (ActivePlan == null)
                return false;

            if (primaryDirector.playableAsset == null)
                return false;

            primaryDirector.Play();

            return true;
        }

        public void CancelPrepared()
        {
            ActivePlan = null;

            primaryDirector.playableAsset = null;
            primaryDirector.time = 0;
        }

        public void ResetDirector()
        {
            primaryDirector.playableAsset = null;
            primaryDirector.time = 0;
        }

        private void OnDirectorStopped(PlayableDirector director)
        {
            PresentationExecutionPlan completedPlan = ActivePlan;

            ActivePlan = null;

            if (completedPlan == null)
                return;

            PlaybackStopped?.Invoke(completedPlan);
        }
    }
}
