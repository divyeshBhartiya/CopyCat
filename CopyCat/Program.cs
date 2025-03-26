using Serilog;
using Serilog.Context;
using Serilog.Formatting.Json;
using System;

namespace CopyCat;

public class Program
{
    public static async Task Main(string[] args)
    {
        // 🔹 Generate a unique Correlation ID for this copy operation
        string correlationId = Guid.NewGuid().ToString();

        // 🔹 Configure Serilog with JSON logs and include Correlation ID
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithProperty("CorrelationId", correlationId)  // Attach Correlation ID
            .WriteTo.Console(new JsonFormatter())
            .WriteTo.File(new JsonFormatter(), "logs/copycat.json", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7) // Log rotation
            .CreateLogger();

        Log.Information("🐱 CopyCat - A simple directory copy utility started. CorrelationId: {CorrelationId}", correlationId);

        try
        {
            var copyOptions = CopyOptions.Parse(args);
            Log.Information("Parsed options: {@CopyOptions}", copyOptions);

            int totalFiles = FileCounter.CountTotalFiles(copyOptions.SourcePath);
            Log.Information("Total files to copy: {TotalFiles}", totalFiles);

            var progress = new Progress<int>(percent =>
            {
                Log.Information("Progress Update: {Progress}%", percent);
                Console.WriteLine($"📊 Progress: {percent}%");
            });

            var progressReporter = new ProgressReporter(totalFiles, progress, correlationId);
            var copyService = new CopyService(copyOptions, progressReporter, correlationId);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Log.Warning("⏹️ Cancellation requested. CorrelationId: {CorrelationId}", correlationId);
                cts.Cancel();
                eventArgs.Cancel = true;
            };

            Log.Information("🚀 Starting copy from '{Source}' to '{Destination}'. CorrelationId: {CorrelationId}",
                copyOptions.SourcePath, copyOptions.DestinationPath, correlationId);

            await copyService.CopyAsync(cts.Token);

            Log.Information("✅ Copy completed successfully! CorrelationId: {CorrelationId}", correlationId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ An error occurred: {Message}. CorrelationId: {CorrelationId}", ex.Message, correlationId);
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
