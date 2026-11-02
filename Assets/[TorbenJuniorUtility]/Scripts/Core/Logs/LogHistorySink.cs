
using System;

namespace Torben
{
    public sealed class LogHistorySink : ILogSink
    {
        private readonly LogEntry[] buffer;
        private readonly object gate = new object();
        private int head;      // 下一筆要寫入的位置
        private int count;
        private int version;

        public LogHistorySink(int capacity) { buffer = new LogEntry[capacity]; }

        public int Version { get { lock (gate) return version; } }

        public void Write(in LogEntry entry)
        {
            lock (gate)
            {
                buffer[head] = entry;
                head = (head + 1) % buffer.Length;
                if (count < buffer.Length) count++;
                version++;
            }
        }

        // 把最新的 n 筆依照「舊到新」的順序複製出去，回傳實際複製的筆數
        public int CopyTo(LogEntry[] destination) => CopyTo(destination, out _);

        /// <summary>同 CopyTo, 並一併取得複製當下的 Version (兩者在同一次鎖定內取得, 不會錯開).</summary>
        /// <remarks>配置: 零配置.</remarks>
        public int CopyTo(LogEntry[] destination, out int copiedVersion)
        {
            lock (gate)
            {
                copiedVersion = version;
                int n = Math.Min(count, destination.Length);
                int start = (head - n + buffer.Length) % buffer.Length;
                for (int i = 0; i < n; i++)
                    destination[i] = buffer[(start + i) % buffer.Length];
                return n;
            }
        }
    }
}