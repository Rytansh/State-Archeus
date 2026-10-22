using System;
using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Systems.Setup;
using Archeus.Content.Blobs;
using Archeus.Content.Lookup;
using Archeus.Content.Registries;
using Archeus.Core.Debugging;
using Archeus.Game.Bootstrap;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Testing.Battle.Setup
{
    /// <summary>
    /// Minimal REAL five-system Edit Mode fixture. Mirrors the passing roster
    /// tests, but starts with StartBattleRequest instead of precreating the battle.
    /// Keep this fixture in the same Edit Mode test assembly as the two test suites.
    /// </summary>
    internal sealed class BattleSetupTestFixture : IDisposable
    {
        public const ulong PlayerAID = 1001;
        public const ulong PlayerBID = 1002;
        public const uint LeadAID = 1;
        public const uint LeadBID = 2;
        public const uint DeckCharacterAID = 3;
        public const uint DeckCharacterBID = 4;
        public const uint SkillAID = 101;
        public const uint SkillBID = 102;
        public const uint FirstRuntimeID = 100;

        public readonly World World;
        public readonly EntityManager Entities;
        private readonly SystemHandle creation;
        private readonly SystemHandle roster;
        private readonly SystemHandle readiness;
        private readonly SystemHandle start;
        private readonly SystemHandle phaseTransition;
        private BlobAssetReference<ContentBlobRegistry> content;
        private ContentLookupTables lookup;

        internal struct Scenario
        {
            public Entity Request;
            public Entity Battle;
            public Entity PlayerA;
            public Entity PlayerB;
        }

        public BattleSetupTestFixture()
        {
            World = new World("Full Battle Setup Edit Mode Tests");
            Entities = World.EntityManager;
            creation = World.CreateSystem<BattleCreationSystem>();
            roster = World.CreateSystem<RosterInitialisationSystem>();
            readiness = World.CreateSystem<BattleReadinessSystem>();
            start = World.CreateSystem<BattleStartSystem>();
            phaseTransition = World.CreateSystem<BattlePhaseTransitionSystem>();

            CreateContent();
            Logging.EnableCategory(LogCategory.Setup);
            Logging.SetMinimumLevel(LogLevel.Info);
        }

        public void Dispose()
        {
            World.Dispose();
            lookup.Dispose();
            if (content.IsCreated)
                content.Dispose();
        }

        public void RunCreation() => creation.Update(World.Unmanaged);

        public void RunRoster() => roster.Update(World.Unmanaged);

        public void RunReadiness() => readiness.Update(World.Unmanaged);

        public void RunStart() => start.Update(World.Unmanaged);

        public void RunPhaseTransition() => phaseTransition.Update(World.Unmanaged);

        public Scenario CreateRequest(
            ulong battleID = 42,
            ulong seed = 12345678,
            bool reversePlayerEntries = false
        )
        {
            Entity request = Entities.CreateEntity();
            Entities.AddComponentData(
                request,
                new StartBattleRequest
                {
                    BattleID = battleID,
                    BattleSeed = seed,
                    BattleConfigID = 0,
                }
            );
            Entities.AddBuffer<BattlePlayerEntry>(request);
            Entities.AddBuffer<PlayerDeckEntry>(request);

            // Fresh buffer handles after all structural changes.
            var players = Entities.GetBuffer<BattlePlayerEntry>(request);
            var a = new BattlePlayerEntry
            {
                PlayerID = PlayerAID,
                LeadCharacterDefinitionID = LeadAID,
            };
            var b = new BattlePlayerEntry
            {
                PlayerID = PlayerBID,
                LeadCharacterDefinitionID = LeadBID,
            };
            if (reversePlayerEntries)
            {
                players.Add(b);
                players.Add(a);
            }
            else
            {
                players.Add(a);
                players.Add(b);
            }

            var deck = Entities.GetBuffer<PlayerDeckEntry>(request);
            deck.Add(
                new PlayerDeckEntry
                {
                    PlayerID = PlayerAID,
                    CardDefinitionID = DeckCharacterAID,
                    CardType = RuntimeCardType.Character,
                }
            );
            deck.Add(
                new PlayerDeckEntry
                {
                    PlayerID = PlayerAID,
                    CardDefinitionID = SkillAID,
                    CardType = RuntimeCardType.Skill,
                }
            );
            deck.Add(
                new PlayerDeckEntry
                {
                    PlayerID = PlayerBID,
                    CardDefinitionID = DeckCharacterBID,
                    CardType = RuntimeCardType.Character,
                }
            );
            deck.Add(
                new PlayerDeckEntry
                {
                    PlayerID = PlayerBID,
                    CardDefinitionID = SkillBID,
                    CardType = RuntimeCardType.Skill,
                }
            );

            return new Scenario { Request = request };
        }

        public Scenario AfterCreation(Scenario s)
        {
            Assert.That(
                Entities.HasComponent<InitialRosterConstructionRequest>(s.Request),
                Is.True,
                "BattleCreationSystem did not hand off to roster construction."
            );
            s.Battle = Entities
                .GetComponentData<InitialRosterConstructionRequest>(s.Request)
                .Battle;
            var refs = Entities.GetBuffer<BattlePlayerReference>(s.Battle);
            foreach (BattlePlayerReference entry in refs)
            {
                ulong id = Entities.GetComponentData<BattlePlayerIdentity>(entry.Player).PlayerID;
                if (id == PlayerAID)
                    s.PlayerA = entry.Player;
                if (id == PlayerBID)
                    s.PlayerB = entry.Player;
            }
            Assert.That(s.PlayerA, Is.Not.EqualTo(Entity.Null));
            Assert.That(s.PlayerB, Is.Not.EqualTo(Entity.Null));
            return s;
        }

        public Scenario ReadyToStart(
            ulong battleID = 42,
            ulong seed = 12345678,
            bool reversePlayerEntries = false
        )
        {
            Scenario s = CreateRequest(battleID, seed, reversePlayerEntries);
            RunCreation();
            s = AfterCreation(s);
            RunRoster();
            Assert.That(Entities.HasComponent<RosterReadyRequest>(s.Request), Is.True);
            RunReadiness();
            Assert.That(Entities.HasComponent<BattleStartRequest>(s.Request), Is.True);
            return s;
        }

        public Entity Lead(Entity player) => Entities.GetComponentData<LeadCharacter>(player).Value;

        public Entity DeckCharacter(Entity player) => Entities.GetBuffer<DeckCard>(player)[0].Card;

        public Entity DeckSkill(Entity player) => Entities.GetBuffer<DeckCard>(player)[1].Card;

        public void SetInitiative(Entity character, float value)
        {
            CharacterRuntimeStatBaseline stats =
                Entities.GetComponentData<CharacterRuntimeStatBaseline>(character);
            stats.Initiative = value;
            Entities.SetComponentData(character, stats);
        }

        public void SetInitiatives(
            Scenario s,
            float leadA,
            float characterA,
            float leadB,
            float characterB
        )
        {
            SetInitiative(Lead(s.PlayerA), leadA);
            SetInitiative(DeckCharacter(s.PlayerA), characterA);
            SetInitiative(Lead(s.PlayerB), leadB);
            SetInitiative(DeckCharacter(s.PlayerB), characterB);
        }

        public ulong ActivePlayerID(Scenario s)
        {
            Entity player = Entities.GetComponentData<ActiveTurnPlayer>(s.Battle).Player;
            return Entities.GetComponentData<BattlePlayerIdentity>(player).PlayerID;
        }

        public int Count<T>()
            where T : unmanaged, IComponentData
        {
            using var query = Entities.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        private void CreateContent()
        {
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref ContentBlobRegistry root = ref builder.ConstructRoot<ContentBlobRegistry>();
                var characters = builder.Allocate(ref root.Characters, 4);
                int[] initiative = { 100, 50, 250, 150 };
                for (int i = 0; i < 4; ++i)
                {
                    ref CharacterDefinitionBlob character = ref characters[i];
                    character.ID = (uint)(i + 1);
                    character.CharacterBlobBaseStats = new CharacterBlobBaseStats
                    {
                        Attack = 101 + i,
                        Defense = 201 + i,
                        MaxHealth = 1001 + i,
                        CritRATE = 10 + i,
                        CritDMG = 25 + i,
                        Initiative = initiative[i],
                    };
                    builder.Allocate(ref character.BehaviourIndices, 0);
                }

                var skills = builder.Allocate(ref root.Skills, 2);
                for (int i = 0; i < 2; ++i)
                {
                    ref SkillDefinitionBlob skill = ref skills[i];
                    skill.ID = (uint)(101 + i);
                    skill.SkillBlobBaseStats = new SkillBlobBaseStats
                    {
                        Attack = 301 + i,
                        Defense = 401 + i,
                        Health = 501 + i,
                    };
                    builder.Allocate(ref skill.BehaviourIndices, 0);
                }

                builder.Allocate(ref root.Behaviours, 0);
                builder.Allocate(ref root.AbilityPrograms, 0);
                builder.Allocate(ref root.Effects, 0);
                content = builder.CreateBlobAssetReference<ContentBlobRegistry>(
                    Allocator.Persistent
                );
            }

            lookup = new ContentLookupTables
            {
                CharacterIDToIndex = new NativeHashMap<uint, int>(4, Allocator.Persistent),
                SkillIDToIndex = new NativeHashMap<uint, int>(2, Allocator.Persistent),
                BehaviourIDToIndex = new NativeHashMap<uint, int>(1, Allocator.Persistent),
                EffectIDToIndex = new NativeHashMap<uint, int>(1, Allocator.Persistent),
            };
            for (int i = 0; i < 4; ++i)
                Assert.That(lookup.CharacterIDToIndex.TryAdd((uint)(i + 1), i), Is.True);
            for (int i = 0; i < 2; ++i)
                Assert.That(lookup.SkillIDToIndex.TryAdd((uint)(101 + i), i), Is.True);

            // These components are world-level singletons in the actual runtime.
            Entity singleton = Entities.CreateEntity();
            Entities.AddComponentData(singleton, lookup);
            Entities.AddComponentData(
                singleton,
                new ContentBlobRegistryComponent { BlobRegistryReference = content }
            );
            Entities.AddComponent<BattleContentReadyTag>(singleton);
        }
    }
}
