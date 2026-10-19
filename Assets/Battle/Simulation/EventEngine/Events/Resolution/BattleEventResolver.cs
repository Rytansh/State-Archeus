using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Context;

namespace Archeus.Battle.Events.Resolvers
{
    public static class BattleEventResolver
    {
        public static void Resolve(BattleEvent evt, ref BattleContext context, in EventEmissionContext emissionContext)
        {
            switch (evt.Type)
            {
                case BattleEventType.DamageRequested:
                    DamageRequestResolver.Resolve(ref context, evt, in emissionContext);
                    break;
                case BattleEventType.DamageConfirmed:
                    DamageConfirmedResolver.Resolve(ref context, evt, in emissionContext);
                    break;
                case BattleEventType.DamageCalculated:
                    DamageCalculatedResolver.Resolve(ref context, evt, in emissionContext);
                    break;
                case BattleEventType.DamageMitigated:
                    DamageMitigatedResolver.Resolve(ref context, evt, in emissionContext);
                    break;
                case BattleEventType.DamageResolved:
                    DamageResolvedResolver.Resolve(ref context, evt, in emissionContext);
                    break;
                case BattleEventType.EffectApplicationRequested:
                    EffectApplicationRequestedResolver.Resolve(ref context, evt, in emissionContext);
                    break;
                case BattleEventType.EffectApplicationResolved:
                    EffectApplicationResolvedResolver.Resolve(ref context, evt, in emissionContext);
                    break;
                default:
                    return;
            }
        }
    }
}
