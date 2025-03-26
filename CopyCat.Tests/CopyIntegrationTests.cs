namespace CopyCat.Tests
{
    public class CopyIntegrationTests : IDisposable
    {
        private readonly string testSourceDir = Path.Combine(Path.GetTempPath(), "CopyCatIntegrationTestSource");
        private readonly string testDestDir = Path.Combine(Path.GetTempPath(), "CopyCatIntegrationTestDest");

        public CopyIntegrationTests()
        {
            Cleanup();
            Directory.CreateDirectory(testSourceDir);
            Directory.CreateDirectory(Path.Combine(testSourceDir, "SubDir"));
            File.WriteAllText(Path.Combine(testSourceDir, "file1.txt"), "File 1");
            File.WriteAllText(Path.Combine(testSourceDir, "SubDir", "file2.txt"), "File 2");
        }

        [Fact]
        public async Task CopyDirectoryAsync_CopiesAllFilesAndDirectories()
        {
            // Arrange
            var options = new CopyOptions(testSourceDir, testDestDir, Overwrite: true);
            int totalFiles = FileCounter.CountTotalFiles(testSourceDir);
            var progress = new Progress<int>(_ => { }); // Dummy progress
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(totalFiles, progress, correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act
            await service.CopyAsync(CancellationToken.None);

            // Assert
            Assert.True(Directory.Exists(testDestDir));
            Assert.True(File.Exists(Path.Combine(testDestDir, "file1.txt")));
            Assert.True(Directory.Exists(Path.Combine(testDestDir, "SubDir")));
            Assert.True(File.Exists(Path.Combine(testDestDir, "SubDir", "file2.txt")));
        }

        [Fact]
        public async Task CopyDirectoryAsync_CopiesFilesInParallel()
        {
            // Arrange
            var options = new CopyOptions(testSourceDir, testDestDir, Overwrite: true, Parallel: true);
            int totalFiles = FileCounter.CountTotalFiles(testSourceDir);
            var progress = new Progress<int>(_ => { });
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(totalFiles, progress, correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act
            await service.CopyAsync(CancellationToken.None);

            // Assert
            Assert.True(Directory.Exists(testDestDir));
            Assert.True(File.Exists(Path.Combine(testDestDir, "file1.txt")));
            Assert.True(File.Exists(Path.Combine(testDestDir, "SubDir", "file2.txt")));
        }

        [Fact]
        public async Task CopyDirectoryAsync_OverwritesExistingFiles()
        {
            // Arrange
            Directory.CreateDirectory(testDestDir);
            await File.WriteAllTextAsync(Path.Combine(testDestDir, "file1.txt"), "Old Data");

            var options = new CopyOptions(testSourceDir, testDestDir, Overwrite: true);
            int totalFiles = FileCounter.CountTotalFiles(testSourceDir);
            var progress = new Progress<int>(_ => { });
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(totalFiles, progress, correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act
            await service.CopyAsync(CancellationToken.None);

            // Assert
            string copiedContent = await File.ReadAllTextAsync(Path.Combine(testDestDir, "file1.txt"));
            Assert.Equal("File 1", copiedContent);
        }

        public void Dispose()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (Directory.Exists(testSourceDir))
                Directory.Delete(testSourceDir, true);
            if (Directory.Exists(testDestDir))
                Directory.Delete(testDestDir, true);
        }
    }
}
