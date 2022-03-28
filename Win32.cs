using System;
using System.Runtime.InteropServices;

namespace CG.Memory;

internal static class Win32
{
    #region [ Structs && Enums ]
    public static readonly IntPtr InvalidHandleValue = new(-1);


    [Flags]
    public enum MemoryState
    {
        None = 0,
        MemCommit = 0x1000,
        MemFree = 0x10000,
        MemReserve = 0x2000
    }

    [Flags]
    public enum MemoryType
    {
        None = 0,
        MemPrivate = 0x20000,
        MemMapped = 0x40000,
        MemImage = 0x1000000
    }

    [Flags]
    public enum MemoryProtection
    {
        None = 0,
        PageNoAccess = 0x00000001,
        PageReadonly = 0x00000002,
        PageReadwrite = 0x00000004,
        PageWriteCopy = 0x00000008,
        PageExecute = 0x00000010,
        PageExecuteRead = 0x00000020,
        PageExecuteReadwrite = 0x00000040,
        PageExecuteWriteCopy = 0x00000080,
        PageGuard = 0x00000100,
        PageNocache = 0x00000200,
        PageWriteCombine = 0x00000400,
        PageTargetsInvalid = 0x40000000
    }

    [Flags]
    internal enum ProcessAccessFlags : uint
    {
        None = 0,
        Terminate = 0x00000001,
        CreateThread = 0x00000002,
        VirtualMemoryOperation = 0x00000008,
        VirtualMemoryRead = 0x00000010,
        VirtualMemoryWrite = 0x00000020,
        DuplicateHandle = 0x00000040,
        CreateProcess = 0x000000080,
        SetQuota = 0x00000100,
        SetInformation = 0x00000200,
        QueryInformation = 0x00000400,
        QueryLimitedInformation = 0x00001000,
        Synchronize = 0x00100000,
        All = 0x001F0FFF
    }

    [Flags]
    public enum SnapshotFlags : uint
    {
        None = 0,
        HeapList = 0x00000001,
        Process = 0x00000002,
        Thread = 0x00000004,
        Module = 0x00000008,
        Module32 = 0x00000010,
        All = HeapList | Process | Thread | Module | Module32,
        NoHeaps = 0x40000000,
        Inherit = 0x80000000
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SystemInfo
    {
        public ushort ProcessorArchitecture;
        public ushort Reserved;
        public uint PageSize; // DWORD
        public UIntPtr MinimumApplicationAddress; // (long)void*
        public UIntPtr MaximumApplicationAddress; // (long)void*
        public UIntPtr ActiveProcessorMask; // DWORD*
        public uint NumberOfProcessors; // DWORD (WTF)
        public uint ProcessorType; // DWORD
        public uint AllocationGranularity; // DWORD
        public ushort ProcessorLevel; // WORD
        public ushort ProcessorRevision; // WORD
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryBasicInformation
    {
        public UIntPtr BaseAddress;
        public UIntPtr AllocationBase;
        public MemoryProtection AllocationProtect;
        public int __alignment1;
        public UIntPtr RegionSize;
        public MemoryState State;
        public MemoryProtection Protect;
        public MemoryType Type;
        public int __alignment2;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ModuleEntry32
    {
        public uint DwSize;
        public uint Th32ModuleID;
        public uint Th32ProcessID;
        public uint GlblcntUsage;
        public uint ProccntUsage;
        public UIntPtr ModBaseAddr;
        public uint ModBaseSize;
        public IntPtr HModule;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string SzModule;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string SzExePath;
    }

    #endregion

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr hHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool ReadProcessMemory(IntPtr hProcess, UIntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool WriteProcessMemory(IntPtr hProcess, UIntPtr lpBaseAddress, [In] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll")]
    internal static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool wow64Process);

    [DllImport("kernel32.dll")]
    internal static extern void GetSystemInfo(out SystemInfo info);

    [DllImport("kernel32.dll", EntryPoint = "VirtualQueryEx")]
    internal static extern int VirtualQueryEx(IntPtr hProcess, UIntPtr lpAddress, out MemoryBasicInformation lpBuffer, uint dwLength);

    [DllImport("ntdll.dll")]
    internal static extern int NtSuspendProcess(IntPtr hProcess);

    [DllImport("ntdll.dll")]
    internal static extern int NtResumeProcess(IntPtr hProcess);

    [DllImport("ntdll.dll")]
    internal static extern int NtTerminateProcess(IntPtr hProcess, int exitStatus);

    [DllImport("kernel32.dll")]
    internal static extern IntPtr CreateToolhelp32Snapshot(SnapshotFlags dwFlags, int th32ProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool Module32First(IntPtr hSnapshot, ref ModuleEntry32 lpme);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool Module32Next(IntPtr hSnapshot, ref ModuleEntry32 lpme);

}
