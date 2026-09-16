using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Archeus.Battle.Presentation.Timeline
{
    [Serializable]
    public sealed class PresentationConsumptionMarker
        : Marker,
            INotification,
            INotificationOptionProvider
    {
        [SerializeField]
        private int impactIndex;

        public int ImpactIndex => impactIndex;

        public PropertyName id => new PropertyName(nameof(PresentationConsumptionMarker));

        public NotificationFlags flags => NotificationFlags.TriggerOnce;
    }
}
