using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    internal static class ChildProcessReaper
    {
        private const uint JobObjectExtendedLimitInformation = 9;
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;

        private static readonly object Gate = new object();
        private static IntPtr _job = IntPtr.Zero;
        private static bool _initialised;

        public static void Register(Process process)
        {
            if (process == null) return;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            var job = EnsureJob();
            if (job == IntPtr.Zero) return;

            try { AssignProcessToJobObject(job, process.Handle); }
            catch (InvalidOperationException) { }
            catch (PlatformNotSupportedException) { }
        }

        private static IntPtr EnsureJob()
        {
            lock (Gate)
            {
                if (_initialised) return _job;
                _initialised = true;

                var job = CreateJobObject(IntPtr.Zero, null);
                if (job == IntPtr.Zero) return _job;

                var info = new JobObjectExtendedLimitInfo();
                info.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;

                var length = Marshal.SizeOf(typeof(JobObjectExtendedLimitInfo));
                var buffer = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.StructureToPtr(info, buffer, false);
                    if (SetInformationJobObject(job, JobObjectExtendedLimitInformation, buffer, (uint)length))
                        _job = job;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }

                return _job;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(
            IntPtr hJob, uint infoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInfo
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInfo
        {
            public JobObjectBasicLimitInfo BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }
}
