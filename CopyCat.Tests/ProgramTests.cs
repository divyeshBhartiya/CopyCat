namespace CopyCat.Tests
{
    public class ProgramTests
    {
        [Fact]
        public async Task Main_WritesExpectedOutput()
        {
            // Arrange
            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            string[] args = { "--source", "C:\\Source", "--destination", "C:\\Dest" };
            await Program.Main(args);

            // Assert
            string output = consoleOutput.ToString();
            Assert.Contains("✅ Copy completed successfully!", output);
        }
    }

}
