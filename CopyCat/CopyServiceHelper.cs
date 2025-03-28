﻿using System.Runtime.InteropServices;
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

    /// <summary>
    /// Copy a file using a single thread I/O operation.
    /// </summary>
    /// <param name="sourceFile"></param>
    /// <param name="destinationFile"></param>
    /// <param name="fileSize"></param>
    /// <param name="correlationId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    internal static async Task CopyFileSingleThreadedAsync(string sourceFile, string destinationFile, long fileSize, string correlationId, long resumeFrom = 0, CancellationToken token = default)
    {
        Log.Information("📄 Copying (Single-threaded) {File}. Size: {Size} bytes. Resuming from: {ResumeBytes} bytes. CorrelationId: {CorrelationId}",
            sourceFile, fileSize, resumeFrom, correlationId);

        const int bufferSize = 81920; // 80KB buffer
        long copiedBytes = resumeFrom;

        using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var destinationStream = new FileStream(destinationFile, FileMode.Append, FileAccess.Write, FileShare.None);

        sourceStream.Seek(resumeFrom, SeekOrigin.Begin);
        destinationStream.Seek(resumeFrom, SeekOrigin.Begin);

        byte[] buffer = new byte[bufferSize];
        int bytesRead;

        while ((bytesRead = await sourceStream.ReadAsync(buffer, token)) > 0)
        {
            await destinationStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
            copiedBytes += bytesRead;

            // ✅ Log progress every 10%
            int fileProgress = (int)((double)copiedBytes / fileSize * 100);
            if (fileProgress % 10 == 0)
            {
                Log.Information("📊 {Progress}% copied for {File}. CorrelationId: {CorrelationId}",
                    fileProgress, sourceFile, correlationId);
            }
        }
    }

    /// <summary>
    /// Copy a file using multiple threads for I/O operations.
    /// </summary>
    /// <param name="sourceFile"></param>
    /// <param name="destinationFile"></param>
    /// <param name="fileSize"></param>
    /// <param name="correlationId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    internal static async Task CopyFileMultiThreadedAsync(string sourceFile, string destinationFile, long fileSize, string correlationId, long resumeFrom = 0, CancellationToken token = default)
    {
        Log.Information("📄 Copying (Multi-threaded) {File}. Size: {Size} bytes. Resuming from: {ResumeBytes} bytes. CorrelationId: {CorrelationId}",
            sourceFile, fileSize, resumeFrom, correlationId);

        long copiedBytes = resumeFrom;
        const int chunkSize = 4 * 1024 * 1024; // 4MB chunks
        int numChunks = (int)Math.Ceiling((double)fileSize / chunkSize);
        SemaphoreSlim semaphore = new(4); // Limit concurrency to 4 threads

        using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var destinationStream = new FileStream(destinationFile, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

        var tasks = Enumerable.Range(0, numChunks).Select(async i =>
        {
            long offset = i * chunkSize;
            if (offset < resumeFrom) return; // Skip already copied chunks

            await semaphore.WaitAsync(token);
            try
            {
                int currentChunkSize = (int)Math.Min(chunkSize, fileSize - offset);
                byte[] buffer = new byte[currentChunkSize];

                token.ThrowIfCancellationRequested();

                lock (sourceStream)
                {
                    sourceStream.Seek(offset, SeekOrigin.Begin);
                    sourceStream.ReadExactly(buffer, 0, currentChunkSize);
                }

                lock (destinationStream)
                {
                    destinationStream.Seek(offset, SeekOrigin.Begin);
                    destinationStream.Write(buffer, 0, currentChunkSize);
                }

                Interlocked.Add(ref copiedBytes, currentChunkSize);

                // ✅ Log progress every 10%
                int fileProgress = (int)((double)copiedBytes / fileSize * 100);
                if (fileProgress % 10 == 0)
                {
                    Log.Information("📊 {Progress}% copied for {File}. CorrelationId: {CorrelationId}",
                        fileProgress, sourceFile, correlationId);
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }
}
