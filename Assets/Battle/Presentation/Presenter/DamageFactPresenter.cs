using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.Presentation.Presenters;

public sealed class DamageFactPresenter
{
    private readonly DamageNumberPresenter damageNumberPresenter;

    public DamageFactPresenter(DamageNumberPresenter damageNumberPresenter)
    {
        this.damageNumberPresenter = damageNumberPresenter;
    }

    public void Present(in PresentationFact fact, float displayValue, CharacterPresenter target)
    {
        if (fact.FactType != PresentationFactType.DamageApplied)
        {
            return;
        }

        bool isCrit = fact.FactPayload.HitPayload.IsCrit;

        damageNumberPresenter.Show(target, displayValue, isCrit);
    }
}
