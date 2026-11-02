
using System;
using System.Threading;

namespace Torben
{
    public sealed class LogHistorySink : ILogSink
    {
        // 所有歷史紀錄共用的流水號. 合併多份紀錄顯示時, 依此還原寫入順序
        private static long nextSequence;

        private readonly LogEntry[] buffer;
        private readonly long[] sequences;  // 與 buffer 同位置的流水號
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
            sequences = new long[capacity];
        }

        public int Version { get { lock (gate) return version; } }

        public void Write(in LogEntry entry)
        {
            lock (gate)
            {
                buffer[head] = entry;
                sequences[head] = Interlocked.Increment(ref nextSequence);
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
        public int CopyTo(LogEntry[] destination, out int copiedVersion) => CopyTo(destination, null, out copiedVersion);

        /// <summary>
        /// 同 CopyTo(destination, out copiedVersion), 並把每筆的流水號複製到 sequenceDestination 的相同位置.
        /// 流水號由所有 LogHistorySink 共用並遞增, 可用來合併多份紀錄的先後順序. sequenceDestination 為 null 時不複製.
        /// </summary>
        /// <remarks>配置: 零配置.</remarks>
        public int CopyTo(LogEntry[] destination, long[] sequenceDestination, out int copiedVersion)
        {
            lock (gate)
            {
                copiedVersion = version;
                int n = Math.Min(count, destination.Length);
                if (sequenceDestination != null) n = Math.Min(n, sequenceDestination.Length);
                int start = (head - n + buffer.Length) % buffer.Length;
                for (int i = 0; i < n; i++)
                {
                    int index = (start + i) % buffer.Length;
                    destination[i] = buffer[index];
                    if (sequenceDestination != null) sequenceDestination[i] = sequences[index];
                }
                return n;
            }
        }
    }
}