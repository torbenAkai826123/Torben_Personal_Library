using UnityEngine;

namespace Torben
{
    /// <summary>
    /// DebugLogs is a MonoBehaviour that can be attached to a GameObject in Unity.
    /// It provides a structure for implementing debug logging functionality.
    /// </summary>
    public sealed class DebugLogs : ILogSink
    {
        public void Write(in LogEntry entry)
        {
            string text = entry.Category.Tag + entry.Message;
            switch (entry.Level)
            {
                case LogLevel.Warning: Debug.LogWarning(text); break;
                case LogLevel.Error: Debug.LogError(text); break;
                default: Debug.Log(text); break;
            }
        }

        // This method is called when the game starts, and it sets the Log.Sink to an instance of DebugLogs.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install() => Log.Sink = new DebugLogs();
    }

}