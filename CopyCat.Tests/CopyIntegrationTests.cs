using System.Runtime.InteropServices;

namespace CopyCat.Tests
{
    public class CopyIntegrationTests : IDisposable
    {
        private readonly string testSourceDir = Path.Combine(Path.GetTempPath(), "CopyCatIntegrationTestSrc");
        private readonly string testDestDir = Path.Combine(Path.GetTempPath(), "CopyCatIntegrationTestDst");

        public CopyIntegrationTests()
        {
            Cleanup();
            Directory.CreateDirectory(testSourceDir);
            Directory.CreateDirectory(Path.Combine(testSourceDir, "SubDir"));
            File.WriteAllText(Path.Combine(testSourceDir, "file1.txt"), "File 1");
            File.WriteAllText(Path.Combine(testSourceDir, "SubDir", "file2.txt"), "File 2");
        }

        [Fact]
        public async Task CopyDirectoryAsync_CopiesSymbolicLinks_AsIs()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return; // Skip the test if the OS doesn't support symbolic links
            }

            // Arrange
            string targetFile = Path.Combine(testSourceDir, "realFile.txt");
            await File.WriteAllTextAsync(targetFile, "This is a real file.");

            string symlinkPath = Path.Combine(testSourceDir, "symlink.txt");
            SymbolicLinkHelper.CreateSymbolicLink(symlinkPath, targetFile, false);

            var options = new CopyOptions(testSourceDir, testDestDir, Overwrite: true, Parallel: true, PreserveSymlinks: true);
            var progress = new Progress<int>(_ => { });
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(1, progress, correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act
            await service.CopyAsync(CancellationToken.None);

            // Assert
            string copiedSymlinkPath = Path.Combine(testDestDir, "symlink.txt");
            Assert.True(SymbolicLinkHelper.IsSymbolicLink(copiedSymlinkPath), "Symlink should be copied as a symlink.");
            Assert.Equal(await File.ReadAllTextAsync(targetFile), await File.ReadAllTextAsync(SymbolicLinkHelper.GetSymbolicLinkTarget(copiedSymlinkPath)));
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

        [Fact]
        public async Task CopyDirectoryAsync_StopsCopying_AtMaxDepth()
        {
            // Arrange
            string deepDir = testSourceDir;
            int maxDepth = 50;
            for (int i = 0; i <= maxDepth; i++)
            {
                deepDir = Path.Combine(deepDir, $"Depth{i}");
                Directory.CreateDirectory(deepDir);
            }

            var options = new CopyOptions(testSourceDir, testDestDir, Overwrite: true, MaxDepth: maxDepth);
            var progress = new Progress<int>(_ => { });
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(1, progress, correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act
            var exception = await Record.ExceptionAsync(() => service.CopyAsync(CancellationToken.None));

            // Assert
            Assert.Null(exception);
            Assert.False(Directory.Exists(Path.Combine(testDestDir, $"Depth{maxDepth + 1}")));
        }

        [Fact]
        public async Task CopyDirectoryAsync_SkipsExistingFiles_WhenOverwriteIsFalse()
        {
            // Arrange
            Directory.CreateDirectory(testDestDir);
            string existingFilePath = Path.Combine(testDestDir, "file1.txt");
            await File.WriteAllTextAsync(existingFilePath, "Old Data"); // Pre-existing file in destination

            var options = new CopyOptions(testSourceDir, testDestDir, Overwrite: false); // Overwrite disabled
            int totalFiles = FileCounter.CountTotalFiles(testSourceDir);
            var progress = new Progress<int>(_ => { });
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(totalFiles, progress, correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act
            await service.CopyAsync(CancellationToken.None);

            // Assert
            string copiedContent = await File.ReadAllTextAsync(existingFilePath);
            Assert.Equal("Old Data", copiedContent); // The file should not be replaced
        }

        [Fact]
        public async Task CopyDirectoryAsync_RenamesFiles_WhenRenameOnConflictIsTrue()
        {
            // Arrange
            Directory.CreateDirectory(testDestDir);
            string existingFilePath = Path.Combine(testDestDir, "file1.txt");
            await File.WriteAllTextAsync(existingFilePath, "Old Data"); // Pre-existing file in destination

            var options = new CopyOptions(testSourceDir, testDestDir, Overwrite: false, RenameOnConflict: true);
            int totalFiles = FileCounter.CountTotalFiles(testSourceDir);
            var progress = new Progress<int>(_ => { });
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(totalFiles, progress, correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act
            await service.CopyAsync(CancellationToken.None);

            // Assert
            string renamedFilePath = Path.Combine(testDestDir, "file1(1).txt");
            Assert.True(File.Exists(renamedFilePath), "Renamed file should exist.");
            Assert.Equal("File 1", await File.ReadAllTextAsync(renamedFilePath)); // New copy should be correctly renamed
        }

        [Fact]
        public async Task CopyDirectoryAsync_CopiesHiddenFiles_WhenIncludeHiddenIsTrue()
        {
            // Arrange
            var sourceDir = Path.Combine(testSourceDir, "source_hidden");
            var destDir = Path.Combine(testDestDir, "dest_hidden");
            Directory.CreateDirectory(sourceDir);
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "visible.txt"), "This is a visible file");

            var hiddenFilePath = Path.Combine(sourceDir, "hidden.txt");
            await File.WriteAllTextAsync(hiddenFilePath, "This is a hidden file");
            File.SetAttributes(hiddenFilePath, FileAttributes.Hidden);
            int totalFiles = FileCounter.CountTotalFiles(testSourceDir);
            var options = new CopyOptions(sourceDir, destDir, IncludeHidden: true);
            var progress = new Progress<int>(_ => { });
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(totalFiles, progress, correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act
            await service.CopyAsync(CancellationToken.None);

            // Assert
            Assert.True(File.Exists(Path.Combine(destDir, "visible.txt")));
            Assert.True(File.Exists(Path.Combine(destDir, "hidden.txt"))); // Hidden file should be copied
        }

        [Fact]
        public async Task CopyDirectoryAsync_DoesNotCopyHiddenFiles_WhenIncludeHiddenIsFalse()
        {
            // Arrange
            var sourceDir = Path.Combine(testSourceDir, "source_hidden");
            var destDir = Path.Combine(testDestDir, "dest_hidden");
            Directory.CreateDirectory(sourceDir);
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "visible.txt"), "This is a visible file");

            var hiddenFilePath = Path.Combine(sourceDir, "hidden.txt");
            await File.WriteAllTextAsync(hiddenFilePath, "This is a hidden file");
            File.SetAttributes(hiddenFilePath, FileAttributes.Hidden);
            int totalFiles = FileCounter.CountTotalFiles(testSourceDir);
            var options = new CopyOptions(sourceDir, destDir, IncludeHidden: false);
            var progress = new Progress<int>(_ => { });
            var correlationId = Guid.NewGuid().ToString();
            var progressReporter = new ProgressReporter(totalFiles, progress, correlationId);
            var service = new CopyService(options, progressReporter, correlationId);

            // Act
            await service.CopyAsync(CancellationToken.None);

            // Assert
            Assert.True(File.Exists(Path.Combine(destDir, "visible.txt")));
            Assert.False(File.Exists(Path.Combine(destDir, "hidden.txt"))); // Hidden file should NOT be copied
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
