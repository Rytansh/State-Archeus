using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Events.Definitions;
using Archeus.Battle.Events.Runtime;

namespace Archeus.Battle.Events.Resolvers
{
    public static class BattleEventResolver
    {
        public static void Resolve(BattleEvent evt, ref BattleContext context)
        {
            switch (evt.Type)
            {
                case BattleEventType.DamageRequested:
                    DamageRequestResolver.Resolve(ref context, evt);
                    break;
                case BattleEventType.DamageCalculated:
                    DamageCalculationResolver.Resolve(ref context, evt);
                    break;
                case BattleEventType.DamageMitigated:
                    DamageMitigationResolver.Resolve(ref context, evt);
                    break;
                case BattleEventType.DamageResolved:
                    DamageResolvedResolver.Resolve(ref context, evt);
                    break;
                default:
                    return;
            }
        }
    }
}
