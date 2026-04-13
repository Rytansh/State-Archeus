using System.Collections.Generic;
using UnityEngine;

namespace Archeus.Core.Debugging
{
    public static class Logging
    {

        // ================================
        // CONFIGURATION
        // ================================

        private static readonly HashSet<LogCategory> enabledCategories = new()
        {
            LogCategory.Testing,
            LogCategory.System,
            LogCategory.Simulation,
            LogCategory.Presentation,
            LogCategory.Event,
            LogCategory.Combat,
            LogCategory.Setup,
            LogCategory.VM
        };

        private static LogLevel minimumLevel = LogLevel.Info;

        private static readonly Dictionary<LogCategory, string> categoryColors = new()
        {
            { LogCategory.Testing, "#FFFFFF" },
            { LogCategory.System, "#8A2BE2" },
            { LogCategory.Simulation, "#1E90FF" },
            { LogCategory.Presentation, "#32CD32" },
            { LogCategory.Event, "#FFA500" },
            { LogCategory.VM, "#FF69B4" },
            { LogCategory.Combat, "#FF4444" },
            { LogCategory.Setup, "#00CED1" }
        };

        // ================================
        // PUBLIC API
        // ================================

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Log(LogCategory category, LogLevel level, string message)
        {
            if (!IsEnabled(category, level))
                return;

            string formatted = Format(category, message);

            switch (level)
            {
                case LogLevel.Info:
                    Debug.Log(formatted);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(formatted);
                    break;
                case LogLevel.Error:
                    Debug.LogError(formatted);
                    break;
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Info(LogCategory category, string message)
        {
            Log(category, LogLevel.Info, message);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Warn(LogCategory category, string message)
        {
            Log(category, LogLevel.Warning, message);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Error(LogCategory category, string message)
        {
            Log(category, LogLevel.Error, message);
        }

        // ================================
        // FILTERING
        // ================================

        public static void EnableCategory(LogCategory category)
        {
            enabledCategories.Add(category);
        }

        public static void DisableCategory(LogCategory category)
        {
            enabledCategories.Remove(category);
        }

        public static void SetMinimumLevel(LogLevel level)
        {
            minimumLevel = level;
        }

        private static bool IsEnabled(LogCategory category, LogLevel level)
        {
            if (level < minimumLevel)
                return false;

            if (!enabledCategories.Contains(category))
                return false;

            return true;
        }

        private static string Format(LogCategory category, string message)
        {
            string color = categoryColors.TryGetValue(category, out var c) ? c : "#FFFFFF";
            return $"<color={color}>[{category}]</color> {message}";
        }
    }

    public enum LogCategory
    {
        Testing,
        System,
        Simulation,
        Presentation,
        Event,
        VM,
        Combat,
        Setup
    }

    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

}