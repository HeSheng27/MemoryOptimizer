using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace MemoryOptimizer
{
    /// <summary>
    /// Win32 / NT 原生接口封装：内存信息、工作集清理、待机列表清理、文件缓存清理、权限提升。
    /// </summary>
    internal static class Native
    {
        public const uint PROCESS_SET_QUOTA = 0x0100;
        public const uint PROCESS_QUERY_INFORMATION = 0x0400;

        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint SE_PRIVILEGE_ENABLED = 0x0002;
        private const int ERROR_NOT_ALL_ASSIGNED = 1300;
        private const int SYSTEM_MEMORY_LIST_INFORMATION = 0x50;
        private const int MEMORY_PURGE_STANDBY_LIST = 4;

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PERFORMANCE_INFORMATION
        {
            public uint cb;
            public IntPtr CommitTotal;
            public IntPtr CommitLimit;
            public IntPtr CommitPeak;
            public IntPtr PhysicalTotal;
            public IntPtr PhysicalAvailable;
            public IntPtr SystemCache;
            public IntPtr KernelTotal;
            public IntPtr KernelPaged;
            public IntPtr KernelNonpaged;
            public IntPtr PageSize;
            public uint HandleCount;
            public uint ProcessCount;
            public uint ThreadCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privileges;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPerformanceInfo(ref PERFORMANCE_INFORMATION info, uint cb);

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetSystemFileCacheSize(IntPtr minimum, IntPtr maximum, uint flags);

        [DllImport("ntdll.dll")]
        private static extern int NtSetSystemInformation(int infoClass, ref int info, int length);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string system, string name, out LUID luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(IntPtr token, [MarshalAs(UnmanagedType.Bool)] bool disableAll,
            ref TOKEN_PRIVILEGES newState, uint bufferLength, IntPtr previousState, IntPtr returnLength);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        public static bool TryGetMemoryStatus(out MEMORYSTATUSEX status)
        {
            status = new MEMORYSTATUSEX();
            status.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            return GlobalMemoryStatusEx(ref status);
        }

        public static bool TryGetPerformanceInfo(out PERFORMANCE_INFORMATION info)
        {
            info = new PERFORMANCE_INFORMATION();
            info.cb = (uint)Marshal.SizeOf(typeof(PERFORMANCE_INFORMATION));
            return GetPerformanceInfo(ref info, info.cb);
        }

        public static void SetDpiAware()
        {
            try { SetProcessDPIAware(); }
            catch { }
        }

        /// <summary>当前前台窗口所属进程的 PID；没有前台窗口时返回 0。</summary>
        public static int GetForegroundProcessId()
        {
            try
            {
                IntPtr window = GetForegroundWindow();
                if (window == IntPtr.Zero) return 0;
                uint processId;
                GetWindowThreadProcessId(window, out processId);
                return (int)processId;
            }
            catch
            {
                return 0;
            }
        }

        public static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>启用当前进程令牌中的某个特权（需要令牌本身已提升）。</summary>
        public static bool EnablePrivilege(string name)
        {
            IntPtr token = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out token))
                    return false;

                LUID luid;
                if (!LookupPrivilegeValue(null, name, out luid))
                    return false;

                TOKEN_PRIVILEGES privileges = new TOKEN_PRIVILEGES();
                privileges.PrivilegeCount = 1;
                privileges.Privileges = new LUID_AND_ATTRIBUTES();
                privileges.Privileges.Luid = luid;
                privileges.Privileges.Attributes = SE_PRIVILEGE_ENABLED;

                if (!AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero))
                    return false;

                return Marshal.GetLastWin32Error() != ERROR_NOT_ALL_ASSIGNED;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (token != IntPtr.Zero)
                    CloseHandle(token);
            }
        }

        /// <summary>清空系统文件缓存（等价于把 min/max 设为 -1 后刷新）。返回 NTSTATUS / Win32 错误码。</summary>
        public static bool ClearSystemFileCache(out int error)
        {
            try
            {
                bool ok = SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0);
                error = ok ? 0 : Marshal.GetLastWin32Error();
                return ok;
            }
            catch
            {
                error = -1;
                return false;
            }
        }

        /// <summary>清空待机内存列表（standby list）。0 表示成功，其余为 NTSTATUS。</summary>
        public static int PurgeStandbyList()
        {
            try
            {
                int command = MEMORY_PURGE_STANDBY_LIST;
                return NtSetSystemInformation(SYSTEM_MEMORY_LIST_INFORMATION, ref command, 4);
            }
            catch
            {
                return unchecked((int)0xC0000001);
            }
        }

        /// <summary>
        /// 对所有可访问进程调用 EmptyWorkingSet，把工作集里的页面移出物理内存。
        /// 只回收内存页，绝不结束进程。关键进程、排除名单、前台程序和自身会被跳过。
        /// </summary>
        public static TrimResult TrimWorkingSets(ICollection<string> skipNames, int protectedProcessId)
        {
            TrimResult result = new TrimResult();

            Process[] processes;
            try { processes = Process.GetProcesses(); }
            catch { return result; }

            int selfId = 0;
            try
            {
                using (Process self = Process.GetCurrentProcess())
                    selfId = self.Id;
            }
            catch { }

            foreach (Process process in processes)
            {
                int pid;
                string name;
                long workingSetBefore;
                try
                {
                    pid = process.Id;
                    name = process.ProcessName;
                    workingSetBefore = process.WorkingSet64;
                }
                catch
                {
                    process.Dispose();
                    continue;
                }

                result.Total++;
                result.WorkingSetBefore += workingSetBefore;

                bool skip = pid <= 4 || pid == selfId || pid == protectedProcessId ||
                            (skipNames != null && skipNames.Contains(name));
                if (skip)
                {
                    result.Skipped++;
                    result.WorkingSetAfter += workingSetBefore;
                    process.Dispose();
                    continue;
                }

                IntPtr handle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_SET_QUOTA, false, pid);
                if (handle == IntPtr.Zero)
                {
                    result.Blocked++;
                    result.WorkingSetAfter += workingSetBefore;
                    process.Dispose();
                    continue;
                }

                try
                {
                    if (EmptyWorkingSet(handle))
                        result.Trimmed++;
                    else
                        result.Blocked++;

                    long workingSetAfter;
                    try { workingSetAfter = process.WorkingSet64; }
                    catch { workingSetAfter = workingSetBefore; }
                    result.WorkingSetAfter += workingSetAfter;
                }
                finally
                {
                    CloseHandle(handle);
                    process.Dispose();
                }
            }

            return result;
        }
    }

    internal sealed class TrimResult
    {
        public long WorkingSetBefore;
        public long WorkingSetAfter;
        public int Total;
        public int Trimmed;
        public int Blocked;
        public int Skipped;
    }
}
