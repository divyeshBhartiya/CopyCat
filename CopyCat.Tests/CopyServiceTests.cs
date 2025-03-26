using Moq;

namespace CopyCat.Tests
{
    public class CopyServiceTests
    {
        private readonly string testSourceDir = Path.Combine(Path.GetTempPath(), "CopyCatTestSource");
        private readonly string testDestDir = Path.Combine(Path.GetTempPath(), "CopyCatTestDest");

        public CopyServiceTests()
        {
            Cleanup();
            Directory.CreateDirectory(testSourceDir);
            File.WriteAllText(Path.Combine(testSourceDir, "test.txt"), "Hello World!");
        }

        [Fact]
        public async Task CopyDirectoryAsync_CopiesFilesSuccessfully()
        {
            // Arrange
            var options = new CopyOptions(testSourceDir, testDestDir, Overwrite: true);
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(1, new Progress<int>(), correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act
            await service.CopyAsync(CancellationToken.None);

            // Assert
            string copiedFile = Path.Combine(testDestDir, "test.txt");
            Assert.True(File.Exists(copiedFile));
            Assert.Equal("Hello World!", await File.ReadAllTextAsync(copiedFile));
        }

        [Fact]
        public async Task CopyDirectoryAsync_ThrowsIfSourceNotExists()
        {
            // Arrange
            var options = new CopyOptions("C:\\InvalidPath", testDestDir);
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(1, new Progress<int>(), correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act & Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.CopyAsync(CancellationToken.None));
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
