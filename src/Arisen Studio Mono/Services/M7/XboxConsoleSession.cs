using ArisenStudio.Forms.Tools.XBOX;
using ArisenStudio.Models.Resources;
using System;
using System.Collections.Generic;

namespace ArisenStudio.Services.M7
{
    // M7 boundary: XDevkit + JRPC (Xbox 360). Both are Windows-only; XDevkit is a COM interop library and JRPC_Client is a
    // third-party .NET assembly, so neither is used here.
    //
    // What already exists: Forms/Tools/XBOX/New/XboxToolContracts.cs holds the per-form interfaces (IXboxConsoleInfo,
    // IXboxFileTransfer, IXboxScreenshotSource, IXboxGameLauncher, IXboxModuleControl, IXboxMemoryAccess, IXboxProfilePicker,
    // IXboxConsoleAccess) and the stub UnavailableXboxConsoleAccess. They are NOT repeated here.
    //
    // What this file adds is what the old XboxExtensions.cs and MainWindow used and those contracts do not cover:
    //  - finding consoles and connecting (XboxManager, OpenConsole, JRPC.Connect with a retry),
    //  - power (Shutdown, Reboot), on-screen notifications (XNotify),
    //  - the console file system (DirectoryFiles, MakeDirectory, DeleteFile, RemoveDirectory, RenameFile),
    //  - typed big-endian memory reads and writes (XboxMemoryExtensions, plain code over IXboxMemoryAccess).
    // Methods throw on failure, as the existing contracts do. The stubs fail with NotSupportedException or report
    // "not connected"; none of them pretends to work.

    /// <summary>
    /// Reboot kind. The old code called XboxConsole.Reboot(..., XboxRebootFlags.Cold) from the menu item named "Soft reboot"
    /// and XboxRebootFlags.Warm from the one named "Hard reboot". The names here follow the XDevkit flags, not the menu
    /// names, so the adapter can map them one to one.
    /// </summary>
    public enum XboxRebootMode
    {
        Cold = 0,
        Warm = 1
    }

    /// <summary>
    /// The notification icon of an on-screen XNotify message. Values are the old XNotifyLogo enum, unchanged
    /// (the old names are kept, including their spelling, so a saved setting still maps).
    /// </summary>
    public enum XNotifyLogo : uint
    {
        XboxLogo = 0,
        NewMessageLogo = 1,
        FriendRequestLogo = 2,
        NewMessage = 3,
        FlashingXboxLogo = 4,
        GamertagSentYouAMessage = 5,
        GamertagSingedOut = 6,
        GamertagSignedin = 7,
        GamertagSignedIntoXboxLive = 8,
        GamertagSignedInOffline = 9,
        GamertagWantsToChat = 10,
        DisconnectedFromXboxLive = 11,
        Download = 12,
        FlashingMusicSymbol = 13,
        FlashingHappyFace = 14,
        FlashingFrowningFace = 15,
        FlashingDoubleSidedHammer = 16,
        GamertagWantsToChat2 = 17,
        PleaseReinsertMemoryUnit = 18,
        PleaseReconnectControllerm = 19,
        GamertagHasJoinedChat = 20,
        GamertagHasLeftChat = 21,
        GameInviteSent = 22,
        FlashLogo = 23,
        PageSentTo = 24,
        Four2 = 25,
        Four3 = 26,
        AchievementUnlocked = 27,
        Four9 = 28,
        GamertagWantsToTalkInVideoKinect = 29,
        VideoChatInviteSent = 30,
        ReadyToPlay = 31,
        CantDownloadX = 32,
        DownloadStoppedForX = 33,
        FlashingXboxConsole = 34,
        XSentYouAGameMessage = 35,
        DeviceFull = 36,
        Four7 = 37,
        FlashingChatIcon = 38,
        AchievementsUnlocked = 39,
        XHasSentYouANudge = 40,
        MessengerDisconnected = 41,
        Blank = 42,
        CantSignInMessenger = 43,
        MissedMessengerConversation = 44,
        FamilyTimerXTimeRemaining = 45,
        DisconnectedXboxLive11MinutesRemaining = 46,
        KinectHealthEffects = 47,
        Four5 = 48,
        GamertagWantsYouToJoinAnXboxLiveParty = 49,
        PartyInviteSent = 50,
        GameInviteSentToXboxLiveParty = 51,
        KickedFromXboxLiveParty = 52,
        Nulled = 53,
        DisconnectedXboxLiveParty = 54,
        Downloaded = 55,
        CantConnectXblParty = 56,
        GamertagHasJoinedXblParty = 57,
        GamertagHasLeftXblParty = 58,
        GamerPictureUnlocked = 59,
        AvatarAwardUnlocked = 60,
        JoinedXblParty = 61,
        PleaseReinsertUsbStorageDevice = 62,
        PlayerMuted = 63,
        PlayerUnmuted = 64,
        FlashingChatSymbol = 65,
        Updating = 76
    }

    /// <summary>
    /// One entry of a console directory listing (old XDevkit IXboxFile: Name, IsDirectory).
    /// Name is what the console returned; the old code compared it as the file name for files and as the full path for
    /// directories (see PathExists / FileExists in the old XboxExtensions), so the adapter must return it unchanged.
    /// </summary>
    public sealed class XboxFileEntry
    {
        public string Name { get; set; }

        public bool IsDirectory { get; set; }
    }

    /// <summary>
    /// The console file system (old XDevkit DirectoryFiles, MakeDirectory, DeleteFile, RemoveDirectory, RenameFile, plus the
    /// old extension helpers DirectoryExists and CreateDirectoryRecursive). Console paths look like "Hdd:\Content\".
    /// </summary>
    public interface IXboxConsoleFileSystem
    {
        /// <summary>List a directory. Throws when the directory does not exist (the old DirectoryExists relied on this).</summary>
        IList<XboxFileEntry> ListDirectory(string path);

        /// <summary>True when ListDirectory(path) succeeds. Never throws for a missing directory.</summary>
        bool DirectoryExists(string path);

        void MakeDirectory(string path);

        /// <summary>
        /// Create every missing directory of the path. Old behaviour: "Hdd:\" is rewritten to "\Hdd:\" first, the root is
        /// skipped, and the first failure is logged and ends the loop without throwing.
        /// </summary>
        void CreateDirectoryRecursive(string path);

        void DeleteFile(string path);

        void RemoveDirectory(string path);

        /// <summary>Rename or move (old RenameFile(old, new); used for both files and folders).</summary>
        void RenameFile(string oldPath, string newPath);
    }

    /// <summary>
    /// Console power control (old MainWindow: XboxConsole.Shutdown, XboxConsole.Reboot(string.Empty x3, flags)).
    /// </summary>
    public interface IXboxConsolePower
    {
        void Shutdown();

        void Reboot(XboxRebootMode mode);
    }

    /// <summary>
    /// On-screen notification on the console (old XNotify extension: a "consolefeatures ver=2 type=12" text command).
    /// There is also an older Notify(text) that used a JRPC call to xam.xex ordinal 656; it was unused by the forms.
    /// </summary>
    public interface IXboxNotifier
    {
        void Notify(string text, XNotifyLogo logo);
    }

    /// <summary>
    /// An open console connection. Access is the existing per-form boundary (memory, modules, files, screenshots...);
    /// the other members are the parts the old MainWindow and XboxExtensions used on top of it.
    /// </summary>
    public interface IXboxSession : IXboxConsolePower, IXboxNotifier, IXboxConsoleFileSystem
    {
        IXboxConsoleAccess Access { get; }

        /// <summary>True when the JRPC2 module answered. Without it the memory and call features do not work.</summary>
        bool JrpcAvailable { get; }

        /// <summary>Close the connection (old XboxConsole.CloseConnection(0)). Safe to call twice; never throws.</summary>
        void Close();
    }

    /// <summary>
    /// A console found by a scan.
    /// </summary>
    public sealed class XboxScanResult
    {
        private readonly List<ConsoleProfile> _profiles = new List<ConsoleProfile>();

        /// <summary>Consoles found. Empty on failure.</summary>
        public IList<ConsoleProfile> Profiles
        {
            get { return _profiles; }
        }

        /// <summary>Null on success (finding nothing is a success). The reason otherwise.</summary>
        public string Error { get; set; }

        public bool Succeeded
        {
            get { return Error == null; }
        }
    }

    /// <summary>
    /// Result of a connect attempt.
    /// </summary>
    public sealed class XboxConnectResult
    {
        /// <summary>The open session. Null when Connected is false.</summary>
        public IXboxSession Session { get; set; }

        public bool Connected
        {
            get { return Session != null; }
        }

        /// <summary>
        /// True when the console opened but the JRPC2 module did not answer after both attempts. The old code showed
        /// "You must have JRPC2 module set as a plugin on your console to use some features." and carried on.
        /// </summary>
        public bool JrpcMissing { get; set; }

        /// <summary>Null on success. The reason otherwise.</summary>
        public string Error { get; set; }
    }

    /// <summary>
    /// Finds Xbox 360 consoles and connects to one (old XboxManager, OpenConsole, JRPC.Connect).
    /// Old connect flow, for the adapter: if the profile uses the default login, open XboxManager.DefaultConsole, else open
    /// the console by profile Address. Then JRPC.Connect(console); if that throws, retry once as
    /// JRPC.Connect(console, "default" when UseDefaultLogin, else the profile Address); if the retry throws, the console stays
    /// open with JrpcMissing set.
    /// Old scan flow: XboxManager.Consoles yields console names; each is opened by name; a profile is built with
    /// Platform XBOX360, PlatformType Xbox360FatWhite, UseDefaultLogin true, Name = console.Name, Address = the console IP.
    /// Names already in the settings are skipped by the form, not by the connector.
    /// </summary>
    public interface IXboxConsoleConnector
    {
        /// <summary>Scan for consoles. Does not throw; failures go in the result.</summary>
        XboxScanResult Scan();

        /// <summary>Open a console for a profile. Does not throw; failures go in the result.</summary>
        XboxConnectResult Connect(ConsoleProfile profile);
    }

    /// <summary>
    /// Stub connector: Scan finds nothing and says why, Connect fails with a message. Default of M7Services.Xbox.
    /// </summary>
    public sealed class UnavailableXboxConsoleConnector : IXboxConsoleConnector
    {
        private static readonly UnavailableXboxConsoleConnector _instance = new UnavailableXboxConsoleConnector();

        public static UnavailableXboxConsoleConnector Instance
        {
            get { return _instance; }
        }

        internal const string Message = "Xbox 360 console access is not available in this build. No XDevkit / JRPC adapter is connected.";

        public XboxScanResult Scan()
        {
            return new XboxScanResult { Error = Message };
        }

        public XboxConnectResult Connect(ConsoleProfile profile)
        {
            return new XboxConnectResult { Error = Message };
        }
    }

    /// <summary>
    /// Stub session: every call fails with NotSupportedException, Close does nothing. Access is the existing
    /// UnavailableXboxConsoleAccess. Only reachable by a caller that builds one itself; the stub connector never returns it.
    /// </summary>
    public sealed class UnavailableXboxSession : IXboxSession
    {
        private static readonly UnavailableXboxSession _instance = new UnavailableXboxSession();

        public static UnavailableXboxSession Instance
        {
            get { return _instance; }
        }

        private static Exception NotAvailable()
        {
            return new NotSupportedException(UnavailableXboxConsoleConnector.Message);
        }

        public IXboxConsoleAccess Access
        {
            get { return UnavailableXboxConsoleAccess.Instance; }
        }

        public bool JrpcAvailable
        {
            get { return false; }
        }

        public void Close()
        {
        }

        public void Shutdown()
        {
            throw NotAvailable();
        }

        public void Reboot(XboxRebootMode mode)
        {
            throw NotAvailable();
        }

        public void Notify(string text, XNotifyLogo logo)
        {
            throw NotAvailable();
        }

        public IList<XboxFileEntry> ListDirectory(string path)
        {
            throw NotAvailable();
        }

        public bool DirectoryExists(string path)
        {
            // A missing console means nothing exists; the old DirectoryExists returned false on any failure.
            return false;
        }

        public void MakeDirectory(string path)
        {
            throw NotAvailable();
        }

        public void CreateDirectoryRecursive(string path)
        {
            throw NotAvailable();
        }

        public void DeleteFile(string path)
        {
            throw NotAvailable();
        }

        public void RemoveDirectory(string path)
        {
            throw NotAvailable();
        }

        public void RenameFile(string oldPath, string newPath)
        {
            throw NotAvailable();
        }
    }

    /// <summary>
    /// Typed memory reads and writes over IXboxMemoryAccess. Port of the old XboxExtensions Read*/Write* methods, which
    /// worked on XDevkit DebugTarget.GetMemory/SetMemory. Xbox 360 memory is big-endian; the old code reversed the bytes
    /// of every multi-byte value, and so does this. Plain code, no console dependency beyond ReadBytes/WriteBytes.
    /// Differences from the old code: no shared static scratch buffer (the old one was not thread safe), and exceptions from
    /// ReadBytes/WriteBytes are not rewrapped.
    /// </summary>
    public static class XboxMemoryExtensions
    {
        private static byte[] ReadBigEndian(IXboxMemoryAccess memory, uint address, int count)
        {
            byte[] bytes = memory.ReadBytes(address, count);
            if (bytes == null || bytes.Length < count)
            {
                throw new InvalidOperationException("Could not read " + count + " byte(s) at 0x" + address.ToString("X"));
            }

            byte[] copy = new byte[count];
            Array.Copy(bytes, copy, count);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(copy);
            }

            return copy;
        }

        private static void WriteBigEndian(IXboxMemoryAccess memory, uint address, byte[] littleEndianHostBytes)
        {
            byte[] copy = (byte[])littleEndianHostBytes.Clone();
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(copy);
            }

            memory.WriteBytes(address, copy);
        }

        public static byte ReadByte(this IXboxMemoryAccess memory, uint address)
        {
            return memory.ReadBytes(address, 1)[0];
        }

        public static sbyte ReadSByte(this IXboxMemoryAccess memory, uint address)
        {
            return (sbyte)memory.ReadBytes(address, 1)[0];
        }

        public static bool ReadBool(this IXboxMemoryAccess memory, uint address)
        {
            return memory.ReadBytes(address, 1)[0] != 0;
        }

        public static short ReadInt16(this IXboxMemoryAccess memory, uint address)
        {
            return BitConverter.ToInt16(ReadBigEndian(memory, address, 2), 0);
        }

        public static ushort ReadUInt16(this IXboxMemoryAccess memory, uint address)
        {
            return BitConverter.ToUInt16(ReadBigEndian(memory, address, 2), 0);
        }

        public static int ReadInt32(this IXboxMemoryAccess memory, uint address)
        {
            return BitConverter.ToInt32(ReadBigEndian(memory, address, 4), 0);
        }

        public static uint ReadUInt32(this IXboxMemoryAccess memory, uint address)
        {
            return BitConverter.ToUInt32(ReadBigEndian(memory, address, 4), 0);
        }

        public static long ReadInt64(this IXboxMemoryAccess memory, uint address)
        {
            return BitConverter.ToInt64(ReadBigEndian(memory, address, 8), 0);
        }

        public static ulong ReadUInt64(this IXboxMemoryAccess memory, uint address)
        {
            return BitConverter.ToUInt64(ReadBigEndian(memory, address, 8), 0);
        }

        public static float ReadFloat(this IXboxMemoryAccess memory, uint address)
        {
            return BitConverter.ToSingle(ReadBigEndian(memory, address, 4), 0);
        }

        /// <summary>
        /// Read an ASCII string of at most maxLength bytes, cut at the first NUL (old default buffer: 32 bytes).
        /// </summary>
        public static string ReadString(this IXboxMemoryAccess memory, uint address, int maxLength = 32)
        {
            byte[] bytes = memory.ReadBytes(address, maxLength);
            int length = Array.IndexOf(bytes, (byte)0);
            if (length < 0)
            {
                length = bytes.Length;
            }

            return System.Text.Encoding.ASCII.GetString(bytes, 0, length);
        }

        public static void WriteByte(this IXboxMemoryAccess memory, uint address, byte value)
        {
            memory.WriteBytes(address, new[] { value });
        }

        public static void WriteSByte(this IXboxMemoryAccess memory, uint address, sbyte value)
        {
            memory.WriteBytes(address, new[] { (byte)value });
        }

        public static void WriteInt16(this IXboxMemoryAccess memory, uint address, short value)
        {
            WriteBigEndian(memory, address, BitConverter.GetBytes(value));
        }

        public static void WriteUInt16(this IXboxMemoryAccess memory, uint address, ushort value)
        {
            WriteBigEndian(memory, address, BitConverter.GetBytes(value));
        }

        public static void WriteInt32(this IXboxMemoryAccess memory, uint address, int value)
        {
            WriteBigEndian(memory, address, BitConverter.GetBytes(value));
        }

        public static void WriteUInt32(this IXboxMemoryAccess memory, uint address, uint value)
        {
            WriteBigEndian(memory, address, BitConverter.GetBytes(value));
        }

        public static void WriteInt64(this IXboxMemoryAccess memory, uint address, long value)
        {
            WriteBigEndian(memory, address, BitConverter.GetBytes(value));
        }

        public static void WriteUInt64(this IXboxMemoryAccess memory, uint address, ulong value)
        {
            WriteBigEndian(memory, address, BitConverter.GetBytes(value));
        }

        public static void WriteFloat(this IXboxMemoryAccess memory, uint address, float value)
        {
            WriteBigEndian(memory, address, BitConverter.GetBytes(value));
        }

        /// <summary>
        /// Write an ASCII string and a terminating NUL (old WriteString: each char cast to byte, then 0).
        /// </summary>
        public static void WriteString(this IXboxMemoryAccess memory, uint address, string value)
        {
            byte[] bytes = new byte[value.Length + 1];
            for (int i = 0; i < value.Length; i++)
            {
                bytes[i] = (byte)value[i];
            }

            memory.WriteBytes(address, bytes);
        }
    }
}
