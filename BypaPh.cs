using System;
using System.Runtime.InteropServices;
using CG.Framework.Attributes;
using CG.Framework.Plugin.Memory;

namespace CG.Memory;

[PluginInfo("CorrM", "BypaPh", "Simple kernel to read/write memory of process.")]
public class BypaPh : MemoryPlugin
{
    [Flags]
    public enum ProcessAccessFlags : uint
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

    private const string LibName = "64.dll";

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool IsWow64Process([In] UIntPtr processHandle, [Out] out bool wow64Process);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern UIntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(UIntPtr hHandle);

    [DllImport(LibName, EntryPoint = "CreateInstance")]
    private static extern UIntPtr BypaPH_ctor(uint pId);

    [DllImport(LibName, EntryPoint = "DeleteInstance")]
    private static extern void BypaPH_dtor(UIntPtr pInstance);

    [DllImport(LibName, EntryPoint = "ReTarget")]
    private static extern void BypaPH_ReTarget(UIntPtr pInstance, uint pId);

    [DllImport(LibName, EntryPoint = "SetRemoveAllOnExit")]
    private static extern void BypaPH_SetRemoveAllOnExit(UIntPtr pInstance, bool removeOnExit);

    [DllImport(LibName, EntryPoint = "GetProcessHandle")]
    private static extern UIntPtr BypaPH_GetProcessHandle(UIntPtr pInstance);

    [DllImport(LibName, EntryPoint = "RWVM")]
    private static extern int BypaPH_RWVM(UIntPtr pInstance, UIntPtr baseAddress, [Out] byte[] buffer, UIntPtr bufferSize, out int numberOfBytesReadOrWritten, bool read = true, bool unsafeRequest = false);

    private UIntPtr _pInstance;

    public override UIntPtr ProcessHandle { get; protected set; }
    public override bool Is64Bit { get; protected set; }
    public override bool IsSetup { get; protected set; }

    public static bool Is64BitProcess(UIntPtr processHandle)
    {
        return IsWow64Process(processHandle, out bool retVal) && !retVal;
    }
    public UIntPtr GetProcessHandle()
    {
        return BypaPH_GetProcessHandle(_pInstance);
    }

    public override void Setup(MemorySetupInfo info)
    {
        if (_pInstance != UIntPtr.Zero)
        {
            BypaPH_ReTarget(_pInstance, (uint)info.Process.Id);
            return;
        }

        _pInstance = BypaPH_ctor((uint)info.Process.Id);
        ProcessHandle = OpenProcess(ProcessAccessFlags.All, false, info.Process.Id);
        Is64Bit = Is64BitProcess(ProcessHandle);

        IsSetup = true;
    }
    public override bool ReadBytes(UIntPtr address, int size, out ReadOnlySpan<byte> buffer, out int numberOfBytesRead)
    {
        var bytes = new byte[size];
        buffer = ReadOnlySpan<byte>.Empty;
        numberOfBytesRead = 0;

        if (_pInstance == UIntPtr.Zero)
            return false;

        // STATUS_SUCCESS
        bool ret = BypaPH_RWVM(_pInstance, address, bytes, (UIntPtr)size, out numberOfBytesRead) == 0;
        buffer = bytes;
        return ret;
    }
    public override bool WriteBytes(UIntPtr address, byte[] buffer, out int numberOfBytesWritten)
    {
        numberOfBytesWritten = 0;

        if (_pInstance == UIntPtr.Zero)
            return false;

        // STATUS_SUCCESS
        return BypaPH_RWVM(_pInstance, address, buffer, (UIntPtr)(uint)buffer.Length, out numberOfBytesWritten, false) == 0;
    }

    public override void Dispose()
    {
        base.Dispose();

        if (_pInstance == UIntPtr.Zero)
            return;

        BypaPH_SetRemoveAllOnExit(_pInstance, true);
        BypaPH_dtor(_pInstance);

        CloseHandle(ProcessHandle);

        _pInstance = UIntPtr.Zero;
        GC.SuppressFinalize(this);
    }
}