using System;
using System.Runtime.InteropServices;
using CG.SDK.Dotnet.Attributes;
using CG.SDK.Dotnet.Helper;
using CG.SDK.Dotnet.Plugin.Target;

namespace CG.Memory;

file static class BypaPhNative
{
    private const string LIB_NAME = "64.dll";

    [DllImport(LIB_NAME, EntryPoint = "CreateInstance")]
    public static extern nuint Ctor(uint pId);

    [DllImport(LIB_NAME, EntryPoint = "DeleteInstance")]
    public static extern void Dtor(nuint pInstance);

    [DllImport(LIB_NAME, EntryPoint = "ReTarget")]
    public static extern void ReTarget(nuint pInstance, uint pId);

    [DllImport(LIB_NAME, EntryPoint = "SetRemoveAllOnExit")]
    public static extern void SetRemoveAllOnExit(nuint pInstance, bool removeOnExit);

    [DllImport(LIB_NAME, EntryPoint = "GetProcessHandle")]
    public static extern nint GetProcessHandle(nuint pInstance);

    [DllImport(LIB_NAME, EntryPoint = "RWVM")]
    public static extern int RWVM(nuint pInstance, nuint baseAddress, [Out] byte[] buffer, nuint bufferSize, out int numberOfBytesReadOrWritten, bool read = true, bool unsafeRequest = false);
}

[PluginInfo(Name = nameof(BypaPh), Version = "5.0.0", Author = "CorrM", Description = "Simple kernel to read/write memory of process.", WebsiteLink = "https://github.com/CheatGear", SourceCodeLink = "https://github.com/CheatGear/Memory.BypaPh")]
public class BypaPh : TargetHandlerPlugin<>
{
    private nuint _pInstance;

    private nint GetProcessHandle()
    {
        return BypaPhNative.GetProcessHandle(_pInstance);
    }

    protected override void Load()
    {
        // Using CheatGear as a target it's just to init BypaPH
        _pInstance = BypaPhNative.Ctor((uint)Environment.ProcessId);
    }

    protected override void Unload()
    {
        if (_pInstance == nuint.Zero)
            return;

        BypaPhNative.SetRemoveAllOnExit(_pInstance, true);
        BypaPhNative.Dtor(_pInstance);

        _pInstance = nuint.Zero;
    }

    protected override bool OnTargetChange()
    {
        if (CurrentTarget.Process is null)
            throw new NullReferenceException("'CurrentTarget.Process' is null");

        BypaPhNative.ReTarget(_pInstance, (uint)CurrentTarget.Process.Id);

        ProcessHandle = GetProcessHandle();
        if (!IsValidProcessHandle())
            return false;
        Is64Bit = UtilsExtensions.Is64BitProcess(ProcessHandle);

        return true;
    }

    public override bool ReadBytes(nuint address, int size, out byte[] buffer, out int numberOfBytesRead)
    {
        byte[]? bytes = new byte[size];
        buffer = Array.Empty<byte>();
        numberOfBytesRead = 0;

        if (_pInstance == nuint.Zero)
            return false;

        // STATUS_SUCCESS
        bool ret = BypaPhNative.RWVM(_pInstance, address, bytes, (nuint)size, out numberOfBytesRead) == 0;
        buffer = bytes;
        return ret;
    }

    public override bool WriteBytes(nuint address, byte[] buffer, out int numberOfBytesWritten)
    {
        numberOfBytesWritten = 0;

        if (_pInstance == nuint.Zero)
            return false;

        // STATUS_SUCCESS
        return BypaPhNative.RWVM(_pInstance, address, buffer, (nuint)(uint)buffer.Length, out numberOfBytesWritten, false) == 0;
    }
}
