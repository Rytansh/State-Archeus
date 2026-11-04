using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Presentation;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Snapshots
{
    /// <summary>
    /// In-memory, battle-scoped value snapshot. Surviving Entity handles are retained. Destroyed
    /// members are recreated and EVERY captured Entity field is remapped, including buffer entries.
    /// Runtime BattleID, PlayerID and CardRuntimeID remain stable; external code must use those IDs,
    /// not cache raw Entity handles across a revert. Content blobs must outlive the snapshot.
    /// </summary>
    public sealed class BattleStateSnapshot
    {
        private sealed class Record
        {
            public Entity Original;
            public readonly List<ISnapshotComponent> Components = new();
        }

        private readonly List<Record> records = new();
        private readonly Dictionary<Entity, Entity> restoredEntities = new();
        private readonly Entity battle;
        public int EntityCount => records.Count;
        public string BaselineHash { get; private set; }

        private BattleStateSnapshot(Entity battle) => this.battle = battle;

        public static bool IsOperational(Type type) =>
            type == typeof(StageSnapshotStatus)
            || type == typeof(PresentationFact)
            || type == typeof(PresentationStateRevision)
            || type == typeof(PresentationSequenceCounter)
            || type == typeof(InitialPresentationStatePublishedTag);

        public static BattleStateSnapshot Capture(EntityManager entities, Entity battle)
        {
            entities.CompleteAllTrackedJobs();
            if (!entities.Exists(battle))
                throw new ArgumentException("Battle does not exist.", nameof(battle));
            SnapshotAotCatalog.Register();
            var snapshot = new BattleStateSnapshot(battle);
            List<Entity> members = FindMembers(entities, battle);
            var included = new HashSet<Entity>(members);
            // Follow all outgoing gameplay references. OwnedBattle is required on spawned runtime
            // members, so newly created members remain discoverable even after leaving a zone.
            for (int i = 0; i < members.Count; i++)
            {
                Entity member = members[i];
                Record record = CaptureRecord(entities, member);
                snapshot.records.Add(record);
                snapshot.restoredEntities.Add(member, member);
                foreach (ISnapshotComponent component in record.Components)
                    component.VisitEntities(reference =>
                    {
                        if (reference == Entity.Null || included.Contains(reference))
                            return;
                        if (!entities.Exists(reference))
                            throw new InvalidOperationException(
                                $"Stale Entity reference in {component.ComponentType}."
                            );
                        if (reference != battle && !entities.HasComponent<OwnedBattle>(reference))
                            throw new InvalidOperationException(
                                "Referenced mutable entities must declare OwnedBattle; shared external entities require an explicit policy."
                            );
                        if (
                            entities.HasComponent<OwnedBattle>(reference)
                            && entities.GetComponentData<OwnedBattle>(reference).Battle != battle
                        )
                            throw new InvalidOperationException(
                                "A mutable snapshot cannot span different battles."
                            );
                        if (entities.HasComponent<BattleID>(reference) && reference != battle)
                            throw new InvalidOperationException(
                                "A mutable snapshot cannot reference another battle."
                            );
                        included.Add(reference);
                        members.Add(reference);
                    });
            }
            snapshot.BaselineHash = snapshot.HashRecords(
                snapshot.records,
                snapshot.OriginalIdentities()
            );
            return snapshot;
        }

        private static Record CaptureRecord(EntityManager entities, Entity entity)
        {
            var record = new Record { Original = entity };
            using NativeArray<ComponentType> types = entities.GetComponentTypes(entity);
            foreach (ComponentType component in types)
            {
                if (
                    component.GetManagedType() == typeof(Entity)
                    || IsOperational(component.GetManagedType())
                )
                    continue;
                record.Components.Add(SnapshotComponents.Capture(entities, entity, component));
            }
            record.Components.Sort(
                (a, b) =>
                    StringComparer.Ordinal.Compare(
                        a.ComponentType.GetManagedType().FullName,
                        b.ComponentType.GetManagedType().FullName
                    )
            );
            return record;
        }

        private static List<Entity> FindMembers(EntityManager entities, Entity battle)
        {
            var result = new List<Entity> { battle };
            using var query = entities.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<OwnedBattle>() },
                    Options =
                        EntityQueryOptions.IncludeDisabledEntities
                        | EntityQueryOptions.IncludePrefab
                        | EntityQueryOptions.IgnoreComponentEnabledState,
                }
            );
            using NativeArray<Entity> candidates = query.ToEntityArray(Allocator.Temp);
            foreach (Entity candidate in candidates)
                if (
                    candidate != battle
                    && entities.GetComponentData<OwnedBattle>(candidate).Battle == battle
                )
                    result.Add(candidate);
            result.Sort(
                1,
                result.Count - 1,
                Comparer<Entity>.Create(
                    (a, b) =>
                    {
                        int index = a.Index.CompareTo(b.Index);
                        return index != 0 ? index : a.Version.CompareTo(b.Version);
                    }
                )
            );
            return result;
        }

        public Entity ResolveRestoredEntity(Entity original) => restoredEntities[original];

        public void Restore(EntityManager entities)
        {
            entities.CompleteAllTrackedJobs();
            if (!entities.Exists(battle))
                throw new InvalidOperationException("The battle itself has ended.");
            List<Entity> current = FindMembers(entities, battle);
            var discovered = new HashSet<Entity>(current);
            foreach (Entity previous in restoredEntities.Values)
                if (entities.Exists(previous) && discovered.Add(previous))
                    current.Add(previous);
            // Reject unsupported additions BEFORE performing structural changes. This avoids
            // pretending that unmanaged copies can roll back native allocations or managed objects.
            for (int i = 0; i < current.Count; i++)
            {
                Record record = CaptureRecord(entities, current[i]);
                foreach (ISnapshotComponent value in record.Components)
                    value.VisitEntities(reference =>
                    {
                        // Provisional references to destroyed objects are expected; baseline data
                        // replaces them. Never absorb another battle through an abandoned reference.
                        if (
                            reference == Entity.Null
                            || !entities.Exists(reference)
                            || discovered.Contains(reference)
                        )
                            return;
                        if (reference != battle && !entities.HasComponent<OwnedBattle>(reference))
                            throw new InvalidOperationException(
                                "A provisional reference escaped battle ownership; restore cannot safely own its side effects."
                            );
                        if (
                            entities.HasComponent<OwnedBattle>(reference)
                            && entities.GetComponentData<OwnedBattle>(reference).Battle != battle
                        )
                            throw new InvalidOperationException(
                                "A provisional reference escaped into another battle."
                            );
                        if (entities.HasComponent<BattleID>(reference) && reference != battle)
                            throw new InvalidOperationException(
                                "A provisional reference escaped into another battle."
                            );
                        discovered.Add(reference);
                        current.Add(reference);
                    });
            }

            var nextMap = new Dictionary<Entity, Entity>();
            foreach (Record record in records)
            {
                Entity previous = restoredEntities[record.Original];
                nextMap.Add(
                    record.Original,
                    entities.Exists(previous) ? previous : entities.CreateEntity()
                );
            }
            var keep = new HashSet<Entity>(nextMap.Values);
            foreach (Entity entity in current)
                if (!keep.Contains(entity))
                    entities.DestroyEntity(entity);

            // Establish every destination archetype first; no old DynamicBuffer or RefRW handle
            // survives a structural change. Data restoration itself performs no structural work.
            foreach (Record record in records)
            {
                Entity target = nextMap[record.Original];
                var expected = new HashSet<Type>();
                foreach (ISnapshotComponent value in record.Components)
                    expected.Add(value.ComponentType.GetManagedType());
                using NativeArray<ComponentType> actual = entities.GetComponentTypes(target);
                foreach (ComponentType component in actual)
                {
                    Type type = component.GetManagedType();
                    if (type != typeof(Entity) && !IsOperational(type) && !expected.Contains(type))
                        entities.RemoveComponent(target, component);
                }
                foreach (ISnapshotComponent value in record.Components)
                    if (!entities.HasComponent(target, value.ComponentType))
                        entities.AddComponent(target, value.ComponentType);
            }
            foreach (Record record in records)
            foreach (ISnapshotComponent value in record.Components)
                value.Restore(entities, nextMap[record.Original], nextMap);
            restoredEntities.Clear();
            foreach (var pair in nextMap)
                restoredEntities.Add(pair.Key, pair.Value);
        }

        /// <summary>Diagnostic full value/topology digest in this baseline's logical identity space.</summary>
        public string ComputeCurrentHash(EntityManager entities)
        {
            var current = new List<Record>();
            var identities = new Dictionary<Entity, int>();
            for (int i = 0; i < records.Count; i++)
            {
                Entity target = restoredEntities[records[i].Original];
                if (!entities.Exists(target))
                    return "missing-entity";
                current.Add(CaptureRecord(entities, target));
                identities.Add(target, i);
            }
            foreach (Entity member in FindMembers(entities, battle))
                if (!identities.ContainsKey(member))
                    return "additional-entity";
            return HashRecords(current, identities);
        }

        private Dictionary<Entity, int> OriginalIdentities()
        {
            var result = new Dictionary<Entity, int>();
            for (int i = 0; i < records.Count; i++)
                result.Add(records[i].Original, i);
            return result;
        }

        private string HashRecords(List<Record> values, IReadOnlyDictionary<Entity, int> identities)
        {
            var text = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                text.Append(i).Append('{');
                foreach (ISnapshotComponent component in values[i].Components)
                {
                    text.Append(component.ComponentType.GetManagedType().FullName).Append(':');
                    component.Append(text, identities);
                    text.Append('\n');
                }
                text.Append('}');
            }
            using SHA256 hash = SHA256.Create();
            return BitConverter
                .ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())))
                .Replace("-", "");
        }
    }
}
