namespace CopyCat.Tests
{
    public class CopyOptionsTests
    {
        [Fact]
        public void Parse_ValidArguments_ReturnsCorrectOptions()
        {
            // Arrange
            string[] args = ["--source", "C:\\Source", "--destination", "C:\\Dest", "--overwrite", "true", "--log", "verbose", "--parallel", "true", "--include-hidden", "true"];

            // Act
            var options = CopyOptions.Parse(args);

            // Assert
            Assert.Equal("C:\\Source", options.SourcePath);
            Assert.Equal("C:\\Dest", options.DestinationPath);
            Assert.True(options.Overwrite);
            Assert.Equal("verbose", options.LogLevel);
            Assert.True(options.Parallel);
            Assert.True(options.IncludeHidden);
        }

        [Fact]
        public void Parse_MissingSourceOrDestination_ThrowsException()
        {
            // Arrange
            string[] args = ["--source", "C:\\Source"]; // Missing destination

            // Act & Assert
            Assert.Throws<ArgumentException>(() => CopyOptions.Parse(args));
        }
    }
}
