using Archeus.Battle.Buffers.Presentation;
using Unity.Entities;

namespace Archeus.Game.Bootstrap
{
    public sealed class BattlePresentationWorld
    {
        public World EcsWorld { get; }

        public BattlePresentationWorld(World ecsWorld)
        {
            EcsWorld = ecsWorld;
            // Create inbox system
            EntityManager em = ecsWorld.EntityManager;
            Entity inbox = em.CreateEntity(typeof(BattlePresentationInboxTag));
            em.SetName(inbox, "Presentation Fact Inbox");
            em.AddBuffer<PresentationFact>(inbox);
        }
    }
}
