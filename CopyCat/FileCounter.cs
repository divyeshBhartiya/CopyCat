using System.Collections.Concurrent;

namespace CopyCat
{
    public static class FileCounter
    {
        public static int CountTotalFiles(string directoryPath, int maxParallelism = 5)
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"The directory '{directoryPath}' does not exist.");
            }

            try
            {
                var fileCount = new ConcurrentBag<int>();
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxParallelism };

                // 🔹 Parallel enumeration with controlled concurrency
                Parallel.ForEach(Directory.EnumerateDirectories(directoryPath, "*", SearchOption.AllDirectories), parallelOptions, subDir =>
                {
                    try
                    {
                        int count = Directory.EnumerateFiles(subDir).Count();
                        fileCount.Add(count);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Console.WriteLine($"⚠️ Warning: Access denied to '{subDir}'. Skipping.");
                    }
                });

                // 🔹 Count root directory files separately
                int rootFileCount = Directory.EnumerateFiles(directoryPath).Count();

                return rootFileCount + fileCount.Sum();
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"⚠️ Warning: Access denied to some files in '{directoryPath}': {ex.Message}");
                return 0;
            }
        }
    }
}
