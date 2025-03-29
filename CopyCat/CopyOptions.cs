namespace CopyCat;

public record CopyOptions(
string SourcePath,
string DestinationPath,
bool Overwrite = false,
bool RenameOnConflict = false,
string LogLevel = "error-only",
bool Parallel = false,
bool IncludeHidden = false,
bool PreserveSymlinks = false,
int MaxDepth = 50)
{
    public static CopyOptions Parse(string[] args)
    {
        var argDict = ParseArguments(args);

        if (!argDict.ContainsKey("source") || !argDict.ContainsKey("destination"))
        {
            throw new ArgumentException("Source and destination paths are required.");
        }

        return new CopyOptions(
            argDict["source"],
            argDict["destination"],
            argDict.ContainsKey("overwrite") && bool.Parse(argDict["overwrite"]),
            argDict.ContainsKey("rename-on-conflict") && bool.Parse(argDict["rename-on-conflict"]),
            argDict.ContainsKey("log") ? argDict["log"].ToLower() : "error-only",
            argDict.ContainsKey("parallel") && bool.Parse(argDict["parallel"]),
            argDict.ContainsKey("include-hidden") && bool.Parse(argDict["include-hidden"]),
            argDict.ContainsKey("preserve-symlinks") && bool.Parse(argDict["preserve-symlinks"])
        );
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length - 1; i += 2)
        {
            dict[args[i].Replace("--", "")] = args[i + 1];
        }
        return dict;
    }
}