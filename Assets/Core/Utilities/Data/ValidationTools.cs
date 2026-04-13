using System;
using System.Collections.Generic;

public static class ValidationTools
{
    // Validation Functions //
    public static void Require(ValidationContext validator, bool condition, string message) {if (!condition) {validator.Error(message);}}
    public static void RequireNotNull(ValidationContext validator, object obj, string fieldName) {if (obj == null) {validator.Error($"Field {fieldName} is null.");}}
    public static void RequireNotNullOrEmpty(ValidationContext validator, string value, string fieldName) {if (string.IsNullOrWhiteSpace(value)) {validator.Error($"Field {fieldName} is null or empty.");}}

    public static void RequireInRange(ValidationContext validator, float value, float min, float max, string fieldName) {if (value < min || value > max) {validator.Warning($"Field {fieldName} is out of acceptable bounds.");}}
    public static void RequirePositive(ValidationContext validator, float value, string fieldName) { if (value <= 0) {validator.Warning($"Field {fieldName} requires a positive value but has value {value}.");} }
    public static void RequireNonNegative(ValidationContext validator, float value, string fieldName) { if (value < 0) {validator.Warning($"Field {fieldName} requires a non-negative value but has value {value}.");} }

    public static void RequireEnumDefined<TEnum>(ValidationContext validator, TEnum value, string fieldName) where TEnum : Enum {if (!Enum.IsDefined(typeof(TEnum), value)) {validator.Error($"Field {fieldName} has invalid enum value {value}.");}}

    public static void RequireReferenceExists(ValidationContext validator, object reference, string fieldName) { if (reference == null) {validator.Error($"Field {fieldName} has a nonexistent reference.");}}

    public static void RequireNoNullElements<T>(ValidationContext validator, IEnumerable<T> collection, string fieldName)
    {
        if (collection == null)
        {
            validator.Error($"Collection {fieldName} is null.");
            return;
        }

        foreach (var item in collection)
        {
            if (item == null)
            {
                validator.Error($"Collection {fieldName} contains a null element.");
                return;
            }
        }
    }

    public static void RequireNoDuplicates<T>(ValidationContext validator, IEnumerable<T> collection, string fieldName)
    {
        if (collection == null) return;

        HashSet<T> set = new HashSet<T>();
        foreach (var item in collection)
        {
            if (!set.Add(item))
            {
                validator.Warning($"Collection {fieldName} contains duplicate value {item}.");
                return;
            }
        }
    }

    public static void ValidateEach<T>(ValidationContext validator, IEnumerable<T> collection, Action<T> validate)
    {
        if (collection == null) return;
        if (validate == null) return;

        foreach (var item in collection)
        {
            validate(item);
        }
    }

}
