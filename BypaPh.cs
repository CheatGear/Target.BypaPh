using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using CG.Framework.Attributes;
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
    private MemoryTargetInfo _targetInfo;
    private Win32.SystemInfo _sysInfo;
    private int _memoryBasicInformationSize;

    public override Version TargetFrameworkVersion { get; } = new(3, 0, 0);
    public override Version PluginVersion { get; } = new(3, 0, 0);

    private static bool Is64BitProcess(IntPtr processHandle)
    {
        return Win32.IsWow64Process(processHandle, out bool retVal) && !retVal;
    }

    private IntPtr GetProcessHandle()
    {
        return BypaPH_GetProcessHandle(_pInstance);
    }

    private UIntPtr GameStartAddress()
    {
        return _sysInfo.MinimumApplicationAddress;
    }

    private UIntPtr GameEndAddress()
    {
        return _sysInfo.MaximumApplicationAddress;
    }

    protected override bool OnInit()
    {
        // Using CheatGear as a target it's just to init BypaPH
        _pInstance = BypaPH_ctor((uint)Environment.ProcessId);

        _memoryBasicInformationSize = Marshal.SizeOf<Win32.MemoryBasicInformation>();
        Win32.GetSystemInfo(out _sysInfo);

        return true;
    }

    protected override bool OnTargetChange(MemoryTargetInfo targetInfo)
    {
        _targetInfo = targetInfo;

        BypaPH_ReTarget(_pInstance, (uint)targetInfo.Process.Id);
        Is64Bit = Is64BitProcess(GetProcessHandle());

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

    public override MemoryRegionInfo? GetMemoryRegion(UIntPtr address)
    {
        // Get Region information
        bool valid = Win32.VirtualQueryEx(
            GetProcessHandle(),
            address,
            out Win32.MemoryBasicInformation info,
            (uint)_memoryBasicInformationSize
        ) == _memoryBasicInformationSize;

        if (!valid)
            return null;

        var region = new MemoryRegionInfo()
        {
            Address = info.BaseAddress,
            Size = info.RegionSize.ToUInt64(),
            State = (int)info.State,
            Protect = (int)info.Protect,
            Type = (int)info.Type,
        };

        return region;
    }

    public override MemoryModuleInfo? GetMainModule()
    {
        ProcessModule? processModule;

        try
        {
            processModule = _targetInfo.Process.MainModule;
            if (processModule is null)
                return null;
        }
        catch (Exception)
        {
            return null;
        }

        return new MemoryModuleInfo()
        {
            Address = (UIntPtr)processModule.BaseAddress.ToInt64(),
            Size = (uint)processModule.ModuleMemorySize,
            Name = Path.GetFileName(processModule.FileName) ?? string.Empty,
            Path = processModule.FileName ?? string.Empty
        };
    }

    public override List<MemoryModuleInfo> GetModulesList()
    {
        var ret = new List<MemoryModuleInfo>();
        // To Avoid Some Games not share it's modules, or could be emulator game
        try
        {
            IntPtr hSnap = Win32.CreateToolhelp32Snapshot(Win32.SnapshotFlags.Module | Win32.SnapshotFlags.Module32, _targetInfo.Process.Id);
            if (hSnap != Win32.InvalidHandleValue)
            {
                var modEntry = new Win32.ModuleEntry32()
                {
                    DwSize = (uint)Marshal.SizeOf(typeof(Win32.ModuleEntry32))
                };

                if (Win32.Module32First(hSnap, ref modEntry))
                {
                    do
                    {
                        var mod = new MemoryModuleInfo()
                        {
                            Handle = modEntry.HModule,
                            Address = modEntry.ModBaseAddr,
                            Size = modEntry.ModBaseSize,
                            Name = modEntry.SzModule,
                            Path = modEntry.SzExePath
                        };
                        ret.Add(mod);
                    } while (Win32.Module32Next(hSnap, ref modEntry));
                }
            }
            Win32.CloseHandle(hSnap);
        }
        catch
        {
            // Ignore
        }

        return ret;
    }

    public override bool IsBadAddress(UIntPtr uIntPtr)
    {
        return uIntPtr.ToUInt64() < GameStartAddress().ToUInt64() || uIntPtr.ToUInt64() > GameEndAddress().ToUInt64();
    }

    public override bool IsValidRemoteAddress(UIntPtr remoteAddress)
    {
        // TODO: Very bad when called in hot-path
        if (remoteAddress == UIntPtr.Zero || IsBadAddress(remoteAddress))
            return false;

        if (Win32.VirtualQueryEx(GetProcessHandle(), remoteAddress, out Win32.MemoryBasicInformation info, (uint)_memoryBasicInformationSize) != 0)
            return info.Protect != 0 && (info.Protect & Win32.MemoryProtection.PageNoAccess) == 0;

        return false;
    }

    public override bool SuspendProcess()
    {
        return Win32.NtSuspendProcess(GetProcessHandle()) >= 0;
    }

    public override bool ResumeProcess()
    {
        return Win32.NtResumeProcess(GetProcessHandle()) >= 0;
    }

    public override bool TerminateProcess()
    {
        return Win32.NtTerminateProcess(GetProcessHandle(), 0) >= 0;
    }

    public override void Dispose()
    {
        if (_pInstance == UIntPtr.Zero)
            return;

        BypaPH_SetRemoveAllOnExit(_pInstance, true);
        BypaPH_dtor(_pInstance);

        _pInstance = UIntPtr.Zero;
    }
}