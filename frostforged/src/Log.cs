using UnityEngine;

namespace FrostforgedCrescent
{
    internal static class Log
    {
        private const string Tag = "[FrostforgedCrescent] ";

        public static void Info(string message) { Debug.Log(Tag + message); }
        public static void Warn(string message) { Debug.LogWarning(Tag + message); }
        public static void Error(string message) { Debug.LogError(Tag + message); }
    }
}
