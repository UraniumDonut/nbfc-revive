using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace StagWare.Hardware
{
    /// <summary>
    /// Provides access to the ACPI embedded controller ports (0x62/0x66) via the PawnIO driver
    /// and its signed LpcACPIEC module (https://pawnio.eu).
    /// </summary>
    public sealed class PawnIOEmbeddedController : IDisposable
    {
        #region Constants

        const string DevicePath = @"\\?\GLOBALROOT\Device\PawnIO";
        const string ModuleResourceName = "StagWare.Hardware.Resources.LpcACPIEC.bin";

        // Other EC tools (e.g. LibreHardwareMonitor, HWiNFO) use the same mutex
        const string EcMutexName = @"Global\Access_EC";

        const uint DeviceType = 41394u << 16;
        const uint IoctlLoadBinary = DeviceType | (0x821 << 2);
        const uint IoctlExecute = DeviceType | (0x841 << 2);
        const int FunctionNameLength = 32;

        const uint GenericRead = 0x80000000;
        const uint GenericWrite = 0x40000000;
        const uint FileShareRead = 0x00000001;
        const uint FileShareWrite = 0x00000002;
        const uint OpenExisting = 3;

        #endregion

        #region Private Fields

        SafeFileHandle handle;
        Mutex ecMutex;

        #endregion

        #region Constructor

        public PawnIOEmbeddedController()
        {
            this.handle = CreateFile(DevicePath, GenericRead | GenericWrite,
                FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

            if (this.handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                this.handle.Dispose();

                throw new PlatformNotSupportedException(
                    "Could not open the PawnIO driver. Make sure PawnIO is installed (https://pawnio.eu).",
                    new Win32Exception(error));
            }

            try
            {
                byte[] module = GetModuleBinary();

                if (!DeviceIoControl(this.handle, IoctlLoadBinary, module, (uint)module.Length,
                    null, 0, out _, IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(),
                        "Could not load the PawnIO LpcACPIEC module.");
                }

                this.ecMutex = CreateOrOpenMutex(EcMutexName);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        #endregion

        #region Public Methods

        public bool WaitEcMutex(int timeout)
        {
            try
            {
                return this.ecMutex.WaitOne(timeout);
            }
            catch (AbandonedMutexException)
            {
                // The previous owner died, we own the mutex now
                return true;
            }
        }

        public void ReleaseEcMutex()
        {
            this.ecMutex.ReleaseMutex();
        }

        public byte ReadPort(int port)
        {
            long[] result = Execute("ioctl_pio_read", new long[] { port }, 1);
            return (byte)result[0];
        }

        public void WritePort(int port, byte value)
        {
            Execute("ioctl_pio_write", new long[] { port, value }, 0);
        }

        public void Dispose()
        {
            if (this.ecMutex != null)
            {
                this.ecMutex.Dispose();
                this.ecMutex = null;
            }

            if (this.handle != null)
            {
                this.handle.Dispose();
                this.handle = null;
            }
        }

        #endregion

        #region Private Methods

        private long[] Execute(string function, long[] input, int outputLength)
        {
            byte[] inBuffer = new byte[FunctionNameLength + (input.Length * sizeof(long))];
            Encoding.ASCII.GetBytes(function, 0, function.Length, inBuffer, 0);
            Buffer.BlockCopy(input, 0, inBuffer, FunctionNameLength, input.Length * sizeof(long));

            byte[] outBuffer = new byte[outputLength * sizeof(long)];

            if (!DeviceIoControl(this.handle, IoctlExecute, inBuffer, (uint)inBuffer.Length,
                outBuffer, (uint)outBuffer.Length, out uint bytesReturned, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "PawnIO call " + function + " failed.");
            }

            if (bytesReturned < outBuffer.Length)
            {
                throw new IOException("PawnIO call " + function + " returned too little data.");
            }

            long[] output = new long[outputLength];
            Buffer.BlockCopy(outBuffer, 0, output, 0, outBuffer.Length);

            return output;
        }

        private static byte[] GetModuleBinary()
        {
            using (Stream stream = typeof(PawnIOEmbeddedController).Assembly
                .GetManifestResourceStream(ModuleResourceName))
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static Mutex CreateOrOpenMutex(string name)
        {
            try
            {
                // Allow everyone to use the mutex, other EC tools might not run as SYSTEM
                var security = new MutexSecurity();
                security.AddAccessRule(new MutexAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    MutexRights.Synchronize | MutexRights.Modify,
                    AccessControlType.Allow));

                return new Mutex(false, name, out _, security);
            }
            catch (UnauthorizedAccessException)
            {
                // The mutex already exists and we may not change its ACL
                return Mutex.OpenExisting(name, MutexRights.Synchronize | MutexRights.Modify);
            }
        }

        #endregion

        #region P/Invoke

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode,
            IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool DeviceIoControl(SafeFileHandle device, uint ioControlCode,
            byte[] inBuffer, uint inBufferSize, byte[] outBuffer, uint outBufferSize,
            out uint bytesReturned, IntPtr overlapped);

        #endregion
    }
}
