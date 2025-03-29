﻿using System.Runtime.InteropServices;
using Serilog;

namespace CopyCat;

public class CopyService(CopyOptions options, ProgressReporter progressReporter, string correlationId)
{
    private readonly ProgressReporter _progressReporter = progressReporter;
    private readonly CopyOptions _options = options;
    private readonly string _correlationId = correlationId;

    public async Task CopyAsync(CancellationToken cancellationToken)
    {
        await CopyContentsAsync(_options.SourcePath, _options.DestinationPath, _options.Overwrite, _options.IncludeHidden, cancellationToken);
    }

    private async Task CopyContentsAsync(string srcDir, string destDir, bool overwrite, bool includeHidden, CancellationToken token, int depth = 0)
    {
        if (!Directory.Exists(srcDir))
        {
            throw new DirectoryNotFoundException($"Source directory '{srcDir}' not found.");
        }

        if (SymbolicLinkHelper.IsSymbolicLink(srcDir))
        {
            var target = SymbolicLinkHelper.GetSymbolicLinkTarget(srcDir);
            Log.Information("🔗 Symbolic link detected: {Directory} -> {Target}. CorrelationId: {CorrelationId}", srcDir, target);

            if (!string.IsNullOrEmpty(target) && Path.GetFullPath(target).StartsWith(Path.GetFullPath(srcDir)))
            {
                Log.Error("♻️ Cyclic symbolic link detected: {Directory} -> {Target}. Skipping to prevent infinite loop.");
                throw new IOException($"Cyclic symbolic link detected: {srcDir} -> {target}");
            }
        }

        if (depth > _options.MaxDepth)
        {
            Log.Warning("📛 Max directory depth reached: {Depth}. Skipping {Directory}. CorrelationId: {CorrelationId}", depth, srcDir, _correlationId);
            return;
        }
        Log.Information("✅ Copying directory {Directory} at depth {Depth}", srcDir, depth);

        // Create destination directory
        Directory.CreateDirectory(destDir);
        CopyServiceHelper.PreserveDirectoryMetadata(srcDir, destDir);  // Preserve directory timestamps & attributes

        Log.Information("📂 Processing directory: {Directory}. CorrelationId: {CorrelationId}", srcDir, _correlationId);

        var files = Directory.EnumerateFiles(srcDir).Where(f => includeHidden || (File.GetAttributes(f) & FileAttributes.Hidden) == 0);
        var directories = Directory.EnumerateDirectories(srcDir);

        try
        {
            await Task.WhenAll
            (
                ProcessFilesAsync(destDir, files, token),
                ProcessDirectoriesAsync(destDir, overwrite, includeHidden, directories, depth + 1, token)
            );
        }
        catch (OperationCanceledException)
        {
            Log.Warning("⏹️ Copy operation cancelled. CorrelationId: {CorrelationId}", _correlationId);
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Error while copying directory. CorrelationId: {CorrelationId}", _correlationId);
            throw;
        }
    }

    private async Task ProcessDirectoriesAsync(string destDir, bool overwrite, bool includeHidden, IEnumerable<string> directories, int depth, CancellationToken token)
    {
        var directoryTasks = directories.Select(async srcSubDir =>
        {
            token.ThrowIfCancellationRequested();
            string destSubDir = Path.Combine(destDir, Path.GetFileName(srcSubDir));
            await CopyContentsAsync(srcSubDir, destSubDir, overwrite, includeHidden, token, depth);
        });

        await Task.WhenAll(directoryTasks);
    }

    private async Task ProcessFilesAsync(string destDir, IEnumerable<string> files, CancellationToken token)
    {
        await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token }, async (file, token) =>
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            try
            {
                token.ThrowIfCancellationRequested();
                bool shouldSkip = CopyServiceHelper.HandleDuplicateFile(_options.Overwrite, _options.RenameOnConflict, _correlationId, ref destFile);
                if (shouldSkip)
                {
                    return;
                }
                if (SymbolicLinkHelper.IsSymbolicLink(file))
                {
                    SymbolicLinkHelper.HandleSymbolicLink(file, destFile, _correlationId);
                }
                else
                {
                    await HandleFileCopying(file, destFile, token);
                }
            }
            catch (OperationCanceledException)
            {
                Log.Warning("⏹️ Copy operation cancelled while processing file. CorrelationId: {CorrelationId}", _correlationId);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Error while copying file. CorrelationId: {CorrelationId}", _correlationId);
                throw new IOException($"Error while copying file '{file}' to '{destFile}'.", ex);
            }
        });
    }

    private async Task HandleFileCopying(string file, string destFile, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();

            if (File.Exists(destFile))
            {
                File.SetAttributes(destFile, FileAttributes.Normal);
                File.Delete(destFile);
            }

            Log.Information("📄 Copying file. CorrelationId: {CorrelationId}", _correlationId);
            using var sourceStream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var destinationStream = File.Create(destFile);
            var buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = await sourceStream.ReadAsync(buffer, token)) > 0)
            {
                await destinationStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
            }

            CopyServiceHelper.PreserveFileMetadata(file, destFile);  // Preserve timestamps & attributes

            _progressReporter.FileCopied();
        }
        catch (OperationCanceledException)
        {
            Log.Warning("⏹️ Copy operation cancelled while processing file. CorrelationId: {CorrelationId}", _correlationId);
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Error while copying file. CorrelationId: {CorrelationId}", _correlationId);
            throw;
        }
    }
}
