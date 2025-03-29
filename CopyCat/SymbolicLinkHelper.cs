using System.Runtime.InteropServices;
using Serilog;

namespace CopyCat
{
    public static partial class SymbolicLinkHelper
    {
        private const int SYMBOLIC_LINK_FLAG_FILE = 0;
        private const int SYMBOLIC_LINK_FLAG_DIRECTORY = 1;

        public static void HandleSymbolicLink(string file, string destFile, string correlationId)
        {
            // 🔹 Check if file is a symbolic link
            if (IsSymbolicLink(file))
            {
                var symlinkTarget = GetSymbolicLinkTarget(file);
                Log.Information("🔗 Symbolic link detected: {File} -> {LinkTarget}. CorrelationId: {CorrelationId}", file, symlinkTarget, correlationId);
                if (!CreateSymbolicLink(destFile, symlinkTarget, Directory.Exists(symlinkTarget)))
                {
                    throw new IOException($"Failed to create symbolic link: {destFile} -> {symlinkTarget}");
                }
            }
        }

        public static bool CreateSymbolicLink(string symlinkPath, string targetPath, bool isDirectory)
        {
            // Check if the symbolic link path already exists and delete it if it does
            if (File.Exists(symlinkPath) || Directory.Exists(symlinkPath))
            {
                try
                {
                    File.Delete(symlinkPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete existing symbolic link path. Exception: {ex.Message}");
                    return false;
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                bool result = CreateSymbolicLinkW(symlinkPath, targetPath, isDirectory ? SYMBOLIC_LINK_FLAG_DIRECTORY : SYMBOLIC_LINK_FLAG_FILE);
                if (!result)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    Console.WriteLine($"Failed to create symbolic link. Error code: {errorCode}");
                }
                else
                {
                    Console.WriteLine($"Successfully created symbolic link: {symlinkPath} -> {targetPath}");
                }
                return result;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                int result = symlink(targetPath, symlinkPath);
                if (result != 0)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    Console.WriteLine($"Failed to create symbolic link. Error code: {errorCode}");
                }
                else
                {
                    Console.WriteLine($"Successfully created symbolic link: {symlinkPath} -> {targetPath}");
                }
                return result == 0;
            }

            throw new PlatformNotSupportedException("Symbolic links are not supported on this platform.");
        }

        /// <summary>
        /// Uses Windows API to create a symbolic link.
        /// </summary>
        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CreateSymbolicLinkW(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);

        /// <summary>
        /// Uses Linux/macOS API to create a symbolic link.
        /// </summary>
        [LibraryImport("libc", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        private static partial int symlink(string target, string symlinkPath);

        public static string GetSymbolicLinkTarget(string filePath)
        {
            return new FileInfo(filePath).LinkTarget ?? string.Empty;
        }

        public static bool IsSymbolicLink(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.ReparsePoint) != 0;
        }
    }
}