using System;
using System.Runtime.InteropServices;
using CG.Framework.Attributes;
using CG.Framework.Helper;
using CG.Framework.Plugin.Memory;

namespace CG.Memory;

[PluginInfo("CorrM", "BypaPh", "Simple kernel to read/write memory of process.", "https://github.com/CheatGear", "https://github.com/CheatGear/Memory.BypaPh")]
public class BypaPh : MemoryPlugin
{
    private const string LibName = "64.dll";

    [DllImport(LibName, EntryPoint = "CreateInstance")]
    internal static extern UIntPtr BypaPH_ctor(uint pId);

    [DllImport(LibName, EntryPoint = "DeleteInstance")]
    internal static extern void BypaPH_dtor(UIntPtr pInstance);

    [DllImport(LibName, EntryPoint = "ReTarget")]
    internal static extern void BypaPH_ReTarget(UIntPtr pInstance, uint pId);

    [DllImport(LibName, EntryPoint = "SetRemoveAllOnExit")]
    internal static extern void BypaPH_SetRemoveAllOnExit(UIntPtr pInstance, bool removeOnExit);

    [DllImport(LibName, EntryPoint = "GetProcessHandle")]
    internal static extern IntPtr BypaPH_GetProcessHandle(UIntPtr pInstance);

    [DllImport(LibName, EntryPoint = "RWVM")]
    internal static extern int BypaPH_RWVM(UIntPtr pInstance, UIntPtr baseAddress, [Out] byte[] buffer, UIntPtr bufferSize, out int numberOfBytesReadOrWritten, bool read = true, bool unsafeRequest = false);
    
    private UIntPtr _pInstance;

    public override Version TargetFrameworkVersion { get; } = new(3, 0, 0);
    public override Version PluginVersion { get; } = new(3, 0, 0);

    private IntPtr GetProcessHandle()
    {
        return BypaPH_GetProcessHandle(_pInstance);
    }

    protected override bool OnInit()
    {
        // Using CheatGear as a target it's just to init BypaPH
        _pInstance = BypaPH_ctor((uint)Environment.ProcessId);

        return _pInstance != UIntPtr.Zero;
    }

    protected override void OnDispose()
    {
        if (_pInstance == UIntPtr.Zero)
            return;

        BypaPH_SetRemoveAllOnExit(_pInstance, true);
        BypaPH_dtor(_pInstance);

        _pInstance = UIntPtr.Zero;
    }
    
    protected override bool OnTargetChange()
    {
        ProcessHandle = GetProcessHandle();
        if (!IsValidProcessHandle())
            return false;
            
        Is64Bit = UtilsExtensions.Is64BitProcess(ProcessHandle);
        BypaPH_ReTarget(_pInstance, (uint)CurrentTarget.Process.Id);
        return true;
    }

    public override bool ReadBytes(UIntPtr address, int size, out byte[] buffer, out int numberOfBytesRead)
    {
        var bytes = new byte[size];
        buffer = Array.Empty<byte>();
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
    
}