using System;
using System.Diagnostics;

namespace MemoryOptimizer
{
    /// <summary>某一时刻的物理内存 / 提交 / 工作集快照。</summary>
    internal sealed class MemorySnapshot
    {
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong SystemCache;
        public ulong CommitTotal;
        public ulong CommitLimit;
        public uint ProcessCount;
        public uint ThreadCount;
        public uint HandleCount;
        public long WorkingSetSum = -1;

        public ulong UsedPhys
        {
            get { return TotalPhys > AvailPhys ? TotalPhys - AvailPhys : 0UL; }
        }

        public double UsedPercent
        {
            get { return TotalPhys == 0 ? 0.0 : (double)UsedPhys * 100.0 / (double)TotalPhys; }
        }

        public static MemorySnapshot Capture(bool includeWorkingSet)
        {
            MemorySnapshot snapshot = new MemorySnapshot();

            Native.MEMORYSTATUSEX status;
            if (Native.TryGetMemoryStatus(out status))
            {
                snapshot.TotalPhys = status.ullTotalPhys;
                snapshot.AvailPhys = status.ullAvailPhys;
            }

            Native.PERFORMANCE_INFORMATION info;
            if (Native.TryGetPerformanceInfo(out info))
            {
                ulong pageSize = (ulong)info.PageSize.ToInt64();
                snapshot.SystemCache = (ulong)info.SystemCache.ToInt64();
                snapshot.CommitTotal = (ulong)info.CommitTotal.ToInt64() * pageSize;
                snapshot.CommitLimit = (ulong)info.CommitLimit.ToInt64() * pageSize;
                snapshot.ProcessCount = info.ProcessCount;
                snapshot.ThreadCount = info.ThreadCount;
                snapshot.HandleCount = info.HandleCount;
            }

            if (includeWorkingSet)
                snapshot.WorkingSetSum = SumWorkingSets();

            return snapshot;
        }

        public static long SumWorkingSets()
        {
            long sum = 0;
            Process[] processes;
            try { processes = Process.GetProcesses(); }
            catch { return 0; }

            foreach (Process process in processes)
            {
                try { sum += process.WorkingSet64; }
                catch { }
                process.Dispose();
            }
            return sum;
        }
    }
}
