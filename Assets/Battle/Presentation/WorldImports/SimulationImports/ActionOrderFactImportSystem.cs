using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.Presentation.State;
using Unity.Entities;

namespace Archeus.Battle.Systems.Presentation
{
    public static class ActionOrderMirrorAccess
    {
        public static ActionOrderMirror Get(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ActionOrderMirror>());
            if (!query.IsEmptyIgnoreFilter)
                return em.GetComponentObject<ActionOrderMirror>(query.GetSingletonEntity());
            Entity entity = em.CreateEntity();
            var mirror = new ActionOrderMirror();
            em.AddComponentObject(entity, mirror);
            return mirror;
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattlePresentationGroup))]
    [UpdateAfter(typeof(PresentationUpdateImportSystem))]
    [UpdateBefore(typeof(BattlePresentationAssemblySystem))]
    public partial class ActionOrderFactImportSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Get/create the managed mirror before acquiring any live buffer handles.
            var mirror = ActionOrderMirrorAccess.Get(EntityManager);
            using var query = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BattlePresentationInboxTag>(),
                ComponentType.ReadWrite<PresentationFact>()
            );
            if (query.IsEmptyIgnoreFilter)
                return;
            var facts = EntityManager.GetBuffer<PresentationFact>(query.GetSingletonEntity());
            for (int i = 0; i < facts.Length; )
            {
                var fact = facts[i];
                if (fact.FactType != PresentationFactType.SchedulerTransition)
                {
                    i++;
                    continue;
                }
                if (
                    mirror.State == null
                    || mirror.State.BattleID == fact.FactMetadata.BattleRuntimeID
                )
                    mirror.Record(fact.FactPayload.Scheduler);
                facts.RemoveAt(i);
            }
        }
    }
}
