using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ZO.DMM.AppNF
{
    public partial class ModItem
    {
        public partial class Files
        {
            public static string GetJunctionTarget(string junctionPoint)
            {
                var directoryInfo = new DirectoryInfo(junctionPoint);
                if ((directoryInfo.Attributes & FileAttributes.ReparsePoint) == 0)
                {
                    return string.Empty;
                }

                SafeFileHandle handle = CreateFile(
                    junctionPoint,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    IntPtr.Zero,
                    FileMode.Open,
                    FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT,
                    IntPtr.Zero);

                if (handle.IsInvalid)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new IOException($"Unable to open junction point. Error code: {errorCode}");
                }

                try
                {
                    var reparseDataBuffer = new byte[REPARSE_DATA_BUFFER_HEADER_SIZE + MAXIMUM_REPARSE_DATA_BUFFER_SIZE];
                    uint bytesReturned;
                    if (!DeviceIoControl(
                        handle,
                        FSCTL_GET_REPARSE_POINT,
                        IntPtr.Zero,
                        0,
                        reparseDataBuffer,
                        reparseDataBuffer.Length,
                        out bytesReturned,
                        IntPtr.Zero))
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        throw new IOException($"Unable to get information about junction point. Error code: {errorCode}");
                    }

                    var target = ParseReparsePoint(reparseDataBuffer);
                    return target;
                }
                finally
                {
                    handle.Close();
                }
            }

            private static string ParseReparsePoint(byte[] reparseDataBuffer)
            {
                GCHandle handle = GCHandle.Alloc(reparseDataBuffer, GCHandleType.Pinned);
                try
                {
                    var reparseData = (REPARSE_DATA_BUFFER)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(REPARSE_DATA_BUFFER));
                    if (reparseData.ReparseTag != IO_REPARSE_TAG_MOUNT_POINT)
                    {
                        throw new IOException("The reparse point is not a junction point.");
                    }

                    if (reparseData.PathBuffer == null)
                    {
                        throw new IOException("The reparse point buffer is null.");
                    }

                    var target = Encoding.Unicode.GetString(reparseData.PathBuffer, reparseData.SubstituteNameOffset, reparseData.SubstituteNameLength);
                    if (target.StartsWith("\\??\\"))
                    {
                        target = target.Substring(4);
                    }

                    return target;
                }
                finally
                {
                    handle.Free();
                }
            }

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            private static extern SafeFileHandle CreateFile(
                string lpFileName,
                [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
                [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
                IntPtr lpSecurityAttributes,
                [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
                [MarshalAs(UnmanagedType.U4)] uint dwFlagsAndAttributes,
                IntPtr hTemplateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool DeviceIoControl(
                SafeFileHandle hDevice,
                uint dwIoControlCode,
                IntPtr lpInBuffer,
                uint nInBufferSize,
                [Out] byte[] lpOutBuffer,
                int nOutBufferSize,
                out uint lpBytesReturned,
                IntPtr lpOverlapped);

            private const uint FSCTL_GET_REPARSE_POINT = 0x000900A8;
            private const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003;
            private const int REPARSE_DATA_BUFFER_HEADER_SIZE = 8;
            private const int MAXIMUM_REPARSE_DATA_BUFFER_SIZE = 16384;
            private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
            private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;

            [StructLayout(LayoutKind.Sequential)]
            private struct REPARSE_DATA_BUFFER
            {
                public uint ReparseTag;
                public ushort ReparseDataLength;
                public ushort Reserved;
                public ushort SubstituteNameOffset;
                public ushort SubstituteNameLength;
                public ushort PrintNameOffset;
                public ushort PrintNameLength;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAXIMUM_REPARSE_DATA_BUFFER_SIZE)]
                public byte[] PathBuffer;
            }
        }
    }
}
