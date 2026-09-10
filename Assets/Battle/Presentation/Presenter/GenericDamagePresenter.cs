using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.Presentation.Presenters;
using UnityEngine;

namespace Archeus.Battle.Presentation.Generic
{
    public sealed class GenericDamagePresenter
    {
        public void Present(in PresentationFact fact, CharacterPresenter target)
        {
            if (fact.FactType != PresentationFactType.DamageApplied)
                return;

            float damage = fact.FactPayload.HitPayload.Damage;
            bool crit = fact.FactPayload.HitPayload.IsCrit;

            Debug.Log($"Presenting {damage} damage on {target.gameObject.name} | Crit={crit}");

            // Temporary visible test.
            target.transform.localScale *= 0.9f;
        }
    }
}
