namespace CopyCat.Tests
{
    public class ProgressReporterTests
    {
        [Fact]
        public void FileCopied_ReportsProgressCorrectly()
        {
            // Arrange
            int reportedProgress = 0;
            // ManualResetEventSlim is used to block the test execution until the expected progress is reported
            var progressReported = new ManualResetEventSlim(false);
            var progress = new Progress<int>(p =>
            {
                reportedProgress = p;
                Console.WriteLine($"Progress reported: {p}%");
                if (reportedProgress == 60)
                {
                    // Signal that the expected progress has been reported
                    progressReported.Set();
                }
            });
            var reporter = new ProgressReporter(5, progress, Guid.NewGuid().ToString());

            // Act
            reporter.FileCopied();
            reporter.FileCopied();
            reporter.FileCopied();

            // Wait for the progress to be reported
            progressReported.Wait();

            // Assert
            Assert.Equal(60, reportedProgress); // 3 out of 5 files copied → 60%
        }
    }
}