using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Payloads;
using Archeus.Battle.Events.Context;
using Archeus.Battle.Events.Factory;

namespace Archeus.Battle.Events.Resolvers
{
    public static class EffectApplicationRequestedResolver
    {
        public static void Resolve(ref BattleContext ctx, BattleEvent evt, in EventEmissionContext emissionContext)
        {
            //any intermediate effect request logic goes here

            BattleEvent effectApplicationResolvedEvent = new BattleEvent
            {
                Type = BattleEventType.EffectApplicationResolved,
                Scope = BattleEventScope.Targeted,
                Source = evt.Source,
                Target = evt.Target,
                Payload = new EventPayload
                {
                    Effect = new EffectPayload
                    {
                        EffectIndex = evt.Payload.Effect.EffectIndex,
                        Strength = evt.Payload.Effect.Strength,
                        Duration = evt.Payload.Effect.Duration,
                        IsPermanent = evt.Payload.Effect.IsPermanent
                    }
                },
                StructuralData = evt.StructuralData
            };
            
            BattleEventEmitter.EmitContinuationEvent(effectApplicationResolvedEvent, ref ctx.ChainedEventQueue, in emissionContext);
        }
    }
}