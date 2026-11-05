using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Unity.Entities;

namespace Archeus.Battle.Snapshots
{
    /// <summary>
    /// Cached value-type traversal, not a Unity object serializer. Native allocations and managed
    /// references fail closed. Blobs are immutable, externally owned content, not mutable state.
    /// </summary>
    internal static class SnapshotValue
    {
        private static readonly Dictionary<Type, FieldInfo[]> fields = new();

        private static bool IsBlob(Type type) =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BlobAssetReference<>);

        private static bool IsScalar(Type type) =>
            type.IsPrimitive || type.IsEnum || type == typeof(decimal);

        private static bool IsFixedList(Type type) =>
            type.IsGenericType
            && type.Namespace == "Unity.Collections"
            && type.Name.StartsWith("FixedList", StringComparison.Ordinal);

        private static bool IsFixedString(Type type) =>
            type.Namespace == "Unity.Collections"
            && type.Name.StartsWith("FixedString", StringComparison.Ordinal);

        private static FieldInfo[] Fields(Type type)
        {
            if (!fields.TryGetValue(type, out FieldInfo[] result))
            {
                result = type.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );
                Array.Sort(result, (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
                fields.Add(type, result);
            }
            return result;
        }

        public static void Validate(Type type)
        {
            if (type.IsPointer || type == typeof(IntPtr) || type == typeof(UIntPtr))
                throw new NotSupportedException($"Snapshot cannot own native memory: {type}.");
            if (type == typeof(Entity) || IsScalar(type) || IsBlob(type))
                return;
            if (IsFixedString(type))
                return;
            if (IsFixedList(type))
            {
                Type item = type.GetGenericArguments()[0];
                Validate(item);
                if (ContainsEntity(item))
                    throw new NotSupportedException(
                        "Entity-valued fixed lists require an explicit remapping codec."
                    );
                return;
            }
            if (
                !type.IsValueType
                || type.IsPointer
                || type == typeof(IntPtr)
                || type == typeof(UIntPtr)
            )
                throw new NotSupportedException(
                    $"Snapshot cannot own mutable reference or native memory: {type}."
                );
            foreach (FieldInfo field in Fields(type))
                Validate(field.FieldType);
        }

        private static bool ContainsEntity(Type type)
        {
            if (type == typeof(Entity))
                return true;
            if (IsScalar(type) || IsBlob(type))
                return false;
            foreach (FieldInfo field in Fields(type))
                if (ContainsEntity(field.FieldType))
                    return true;
            return false;
        }

        public static void VisitEntities(object value, Action<Entity> visit)
        {
            Type type = value.GetType();
            if (type == typeof(Entity))
            {
                visit((Entity)value);
                return;
            }
            if (IsScalar(type) || IsBlob(type) || IsFixedList(type) || IsFixedString(type))
                return;
            foreach (FieldInfo field in Fields(type))
                VisitEntities(field.GetValue(value), visit);
        }

        public static object Remap(object value, IReadOnlyDictionary<Entity, Entity> map)
        {
            Type type = value.GetType();
            if (type == typeof(Entity))
            {
                Entity entity = (Entity)value;
                return entity == Entity.Null ? entity : map[entity];
            }
            if (IsScalar(type) || IsBlob(type) || IsFixedList(type) || IsFixedString(type))
                return value;
            // Boxing a value type makes a new copy; never patch the stored baseline in place.
            object copy = System.Runtime.CompilerServices.RuntimeHelpers.GetObjectValue(value);
            foreach (FieldInfo field in Fields(type))
                field.SetValue(copy, Remap(field.GetValue(value), map));
            return copy;
        }

        public static void Append(
            StringBuilder text,
            object value,
            IReadOnlyDictionary<Entity, int> identities
        )
        {
            Type type = value.GetType();
            if (type == typeof(Entity))
            {
                Entity entity = (Entity)value;
                text.Append(
                    entity == Entity.Null ? "null"
                    : identities.TryGetValue(entity, out int id)
                        ? id.ToString(CultureInfo.InvariantCulture)
                    : "external:" + entity.Index + ":" + entity.Version
                );
                return;
            }
            if (IsBlob(type))
            {
                // The content blob stays alive for the battle lifetime. Identity suffices for this
                // in-process diagnostic digest; this is intentionally not a portable save format.
                text.Append(value.GetHashCode().ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (IsScalar(type))
            {
                text.Append(
                    value is IFormattable formatted
                        ? formatted.ToString(
                            value is float || value is double ? "R" : null,
                            CultureInfo.InvariantCulture
                        )
                        : value.ToString()
                );
                return;
            }
            if (IsFixedString(type))
            {
                string content = value.ToString();
                text.Append(content.Length).Append(':').Append(content);
                return;
            }
            if (IsFixedList(type))
            {
                text.Append('[');
                foreach (object item in (IEnumerable)value)
                {
                    Append(text, item, identities);
                    text.Append('|');
                }
                text.Append(']');
                return;
            }
            foreach (FieldInfo field in Fields(type))
            {
                text.Append(field.Name).Append('=');
                Append(text, field.GetValue(value), identities);
                text.Append(';');
            }
        }
    }
}
