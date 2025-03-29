using System.Runtime.InteropServices;
using Serilog;

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
        public async Task CopyDirectoryAsync_OverwritesExistingFiles_WhenEnabled()
        {
            // Arrange
            var srcFile = Path.Combine(testSourceDir, "test.txt");
            var destFile = Path.Combine(testDestDir, "test.txt");

            Directory.CreateDirectory(testDestDir);
            await File.WriteAllTextAsync(destFile, "Old Content"); // Existing file at destination

            var options = new CopyOptions(testSourceDir, testDestDir, Overwrite: true);
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(1, new Progress<int>(), correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act
            await service.CopyAsync(CancellationToken.None);

            // Assert
            Assert.True(File.Exists(destFile));
            Assert.Equal("Hello World!", await File.ReadAllTextAsync(destFile)); // Should be replaced
        }

        [Fact]
        public async Task CopyDirectoryAsync_SkipsExistingFiles_WhenOverwriteIsDisabled()
        {
            // Arrange
            var destFile = Path.Combine(testDestDir, "test.txt");

            Directory.CreateDirectory(testDestDir);
            await File.WriteAllTextAsync(destFile, "Old Content"); // Existing file at destination

            var options = new CopyOptions(testSourceDir, testDestDir, Overwrite: false);
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(1, new Progress<int>(), correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act
            await service.CopyAsync(CancellationToken.None);

            // Assert
            Assert.True(File.Exists(destFile));
            Assert.Equal("Old Content", await File.ReadAllTextAsync(destFile)); // Should remain unchanged
        }

        [Fact]
        public async Task CopyDirectoryAsync_RenamesFiles_OnConflict_WhenEnabled()
        {
            // Arrange
            var destFile = Path.Combine(testDestDir, "test.txt");

            Directory.CreateDirectory(testDestDir);
            await File.WriteAllTextAsync(destFile, "Old Content"); // Existing file at destination

            var options = new CopyOptions(testSourceDir, testDestDir, Overwrite: false, RenameOnConflict: true);
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(1, new Progress<int>(), correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act
            await service.CopyAsync(CancellationToken.None);

            // Assert
            string renamedFilePattern = Path.Combine(testDestDir, "test(*).txt");
            bool renamedFileExists = Directory.GetFiles(testDestDir, "test(*).txt").Any();

            Assert.True(File.Exists(destFile)); // Original file remains
            Assert.Equal("Old Content", await File.ReadAllTextAsync(destFile)); // Original remains unchanged
            Assert.True(renamedFileExists, "Renamed file should exist.");
        }


        [Fact]
        public async Task CopyDirectoryAsync_DetectsSymbolicLinks_Correctly()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return; // Skip the test if symbolic links aren't supported
            }

            // Arrange
            string targetFile = Path.Combine(testSourceDir, "realFile.txt");
            await File.WriteAllTextAsync(targetFile, "Real content");

            string symlinkPath = Path.Combine(testSourceDir, "symlink.txt");
            SymbolicLinkHelper.CreateSymbolicLink(symlinkPath, targetFile, false);

            Assert.True(SymbolicLinkHelper.IsSymbolicLink(symlinkPath), "Test setup failed: Symlink was not created.");

            var options = new CopyOptions(testSourceDir, testDestDir, Overwrite: true, Parallel: true, PreserveSymlinks: true);
            var progress = new Progress<int>(_ => { });
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(1, progress, correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act
            await service.CopyAsync(CancellationToken.None);

            // Assert
            string copiedSymlinkPath = Path.Combine(testDestDir, "symlink.txt");
            Assert.True(SymbolicLinkHelper.IsSymbolicLink(copiedSymlinkPath), "Symlink should be preserved.");
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

        [Fact]
        public async Task CopyDirectoryAsync_SkipsDirectories_WhenMaxDepthExceeded()
        {
            // Arrange
            int maxDepth = 50;
            string expectedSourcePath = testSourceDir;
            for (int i = 0; i < maxDepth; i++)
            {
                expectedSourcePath = Path.Combine(expectedSourcePath, $"Depth{i}");
                Directory.CreateDirectory(expectedSourcePath);
                Assert.True(Directory.Exists(expectedSourcePath), $"Expected source directory {expectedSourcePath} to exist but it does not.");
            }

            // Act
            var options = new CopyOptions(testSourceDir, testDestDir, Overwrite: true, MaxDepth: maxDepth); // Pass MaxDepth
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(1, new Progress<int>(), correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            Log.Information("Copying from {Source} to {Destination} with MaxDepth {MaxDepth}", testSourceDir, testDestDir, maxDepth);

            await service.CopyAsync(CancellationToken.None);

            foreach (var dir in Directory.GetDirectories(testDestDir, "*", SearchOption.AllDirectories))
            {
                Log.Information("Copied Directory: {Dir}", dir);
            }

            // Assert: Directories up to depth 50 should exist
            string expectedPath = testDestDir;
            for (int i = 0; i < maxDepth; i++)
            {
                expectedPath = Path.Combine(expectedPath, $"Depth{i}");
                Assert.True(Directory.Exists(expectedPath), $"Expected {expectedPath} to exist but it does not.");
            }

            // Assert: Depth 50 should NOT exist
            string skippedPath = Path.Combine(testDestDir, $"Depth{maxDepth}");
            Assert.False(Directory.Exists(skippedPath), $"Expected {skippedPath} NOT to exist, but it does!");
        }

        [Fact]
        public async Task CopyDirectoryAsync_DetectsCyclicSymbolicLinks()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return; // Skip if symbolic links are not supported
            }

            // Arrange
            string symlinkPath = Path.Combine(testSourceDir, "symlinkLoop");
            SymbolicLinkHelper.CreateSymbolicLink(symlinkPath, testSourceDir, true); // Creates a loop

            // Verify symbolic link creation
            Assert.True(SymbolicLinkHelper.IsSymbolicLink(symlinkPath), "Test setup failed: Symlink was not created.");
            var target = SymbolicLinkHelper.GetSymbolicLinkTarget(symlinkPath);
            Assert.Equal(testSourceDir, target);

            var options = new CopyOptions(testSourceDir, testDestDir, Overwrite: true, PreserveSymlinks: true);
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(1, new Progress<int>(), correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act & Assert
            try
            {
                await service.CopyAsync(CancellationToken.None);
            }
            catch (IOException ex)
            {
                Log.Information("IOException caught as expected: {Message}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error("Unexpected exception type: {ExceptionType}, Message: {Message}", ex.GetType(), ex.Message);
                throw;
            }
        }

        [Fact]
        public async Task CopyDirectoryAsync_CopiesLargeNumberOfFilesSuccessfully()
        {
            // Arrange
            int fileCount = 1000;
            for (int i = 0; i < fileCount; i++)
            {
                string filePath = Path.Combine(testSourceDir, $"file{i}.txt");
                await File.WriteAllTextAsync(filePath, $"Content of file {i}");
            }

            var options = new CopyOptions(testSourceDir, testDestDir, Overwrite: true);
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(1, new Progress<int>(), correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act
            await service.CopyAsync(CancellationToken.None);

            // Assert
            for (int i = 0; i < fileCount; i++)
            {
                string copiedFile = Path.Combine(testDestDir, $"file{i}.txt");
                Assert.True(File.Exists(copiedFile), $"File {copiedFile} was not copied.");
                Assert.Equal($"Content of file {i}", await File.ReadAllTextAsync(copiedFile));
            }
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
