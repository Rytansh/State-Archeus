using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;

namespace Archeus.Battle.Snapshots
{
    internal interface ISnapshotComponent
    {
        ComponentType ComponentType { get; }
        void VisitEntities(Action<Entity> visit);
        void Restore(
            EntityManager entities,
            Entity entity,
            IReadOnlyDictionary<Entity, Entity> map
        );
        void Append(StringBuilder text, IReadOnlyDictionary<Entity, int> identities);
    }

    internal static class SnapshotComponents
    {
        private static readonly Dictionary<
            Type,
            Func<EntityManager, Entity, ISnapshotComponent>
        > factories = new();

        public static ISnapshotComponent Capture(
            EntityManager entities,
            Entity entity,
            ComponentType component
        )
        {
            Type type = component.GetManagedType();
            if (
                component.IsManagedComponent
                || component.IsSharedComponent
                || component.IsCleanupComponent
                || component.IsCleanupSharedComponent
                || component.IsCleanupBufferComponent
            )
                throw new NotSupportedException(
                    $"Snapshot requires an explicit ownership policy for {type}."
                );
            SnapshotValue.Validate(type);
            if (!factories.TryGetValue(type, out var factory))
            {
                Type generic = component.IsBuffer
                    ? typeof(BufferSnapshot<>)
                    : typeof(ComponentSnapshot<>);
                Type closed = generic.MakeGenericType(type);
                factory = (manager, target) =>
                    (ISnapshotComponent)Activator.CreateInstance(closed, manager, target);
                factories.Add(type, factory);
            }
            return factory(entities, entity);
        }

        // Explicit registration also roots the closed codecs for an IL2CPP build. New runtime
        // state is discovered automatically in the Editor; the AOT catalog audit prevents omissions.
        public static void RegisterComponent<T>()
            where T : unmanaged, IComponentData =>
            factories[typeof(T)] = (manager, entity) => new ComponentSnapshot<T>(manager, entity);

        public static void RegisterBuffer<T>()
            where T : unmanaged, IBufferElementData =>
            factories[typeof(T)] = (manager, entity) => new BufferSnapshot<T>(manager, entity);
    }

    [Preserve]
    internal sealed class ComponentSnapshot<T> : ISnapshotComponent
        where T : unmanaged, IComponentData
    {
        private readonly T value;
        private readonly bool enabled;
        public ComponentType ComponentType => Unity.Entities.ComponentType.ReadWrite<T>();

        [Preserve]
        public ComponentSnapshot(EntityManager entities, Entity entity)
        {
            value = ComponentType.IsZeroSized ? default : entities.GetComponentData<T>(entity);
            enabled =
                !ComponentType.IsEnableable || entities.IsComponentEnabled(entity, ComponentType);
        }

        public void VisitEntities(Action<Entity> visit) =>
            SnapshotValue.VisitEntities(value, visit);

        public void Restore(
            EntityManager entities,
            Entity entity,
            IReadOnlyDictionary<Entity, Entity> map
        )
        {
            if (!ComponentType.IsZeroSized)
                entities.SetComponentData(entity, (T)SnapshotValue.Remap(value, map));
            if (ComponentType.IsEnableable)
                entities.SetComponentEnabled(entity, ComponentType, enabled);
        }

        public void Append(StringBuilder text, IReadOnlyDictionary<Entity, int> identities)
        {
            text.Append(enabled).Append(':');
            SnapshotValue.Append(text, value, identities);
        }
    }

    [Preserve]
    internal sealed class BufferSnapshot<T> : ISnapshotComponent
        where T : unmanaged, IBufferElementData
    {
        private readonly T[] values;
        private readonly bool enabled;
        public ComponentType ComponentType => Unity.Entities.ComponentType.ReadWrite<T>();

        [Preserve]
        public BufferSnapshot(EntityManager entities, Entity entity)
        {
            using NativeArray<T> copy = entities.GetBuffer<T>(entity).ToNativeArray(Allocator.Temp);
            values = copy.ToArray();
            enabled =
                !ComponentType.IsEnableable || entities.IsComponentEnabled(entity, ComponentType);
        }

        public void VisitEntities(Action<Entity> visit)
        {
            foreach (T value in values)
                SnapshotValue.VisitEntities(value, visit);
        }

        public void Restore(
            EntityManager entities,
            Entity entity,
            IReadOnlyDictionary<Entity, Entity> map
        )
        {
            DynamicBuffer<T> buffer = entities.GetBuffer<T>(entity);
            buffer.Clear();
            foreach (T value in values)
                buffer.Add((T)SnapshotValue.Remap(value, map));
            if (ComponentType.IsEnableable)
                entities.SetComponentEnabled(entity, ComponentType, enabled);
        }

        public void Append(StringBuilder text, IReadOnlyDictionary<Entity, int> identities)
        {
            text.Append(enabled).Append(':').Append(values.Length).Append('[');
            foreach (T value in values)
            {
                SnapshotValue.Append(text, value, identities);
                text.Append('|');
            }
            text.Append(']');
        }
    }
}
