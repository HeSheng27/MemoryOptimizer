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

            log("开始优化内存…", LogLevel.Info);
            if (!result.IsAdministrator)
                log("当前是普通权限，系统级清理可能被拒绝（可在底部切换到管理员）。", LogLevel.Warn);

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
                    log("已清空系统文件缓存。", LogLevel.Success);
                }
                else
                {
                    result.ActionsFailed++;
                    log(string.Format("清空系统文件缓存失败{0}。", DescribeWin32Error(error)), LogLevel.Warn);
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
                        log("前台程序保护：跳过 " + protectedName + "。", LogLevel.Info);
                    else
                        log("前台程序保护：未检测到前台窗口，本次不跳过前台进程。", LogLevel.Info);
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
                    log(string.Format("已压缩 {0} 个进程的工作集（跳过 {1} 个：系统关键进程/排除名单/自身；{2} 个无权限访问，共 {3} 个）。",
                        trim.Trimmed, trim.Skipped, trim.Blocked, trim.Total), LogLevel.Success);
                }
                else
                {
                    result.ActionsFailed++;
                    log("没有成功压缩任何进程，试试以管理员身份运行。", LogLevel.Warn);
                }
            }

            if (options.PurgeStandby)
            {
                int status = Native.PurgeStandbyList();
                if (status == 0)
                {
                    result.StandbyPurged = true;
                    result.ActionsSucceeded++;
                    log("已清空待机内存列表。", LogLevel.Success);
                }
                else
                {
                    result.ActionsFailed++;
                    log(string.Format("清空待机内存列表失败{0}。", DescribeNtStatus(status)), LogLevel.Warn);
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

            log(string.Format("优化完成，耗时 {0:0.00} 秒，本次释放约 {1}。",
                result.ElapsedSeconds, Fmt.Bytes(result.FreedBytes)),
                result.ActionsSucceeded > 0 ? LogLevel.Success : LogLevel.Warn);
            log(string.Format("内存状态：可用 {0} → {1}，进程工作集 {2} → {3}。",
                Fmt.Bytes(result.AvailableBefore), Fmt.Bytes(result.AvailableAfter),
                Fmt.Bytes(result.WorkingSetBefore), Fmt.Bytes(result.WorkingSetAfter)), LogLevel.Info);

            return result;
        }

        private static string DescribeWin32Error(int code)
        {
            if (code == 1314) return "（错误码 1314，需要管理员权限）";
            if (code == 5) return "（错误码 5，访问被拒绝）";
            return "（错误码 " + code + "）";
        }

        private static string DescribeNtStatus(int status)
        {
            if (status == unchecked((int)0xC0000061)) return "（STATUS_PRIVILEGE_NOT_HELD，需要管理员权限）";
            return string.Format("（NTSTATUS 0x{0:X8}）", status);
        }
    }
}
