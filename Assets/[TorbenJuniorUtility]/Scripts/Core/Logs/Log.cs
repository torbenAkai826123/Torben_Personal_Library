using System.Runtime.CompilerServices;

namespace Torben
{
    // Call Log facade.
    public static class Log
    {
        // LOG 的全體總開關，只要關閉就會被擋住所有訊息。（預設關閉）
        public static bool isPrintLog = false; // Flag to control whether logs should be printed to the console.
        // 針對 Debug 等級的訊息，額外的開關，總開關開啟時才會生效。（預設關閉）
        public static bool isDebugLog = false; // Flag to control whether debug logs should be printed to the console.


        // 接收訊息的 sink 清單. 新增 / 移除時複製新陣列再整個替換 (copy-on-write), 寫入時走訪不需加鎖
        private static readonly object sinkGate = new object();
        private static readonly ILogSink[] emptySinks = new ILogSink[0];
        private static volatile ILogSink[] sinks = emptySinks;

        /// <summary>目前是否至少有一個 sink.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static bool HasSink => sinks.Length > 0;

        /// <summary>新增 sink; 已存在或為 null 時不做事.</summary>
        /// <remarks>配置: 每次呼叫配置新陣列.</remarks>
        public static void AddSink(ILogSink sink)
        {
            if (sink == null) return;
            lock (sinkGate)
            {
                var current = sinks;
                if (System.Array.IndexOf(current, sink) >= 0) return;
                var next = new ILogSink[current.Length + 1];
                System.Array.Copy(current, next, current.Length);
                next[current.Length] = sink;
                sinks = next;
            }
        }

        /// <summary>移除 sink; 不存在時不做事.</summary>
        /// <remarks>配置: 有移除時配置新陣列.</remarks>
        public static void RemoveSink(ILogSink sink)
        {
            lock (sinkGate)
            {
                var current = sinks;
                int index = System.Array.IndexOf(current, sink);
                if (index < 0) return;
                var next = current.Length == 1 ? emptySinks : new ILogSink[current.Length - 1];
                System.Array.Copy(current, 0, next, 0, index);
                System.Array.Copy(current, index + 1, next, index, current.Length - index - 1);
                sinks = next;
                failedSinks?.Remove(sink);  // 重新加入後再失敗時會再回報一次
            }
        }

        /// <summary>移除全部 sink (一併清除 SinkErrorHandler 的回報記錄).</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static void ClearSinks()
        {
            lock (sinkGate)
            {
                sinks = emptySinks;
                failedSinks = null;
            }
        }

        // ---- sink 例外隔離 ----

        /// <summary>
        /// sink 的 Write 拋出例外時呼叫 (參數為該 sink 與例外). 每個 sink 實例只回報第一次失敗, 之後的失敗不再回報;
        /// RemoveSink / ClearSinks 會清除記錄. 例外不會傳回呼叫 Log 的地方, 失敗的 sink 也不會被移除, 其他 sink 照常收到訊息.
        /// 回報期間同一執行緒上的 Log 寫入會被略過 (避免遞迴), 本處理器拋出的例外會被捕捉並忽略. null 時不回報.
        /// Runtime 的 DebugLogs.Install 會設定為以 UnityEngine.Debug.LogException 輸出.
        /// </summary>
        public static System.Action<ILogSink, System.Exception> SinkErrorHandler;

        // 已回報過的 sink (以參考比對); 第一次有 sink 失敗時才建立, 只在 sinkGate 內存取
        private static System.Collections.Generic.List<ILogSink> failedSinks;

        // 正在回報 sink 錯誤: 為 true 時同一執行緒上的 Log 寫入直接略過
        [System.ThreadStatic] private static bool reportingSinkError;

        // 只在例外發生時執行; 無例外的寫入路徑不經過這裡
        private static void ReportSinkError(ILogSink sink, System.Exception exception)
        {
            var handler = SinkErrorHandler;
            if (handler == null) return;
            lock (sinkGate)
            {
                if (failedSinks == null) failedSinks = new System.Collections.Generic.List<ILogSink>();
                for (int i = 0; i < failedSinks.Count; i++)
                    if (ReferenceEquals(failedSinks[i], sink)) return;
                failedSinks.Add(sink);
            }
            reportingSinkError = true;
            try { handler(sink, exception); }
            catch (System.Exception) { }    // 回報失敗時忽略, 不傳回呼叫端
            finally { reportingSinkError = false; }
        }

        /// <summary>
        /// 指定等級的訊息目前是否會送到 sink (總開關開啟、至少有一個 sink; Debug 等級另需 isDebugLog).
        /// 呼叫端組字串 (例如 $"hp={hp}") 前先檢查, 可避免關閉時的配置.
        /// </summary>
        /// <remarks>配置: 零配置.</remarks>
        public static bool IsEnabled(LogLevel level)
            => isPrintLog && sinks.Length > 0 && (level != LogLevel.Debug || isDebugLog);

        // 擷取堆疊追蹤的等級 (以 1 << (int)level 為位元). 預設只有 Error
        private static int stackTraceLevels = 1 << (int)LogLevel.Error;

        /// <summary>某個等級的訊息是否擷取堆疊追蹤 (存在 LogEntry.StackTrace). 預設值見 LogOptions 欄位初始值.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public static bool IsStackTraceEnabled(LogLevel level) => (stackTraceLevels & LevelBit(level)) != 0;

        /// <summary>
        /// 設定某個等級的訊息是否擷取堆疊追蹤. 隨時可改, 只影響之後寫入的訊息.
        /// 擷取有額外成本 (每次配置), 建議只開在少量出現的等級.
        /// </summary>
        /// <remarks>配置: 零配置.</remarks>
        public static void SetStackTraceEnabled(LogLevel level, bool isEnabled)
        {
            if (isEnabled) stackTraceLevels |= LevelBit(level);
            else stackTraceLevels &= ~LevelBit(level);
        }

        private static int LevelBit(LogLevel level)
        {
            int index = (int)level;
            return index >= 0 && index < 32 ? 1 << index : 0;
        }

        // 呼叫端參數 (callerMemberName / callerFilePath / callerLineNumber) 由編譯器在呼叫處填入, 不要自己傳.
        // 這些值是編譯期常數, 不會在執行時配置. 前面的 LogArgsEnd 讓多傳的參數變成編譯錯誤, 而不是被當成呼叫端資訊.

        /// <summary>寫入 Normal 等級的訊息; 呼叫端位置由編譯器自動填入.</summary>
        /// <remarks>配置: 關閉時零配置; 開啟時零配置 (該等級開啟堆疊追蹤時每次呼叫配置).</remarks>
        public static void Information(string message, LogCategory category = default(LogCategory),
            LogArgsEnd argsEnd = default, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
            => SinkLog(message, category, LogLevel.Normal, callerMemberName, callerFilePath, callerLineNumber);
        /// <summary>寫入 Warning 等級的訊息; 呼叫端位置由編譯器自動填入.</summary>
        /// <remarks>配置: 關閉時零配置; 開啟時零配置 (該等級開啟堆疊追蹤時每次呼叫配置).</remarks>
        public static void Warning(string message, LogCategory category = default(LogCategory),
            LogArgsEnd argsEnd = default, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
            => SinkLog(message, category, LogLevel.Warning, callerMemberName, callerFilePath, callerLineNumber);
        /// <summary>寫入 Error 等級的訊息; 呼叫端位置由編譯器自動填入.</summary>
        /// <remarks>配置: 關閉時零配置; 開啟時零配置 (該等級開啟堆疊追蹤時每次呼叫配置).</remarks>
        public static void Error(string message, LogCategory category = default(LogCategory),
            LogArgsEnd argsEnd = default, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
            => SinkLog(message, category, LogLevel.Error, callerMemberName, callerFilePath, callerLineNumber);
        /// <summary>
        /// 寫入 Debug 等級的訊息; 呼叫端位置由編譯器自動填入.
        /// 呼叫端編譯時未定義 UNITY_EDITOR / DEVELOPMENT_BUILD / TORBEN_LOG_DEBUG 任一個時, 整行呼叫 (含參數運算) 會被移除;
        /// 參數不要有副作用 (例如 $"{i++}"), 否則副作用會跟著消失. 保留時仍由 isPrintLog / isDebugLog 決定是否輸出.
        /// 去留由呼叫端的編譯決定, 函式庫內部不得呼叫, 否則打包 DLL 時就已決定.
        /// </summary>
        /// <remarks>配置: 被移除時零配置; 保留且關閉時零配置 (呼叫端組字串的配置除外); 開啟時零配置 (該等級開啟堆疊追蹤時每次呼叫配置).</remarks>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("TORBEN_LOG_DEBUG")]
        public static void Debug(string message, LogCategory category = default(LogCategory),
            LogArgsEnd argsEnd = default, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
            => SinkLog(message, category, LogLevel.Debug, callerMemberName, callerFilePath, callerLineNumber);

        // ---- Format: 開啟時才以 string.Format 組字串; 關閉時零配置 ----

        /// <summary>
        /// 指定等級的格式化訊息; 只有 IsEnabled(level) 時才組字串, 參數以泛型傳入, 關閉時不裝箱也不呼叫 ToString.
        /// Debug 等級請用 Log.Debug: 這裡傳 LogLevel.Debug 不會被編譯期移除.
        /// </summary>
        /// <remarks>配置: 關閉時零配置; 開啟時每次呼叫配置 (格式化字串, 值型別參數裝箱).</remarks>
        public static void Format<T1>(LogLevel level, string format, T1 arg1,
            LogArgsEnd argsEnd = default, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
            => WriteFormat(level, null, format, arg1, callerMemberName, callerFilePath, callerLineNumber);
        /// <inheritdoc cref="Format{T1}(LogLevel, string, T1, LogArgsEnd, string, string, int)"/>
        public static void Format<T1, T2>(LogLevel level, string format, T1 arg1, T2 arg2,
            LogArgsEnd argsEnd = default, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
            => WriteFormat(level, null, format, arg1, arg2, callerMemberName, callerFilePath, callerLineNumber);
        /// <inheritdoc cref="Format{T1}(LogLevel, string, T1, LogArgsEnd, string, string, int)"/>
        public static void Format<T1, T2, T3>(LogLevel level, string format, T1 arg1, T2 arg2, T3 arg3,
            LogArgsEnd argsEnd = default, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
            => WriteFormat(level, null, format, arg1, arg2, arg3, callerMemberName, callerFilePath, callerLineNumber);
        /// <inheritdoc cref="Format{T1}(LogLevel, string, T1, LogArgsEnd, string, string, int)"/>
        public static void Format<T1>(LogLevel level, LogCategory category, string format, T1 arg1,
            LogArgsEnd argsEnd = default, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
            => WriteFormat(level, category, format, arg1, callerMemberName, callerFilePath, callerLineNumber);
        /// <inheritdoc cref="Format{T1}(LogLevel, string, T1, LogArgsEnd, string, string, int)"/>
        public static void Format<T1, T2>(LogLevel level, LogCategory category, string format, T1 arg1, T2 arg2,
            LogArgsEnd argsEnd = default, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
            => WriteFormat(level, category, format, arg1, arg2, callerMemberName, callerFilePath, callerLineNumber);
        /// <inheritdoc cref="Format{T1}(LogLevel, string, T1, LogArgsEnd, string, string, int)"/>
        public static void Format<T1, T2, T3>(LogLevel level, LogCategory category, string format, T1 arg1, T2 arg2, T3 arg3,
            LogArgsEnd argsEnd = default, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
            => WriteFormat(level, category, format, arg1, arg2, arg3, callerMemberName, callerFilePath, callerLineNumber);

        // 參數以泛型傳入, 關閉時不會裝箱; 開啟時才交給 string.Format
        private static void WriteFormat<T1>(LogLevel level, LogCategory category, string format, T1 arg1,
            string callerMemberName, string callerFilePath, int callerLineNumber)
        {
            if (IsEnabled(level)) SinkLog(string.Format(format, arg1), category, level, callerMemberName, callerFilePath, callerLineNumber);
        }
        private static void WriteFormat<T1, T2>(LogLevel level, LogCategory category, string format, T1 arg1, T2 arg2,
            string callerMemberName, string callerFilePath, int callerLineNumber)
        {
            if (IsEnabled(level)) SinkLog(string.Format(format, arg1, arg2), category, level, callerMemberName, callerFilePath, callerLineNumber);
        }
        private static void WriteFormat<T1, T2, T3>(LogLevel level, LogCategory category, string format, T1 arg1, T2 arg2, T3 arg3,
            string callerMemberName, string callerFilePath, int callerLineNumber)
        {
            if (IsEnabled(level)) SinkLog(string.Format(format, arg1, arg2, arg3), category, level, callerMemberName, callerFilePath, callerLineNumber);
        }

        // Internal method to send log entries to the sink.
        private static void SinkLog(string message, LogCategory category, LogLevel level,
            string callerMemberName, string callerFilePath, int callerLineNumber)
        {
            if (!IsEnabled(level)) return; // 總開關關閉、沒有 sink, 或 Debug 訊息未開啟時都不輸出
            if (reportingSinkError) return; // 回報 sink 錯誤期間 (同一執行緒) 不再寫入, 避免遞迴
            if (category == null) category = LogCategoryEmpty.None;
            var stackTrace = IsStackTraceEnabled(level) ? LogStackTrace.Capture() : null;
            var entry = new LogEntry(level, category, message, System.DateTime.UtcNow,
                callerFilePath, callerMemberName, callerLineNumber, stackTrace);
            var current = sinks;
            for (int i = 0; i < current.Length; i++)
            {
                // 一個 sink 失敗不影響其他 sink, 例外也不傳回呼叫端
                try { current[i].Write(in entry); }
                catch (System.Exception e) { ReportSinkError(current[i], e); }
            }
        }
    }
}

// 簡易筆記
/*  # 設置說明
     1. 系統載入時會自動讀取 DebugLogs.cs 中的 Install 方法，以 Log.AddSink 加入 DebugLogs (Console) 與 DebugWindow 的歷史紀錄 sink。
     2. Log 類別提供了四個靜態方法：Information、Warning、Error 和 Debug，用於記錄不同級別的日誌。
     3. 每個方法都接受一個消息字符串和一個可選的 LogCategory 參數。如果未提供 LogCategory，將使用 LogCategoryEmpty.None 作為默認值。
     4. 需要組字串時，用 Log.Format (例如 Log.Format(LogLevel.Normal, "hp={0}", hp)) 或先檢查 Log.IsEnabled(level)，關閉時不會配置；Debug 等級用 Log.Debug，正式版會被編譯期移除。
     5. 每筆 LogEntry 會記錄 UTC 時間與呼叫端位置 (編譯器自動填入)；Log.SetStackTraceEnabled 開啟的等級另外擷取堆疊追蹤 (預設只有 Error)。

    # 使用說明
     1. 在 Unity 中，您可以在任何地方調用 Log.Information、Log.Warning 或 Log.Error 方法來記錄日誌。
     2. 您可以選擇性地提供一個自定義的 LogCategory，以便對日誌進行分類和標記。
     3. 日誌會傳給所有已加入的 ILogSink (預設為 DebugLogs 與畫面視窗的歷史紀錄)。

    # 注意事項
     1. 沒有任何 sink 時日誌不會被輸出，可用 Log.HasSink 確認。
     2. 自訂輸出可用 Log.AddSink / Log.RemoveSink 加入或移除。

    # 運作流程摘要
     1. DebugLogs.Install 方法在應用程序啟動時被調用，加入 Console 與歷史紀錄兩個 sink。
     2. 當調用 Log.Information、Log.Warning 或 Log.Error 方法時，
        這些方法會創建一個 LogEntry 對象，並依序傳給每個 sink 的 Write 方法。
     3. DebugLogs 的 Write 方法根據日誌級別將日誌輸出到 Unity 控制台，並附加相應的標籤和消息內容。

 */