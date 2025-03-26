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

    private async Task CopyContentsAsync(string srcDir, string destDir, bool overwrite, bool includeHidden, CancellationToken token)
    {
        if (!Directory.Exists(srcDir))
        {
            throw new DirectoryNotFoundException($"Source directory '{srcDir}' not found.");
        }

        Directory.CreateDirectory(destDir);
        Log.Information("📂 Processing directory. CorrelationId: {CorrelationId}", _correlationId);

        var files = Directory.EnumerateFiles(srcDir, "*", SearchOption.TopDirectoryOnly)
                             .Where(f => includeHidden || (File.GetAttributes(f) & FileAttributes.Hidden) == 0);
        var directories = Directory.EnumerateDirectories(srcDir);

        try
        {
            await Task.WhenAll
                (
                    ProcessFilesAsync(destDir, files, token),
                    ProcessDirectoriesAsync(destDir, overwrite, includeHidden, directories, token)
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

    private async Task ProcessDirectoriesAsync(string destDir, bool overwrite, bool includeHidden, IEnumerable<string> directories, CancellationToken token)
    {
        var directoryTasks = directories.Select(async srcSubDir =>
        {
            token.ThrowIfCancellationRequested();
            string destSubDir = Path.Combine(destDir, Path.GetFileName(srcSubDir));
            await CopyContentsAsync(srcSubDir, destSubDir, overwrite, includeHidden, token);
        });

        await Task.WhenAll(directoryTasks);
    }

    private async Task ProcessFilesAsync(string destDir, IEnumerable<string> files, CancellationToken token)
    {
        await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token }, async (file, token) =>
        {
            try
            {
                token.ThrowIfCancellationRequested();

                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                Log.Information("📄 Copying file. CorrelationId: {CorrelationId}", _correlationId);

                using var sourceStream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var destinationStream = File.Create(destFile);

                var buffer = new byte[81920];
                int bytesRead;
                while ((bytesRead = await sourceStream.ReadAsync(buffer, token)) > 0)
                {
                    await destinationStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                }

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
        });
    }
}
