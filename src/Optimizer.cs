using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MemoryOptimizer
{
    internal sealed class OptimizeOptions
    {
        public bool TrimWorkingSets = true;
        public bool PurgeStandby = true;
        public bool ClearFileCache = true;
        public List<string> ExcludedProcesses = new List<string>();
        public bool ProtectForeground = true;
    }

    internal sealed class OptimizeResult
    {
        public bool IsAdministrator;
        public long WorkingSetBefore;
        public long WorkingSetAfter;
        public int TotalProcesses;
        public int TrimmedProcesses;
        public int BlockedProcesses;
        public int SkippedProcesses;
        public ulong CacheBefore;
        public ulong CacheAfter;
        public ulong AvailableBefore;
        public ulong AvailableAfter;
        public bool StandbyPurged;
        public bool FileCacheCleared;
        public long FreedBytes;
        public double ElapsedSeconds;
        public int ActionsSucceeded;
        public int ActionsFailed;
    }

    internal static class Optimizer
    {
        /// <summary>关键进程 / 受保护进程，压缩它们的工作集没有意义，甚至会引发问题。</summary>
        private static readonly HashSet<string> SkipProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Idle", "System", "Registry", "Memory Compression", "MemCompression",
            "Secure System", "csrss", "smss", "wininit", "winlogon", "services",
            "lsass", "audiodg", "fontdrvhost", "dwm"
        };

        public static OptimizeResult Run(OptimizeOptions options, Action<string, LogLevel> log)
        {
            if (log == null)
                log = delegate(string message, LogLevel level) { };

            Stopwatch watch = Stopwatch.StartNew();
            OptimizeResult result = new OptimizeResult();
            result.IsAdministrator = Native.IsAdministrator();

            log(Loc.T("opt.log.start"), LogLevel.Info);
            if (!result.IsAdministrator)
                log(Loc.T("opt.log.noAdmin"), LogLevel.Warn);

            // 这两个特权是清理待机列表 / 文件缓存必需的；普通权限下会静默失败。
            Native.EnablePrivilege("SeProfileSingleProcessPrivilege");
            Native.EnablePrivilege("SeIncreaseQuotaPrivilege");

            MemorySnapshot before = MemorySnapshot.Capture(true);
            result.AvailableBefore = before.AvailPhys;
            result.CacheBefore = before.SystemCache;
            result.WorkingSetBefore = before.WorkingSetSum;

            if (options.ClearFileCache)
            {
                int error;
                if (Native.ClearSystemFileCache(out error))
                {
                    result.FileCacheCleared = true;
                    result.ActionsSucceeded++;
                    log(Loc.T("opt.log.cacheOk"), LogLevel.Success);
                }
                else
                {
                    result.ActionsFailed++;
                    log(Loc.F("opt.log.cacheFail", DescribeWin32Error(error)), LogLevel.Warn);
                }
            }

            if (options.TrimWorkingSets)
            {
                HashSet<string> skipNames = new HashSet<string>(SkipProcessNames, StringComparer.OrdinalIgnoreCase);
                if (options.ExcludedProcesses != null)
                {
                    foreach (string name in options.ExcludedProcesses)
                    {
                        if (!string.IsNullOrEmpty(name)) skipNames.Add(name);
                    }
                }

                int protectedPid = options.ProtectForeground ? Native.GetForegroundProcessId() : 0;
                string protectedName = null;
                if (protectedPid > 0)
                {
                    try
                    {
                        using (Process foreground = Process.GetProcessById(protectedPid))
                            protectedName = foreground.ProcessName;
                    }
                    catch { }
                }

                if (options.ProtectForeground)
                {
                    if (protectedName != null)
                        log(Loc.F("opt.log.fgSkip", protectedName), LogLevel.Info);
                    else
                        log(Loc.T("opt.log.fgNone"), LogLevel.Info);
                }

                TrimResult trim = Native.TrimWorkingSets(skipNames, protectedPid);
                result.TotalProcesses = trim.Total;
                result.TrimmedProcesses = trim.Trimmed;
                result.BlockedProcesses = trim.Blocked;
                result.SkippedProcesses = trim.Skipped;
                result.WorkingSetBefore = trim.WorkingSetBefore;
                result.WorkingSetAfter = trim.WorkingSetAfter;

                if (trim.Trimmed > 0)
                {
                    result.ActionsSucceeded++;
                    log(Loc.F("opt.log.wsOk", trim.Trimmed, trim.Skipped, trim.Blocked, trim.Total), LogLevel.Success);
                }
                else
                {
                    result.ActionsFailed++;
                    log(Loc.T("opt.log.wsFail"), LogLevel.Warn);
                }
            }

            if (options.PurgeStandby)
            {
                int status = Native.PurgeStandbyList();
                if (status == 0)
                {
                    result.StandbyPurged = true;
                    result.ActionsSucceeded++;
                    log(Loc.T("opt.log.standbyOk"), LogLevel.Success);
                }
                else
                {
                    result.ActionsFailed++;
                    log(Loc.F("opt.log.standbyFail", DescribeNtStatus(status)), LogLevel.Warn);
                }
            }

            MemorySnapshot after = MemorySnapshot.Capture(false);
            result.AvailableAfter = after.AvailPhys;
            result.CacheAfter = after.SystemCache;
            if (!options.TrimWorkingSets)
                result.WorkingSetAfter = MemorySnapshot.SumWorkingSets();

            long workingSetDelta = result.WorkingSetBefore - result.WorkingSetAfter;
            long cacheDelta = (long)result.CacheBefore - (long)result.CacheAfter;
            long freed = 0;
            if (workingSetDelta > 0) freed += workingSetDelta;
            if (cacheDelta > 0) freed += cacheDelta;
            result.FreedBytes = freed;
            result.ElapsedSeconds = watch.Elapsed.TotalSeconds;

            log(Loc.F("opt.log.done", result.ElapsedSeconds, Fmt.Bytes(result.FreedBytes)),
                result.ActionsSucceeded > 0 ? LogLevel.Success : LogLevel.Warn);
            log(Loc.F("opt.log.state",
                Fmt.Bytes(result.AvailableBefore), Fmt.Bytes(result.AvailableAfter),
                Fmt.Bytes(result.WorkingSetBefore), Fmt.Bytes(result.WorkingSetAfter)), LogLevel.Info);

            return result;
        }

        private static string DescribeWin32Error(int code)
        {
            if (code == 1314) return Loc.F("opt.err.privilege", code);
            if (code == 5) return Loc.F("opt.err.denied", code);
            return Loc.F("opt.err.generic", code);
        }

        private static string DescribeNtStatus(int status)
        {
            if (status == unchecked((int)0xC0000061)) return Loc.T("opt.err.ntPrivilege");
            return Loc.F("opt.err.nt", status);
        }
    }
}
