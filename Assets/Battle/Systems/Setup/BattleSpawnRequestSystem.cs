using Unity.Entities;
using Unity.Collections;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Requests;
using Archeus.Core.Debugging;

namespace Archeus.Battle.Systems.Setup
{
    [UpdateInGroup(typeof(BattleInitialisationGroup))]
    public partial struct BattleSpawnRequestSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (battleState, battle)
                    in SystemAPI.Query<RefRO<BattleState>>()
                                .WithAll<BattleTag>()
                                .WithNone<BattleSpawnRequestsIssuedTag>()
                                .WithEntityAccess())
            {
                if (battleState.ValueRO.Phase != BattlePhase.Initialising)
                    continue;

                Entity req1 = ecb.CreateEntity();

                ecb.AddComponent(req1, new SpawnCharacterRequest
                {
                    Battle = battle,
                    Slot = 1,
                    CharacterID = StableHash32.HashFromString("C1")
                });

                Entity req2 = ecb.CreateEntity();

                ecb.AddComponent(req2, new SpawnCharacterRequest
                {
                    Battle = battle,
                    Slot = 2,
                    CharacterID = StableHash32.HashFromString("C2")
                });

                ecb.AddComponent<BattleSpawnRequestsIssuedTag>(battle);
                Logging.Info(LogCategory.Setup, "Battle spawn requests issued.");
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}


