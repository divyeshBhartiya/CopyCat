using System.Runtime.InteropServices;
using System.Security.AccessControl;
using Serilog;

namespace CopyCat;

internal static class CopyServiceHelper
{
    /// <summary>
    /// Get a unique file name by appending a number to the file name if it already exists.
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    internal static string GetUniqueFileName(string filePath)
    {
        string directory = Path.GetDirectoryName(filePath)!;
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(filePath);

        int count = 1;
        string newFilePath = filePath;
        while (File.Exists(newFilePath))
        {
            newFilePath = Path.Combine(directory, $"{fileName}({count}){extension}");
            count++;
        }
        return newFilePath;
    }

    /// <summary>
    /// Handle duplicate files based on the overwrite and renameOnConflict options.
    /// </summary>
    /// <param name="overWrite"></param>
    /// <param name="renameOnConflict"></param>
    /// <param name="correlationId"></param>
    /// <param name="destFile"></param>
    internal static bool HandleDuplicateFile(bool overWrite, bool renameOnConflict, string correlationId, ref string destFile)
    {
        if (File.Exists(destFile))
        {
            if (overWrite)
            {
                Log.Information("🔄 Overwriting existing file: {File}. CorrelationId: {CorrelationId}", destFile, correlationId);
            }
            else if (renameOnConflict)
            {
                string newDestFile = GetUniqueFileName(destFile);
                Log.Information("📂 File conflict detected. Renaming {File} to {NewFile}. CorrelationId: {CorrelationId}", destFile, newDestFile, correlationId);
                destFile = newDestFile;
            }
            else
            {
                Log.Warning("⏭️ Skipping existing file: {File}. CorrelationId: {CorrelationId}", destFile, correlationId);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Preserve directory metadata such as timestamps and attributes.
    /// </summary>
    /// <param name="srcDir"></param>
    /// <param name="destDir"></param>
    internal static void PreserveDirectoryMetadata(string srcDir, string destDir)
    {
        DirectoryInfo srcDirInfo = new(srcDir);
        DirectoryInfo destDirInfo = new(destDir)
        {
            Attributes = srcDirInfo.Attributes,
            CreationTime = srcDirInfo.CreationTime,
            LastWriteTime = srcDirInfo.LastWriteTime,
            LastAccessTime = srcDirInfo.LastAccessTime
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            DirectorySecurity srcAcl = srcDirInfo.GetAccessControl();
            destDirInfo.SetAccessControl(srcAcl);
        }
    }

    /// <summary>
    /// Preserve file metadata such as timestamps and attributes.
    /// </summary>
    /// <param name="srcFile"></param>
    /// <param name="destFile"></param>
    internal static void PreserveFileMetadata(string srcFile, string destFile)
    {
        var srcCreationTime = File.GetCreationTime(srcFile);
        var srcLastWriteTime = File.GetLastWriteTime(srcFile);
        var srcLastAccessTime = File.GetLastAccessTime(srcFile);

        File.SetAttributes(destFile, File.GetAttributes(srcFile));
        File.SetCreationTime(destFile, srcCreationTime);
        File.SetLastWriteTime(destFile, srcLastWriteTime);
        File.SetLastAccessTime(destFile, srcLastAccessTime);

        Log.Information("Preserved timestamps for {File}. CreationTime: {CreationTime}, LastWriteTime: {LastWriteTime}, LastAccessTime: {LastAccessTime}",
            destFile, srcCreationTime, srcLastWriteTime, srcLastAccessTime);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            FileInfo srcFileInfo = new(srcFile);
            FileInfo destFileInfo = new(destFile);
            FileSecurity srcAcl = srcFileInfo.GetAccessControl();
            destFileInfo.SetAccessControl(srcAcl);
        }
    }
}
