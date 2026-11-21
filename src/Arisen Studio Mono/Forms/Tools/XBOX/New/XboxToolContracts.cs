using ArisenStudio.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.XBOX
{
    // Boundaries between the Xbox 360 tool forms and the parts that talk to a console or read Xbox package files.
    // The old forms called XDevkit (IXboxConsole), JRPC_Client and Isolib.STFSPackage directly. All three are Windows-only
    // or third-party and are NOT ported. The forms call only the interfaces below; a real adapter (XDevkit + JRPC, or another
    // route) implements them later. The defaults ("Unavailable...") are stubs that fail with a clear message.
    // Methods throw on failure. They are synchronous, as the old calls were; the forms run them off the UI thread where it matters.

    public enum XboxLedState
    {
        Green = 0,
        Orange = 1,
        Red = 2,
        Off = 3
    }

    public enum XboxTemperatureSensor
    {
        Cpu = 0,
        Gpu = 1,
        MemoryEdram = 2,
        Motherboard = 3
    }

    public enum XboxIconColor
    {
        Black = 0,
        Blue = 1,
        BlueGray = 2,
        White = 3
    }

    /// <summary>
    /// Name and icon shown in Xbox Neighborhood.
    /// </summary>
    public interface IXboxNeighborhood
    {
        void ChangeName(string name);

        void ChangeIcon(XboxIconColor color);
    }

    /// <summary>
    /// Console details and hardware control (old XDevkit IXboxConsole plus JRPC: IPAddress, GetCPUKey, GetTemperature, SetLeds,
    /// SetTrayState, SetFanSpeed, XamGetCurrentTitleId, Name).
    /// </summary>
    public interface IXboxConsoleInfo : IXboxNeighborhood
    {
        string IpAddress { get; }

        string Name { get; }

        string GetCpuKey();

        string GetKernelVersion();

        uint GetCurrentTitleId();

        /// <summary>Temperature in degrees Celsius.</summary>
        int GetTemperature(XboxTemperatureSensor sensor);

        void SetLeds(XboxLedState topLeft, XboxLedState topRight, XboxLedState bottomLeft, XboxLedState bottomRight);

        void SetTrayOpen(bool open);

        void SetFanSpeed(int fan, int speed);
    }

    /// <summary>
    /// Send and receive files with the console (old XDevkit ReceiveFile / SendFile). Console paths look like "Hdd:\launch.ini".
    /// </summary>
    public interface IXboxFileTransfer
    {
        /// <summary>Copy a console file to the exact local file path, overwriting.</summary>
        void ReceiveFile(string localFile, string consoleFile);

        /// <summary>Copy a local file to the exact console file path, overwriting.</summary>
        void SendFile(string localFile, string consoleFile);
    }

    /// <summary>
    /// Capture the console screen (old XDevkit ScreenShot).
    /// </summary>
    public interface IXboxScreenshotSource
    {
        /// <summary>Capture the screen into a BMP file at the given local path.</summary>
        void CaptureScreenshot(string localFile);
    }

    /// <summary>
    /// Start a game from its .xex file (old LaunchXex extension).
    /// </summary>
    public interface IXboxGameLauncher
    {
        void LaunchXex(string xexPath, string directory);
    }

    /// <summary>
    /// A module that is loaded on the console.
    /// </summary>
    public sealed class XboxModuleEntry
    {
        public string Name { get; set; }

        public long Size { get; set; }
    }

    /// <summary>
    /// Module list, load and unload (old XDevkit DebugTarget plus JRPC calls).
    /// Notes for the adapter, from the old code: LoadModule called xboxkrnl.exe ordinal 0x199 (XexLoadImage) with
    /// (path, 8, 0, 0). UnloadModule looked the handle up with xam.xex ordinal 0x44E (XexGetModuleHandle), wrote the Int16
    /// value 1 at handle + 0x40 when systemDll was true, then called xboxkrnl.exe ordinal 0x1A1 (XexUnloadImage) with the handle.
    /// </summary>
    public interface IXboxModuleControl
    {
        /// <summary>Connect as debugger to the console (old: DebugTarget.ConnectAsDebugger with the Force flag).</summary>
        void ConnectAsDebugger();

        IList<XboxModuleEntry> GetModules();

        void LoadModule(string path);

        /// <summary>Unload a module by name. False when no module with that name is loaded.</summary>
        bool UnloadModule(string moduleName, bool systemDll);
    }

    /// <summary>
    /// Memory and function calls on the console (old JRPC Call, ReadBytes, WriteBytes).
    /// </summary>
    public interface IXboxMemoryAccess
    {
        bool IsConnected { get; }

        uint GetCurrentTitleId();

        /// <summary>Call the function at an address and return its 32-bit result. Arguments are ints, longs, strings and the like, as JRPC took them.</summary>
        uint CallUInt32(uint address, params object[] arguments);

        byte[] ReadBytes(uint address, int count);

        void WriteBytes(uint address, byte[] data);
    }

    /// <summary>
    /// Pick a profile file on the console (old XboxExtensions.GetXboxProfileFile: list the profile files, show a chooser).
    /// Returns an item whose Name is the file name and Value the console path, or null when none was found or chosen.
    /// </summary>
    public interface IXboxProfilePicker
    {
        ListItem PickProfileFile(IWin32Window owner);
    }

    /// <summary>
    /// Everything an adapter has to provide, in one interface. The forms ask only for the part they use.
    /// </summary>
    public interface IXboxConsoleAccess : IXboxConsoleInfo, IXboxFileTransfer, IXboxScreenshotSource, IXboxGameLauncher, IXboxModuleControl, IXboxMemoryAccess, IXboxProfilePicker
    {
    }

    /// <summary>
    /// Stub: not connected, and every call fails with NotSupportedException. It is the default of every Xbox tool form.
    /// </summary>
    public sealed class UnavailableXboxConsoleAccess : IXboxConsoleAccess
    {
        private static readonly UnavailableXboxConsoleAccess _instance = new UnavailableXboxConsoleAccess();

        public static UnavailableXboxConsoleAccess Instance
        {
            get { return _instance; }
        }

        private static Exception NotAvailable()
        {
            return new NotSupportedException("Xbox 360 console access is not available in this build. No XDevkit / JRPC adapter is connected.");
        }

        public bool IsConnected
        {
            get { return false; }
        }

        public string IpAddress
        {
            get { throw NotAvailable(); }
        }

        public string Name
        {
            get { throw NotAvailable(); }
        }

        public string GetCpuKey()
        {
            throw NotAvailable();
        }

        public string GetKernelVersion()
        {
            throw NotAvailable();
        }

        public uint GetCurrentTitleId()
        {
            throw NotAvailable();
        }

        public int GetTemperature(XboxTemperatureSensor sensor)
        {
            throw NotAvailable();
        }

        public void SetLeds(XboxLedState topLeft, XboxLedState topRight, XboxLedState bottomLeft, XboxLedState bottomRight)
        {
            throw NotAvailable();
        }

        public void SetTrayOpen(bool open)
        {
            throw NotAvailable();
        }

        public void SetFanSpeed(int fan, int speed)
        {
            throw NotAvailable();
        }

        public void ChangeName(string name)
        {
            throw NotAvailable();
        }

        public void ChangeIcon(XboxIconColor color)
        {
            throw NotAvailable();
        }

        public void ReceiveFile(string localFile, string consoleFile)
        {
            throw NotAvailable();
        }

        public void SendFile(string localFile, string consoleFile)
        {
            throw NotAvailable();
        }

        public void CaptureScreenshot(string localFile)
        {
            throw NotAvailable();
        }

        public void LaunchXex(string xexPath, string directory)
        {
            throw NotAvailable();
        }

        public void ConnectAsDebugger()
        {
            throw NotAvailable();
        }

        public IList<XboxModuleEntry> GetModules()
        {
            throw NotAvailable();
        }

        public void LoadModule(string path)
        {
            throw NotAvailable();
        }

        public bool UnloadModule(string moduleName, bool systemDll)
        {
            throw NotAvailable();
        }

        public uint CallUInt32(uint address, params object[] arguments)
        {
            throw NotAvailable();
        }

        public byte[] ReadBytes(uint address, int count)
        {
            throw NotAvailable();
        }

        public void WriteBytes(uint address, byte[] data)
        {
            throw NotAvailable();
        }

        public ListItem PickProfileFile(IWin32Window owner)
        {
            throw NotAvailable();
        }
    }

    /// <summary>
    /// The header fields of an Xbox 360 STFS package (old Isolib.STFSPackage Stfs.HeaderData).
    /// </summary>
    public interface IStfsHeader
    {
        string DisplayName { get; set; }

        string TitleName { get; set; }

        string TitleId { get; }

        string ProfileId { get; set; }

        string DeviceId { get; set; }

        string ConsoleId { get; set; }

        /// <summary>The package thumbnail.</summary>
        Image PackageImage { get; set; }

        /// <summary>The title thumbnail.</summary>
        Image ContentImage { get; set; }
    }

    /// <summary>
    /// An open STFS package (old Isolib.STFSPackage Stfs).
    /// </summary>
    public interface IStfsPackage
    {
        IStfsHeader Header { get; }

        /// <summary>Write the header changes and re-sign the package (old Stfs.Finish).</summary>
        void Finish();

        void Close();
    }

    /// <summary>
    /// Opens STFS packages (game saves and profiles). Isolib.STFSPackage is a closed third-party library and is not ported;
    /// an adapter has to provide this (resigning needs a valid STFS writer).
    /// </summary>
    public interface IStfsPackageFactory
    {
        IStfsPackage Open(string filePath);
    }

    /// <summary>
    /// Stub: Open fails with NotSupportedException.
    /// </summary>
    public sealed class UnavailableStfsPackageFactory : IStfsPackageFactory
    {
        public IStfsPackage Open(string filePath)
        {
            throw new NotSupportedException("Xbox 360 STFS package support is not available in this build. No STFS library is connected.");
        }
    }
}
