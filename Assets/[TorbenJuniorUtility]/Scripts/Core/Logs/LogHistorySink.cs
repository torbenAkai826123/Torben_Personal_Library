
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

        /// <summary>建立可保留最新 capacity 筆的歷史紀錄; capacity 小於 1 時丟出 ArgumentOutOfRangeException.</summary>
        /// <remarks>配置: 僅初始化時配置.</remarks>
        public LogHistorySink(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be at least 1.");
            buffer = new LogEntry[capacity];
        }

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

        /// <summary>清空所有紀錄. Version 會遞增, 讓讀取端知道內容已改變.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public void Clear()
        {
            lock (gate)
            {
                Array.Clear(buffer, 0, buffer.Length);  // 釋放訊息字串的參照
                head = 0;
                count = 0;
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