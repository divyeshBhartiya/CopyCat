using Serilog;

namespace CopyCat
{
    public class ProgressReporter(int totalFiles, IProgress<int> progress, string correlationId)
    {
        private readonly int _totalFiles = totalFiles;
        private int _copiedFiles = 0;
        private readonly IProgress<int> _progress = progress;
        private readonly string _correlationId = correlationId;

        public void FileCopied()
        {
            _copiedFiles++;
            int progressPercentage = (int)((double)_copiedFiles / _totalFiles * 100);

            Log.Information("📊 Progress Update. CorrelationId: {CorrelationId}", _correlationId);
            Log.Information("📊 {Copied}/{TotalFiles} files copied ({Progress}%). CorrelationId: {CorrelationId}",
                _copiedFiles, _totalFiles, progressPercentage, _correlationId);

            _progress.Report(progressPercentage);
        }
    }
}
