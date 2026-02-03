namespace HttpFileParser.Environment;

/// <summary>
/// Auto-discovers and loads environment files from a directory.
/// </summary>
public sealed class EnvironmentDiscovery
{
    private static readonly string[] EnvFileNames =
    [
        "http-client.env.json",
        "http-client.env.json.user",
        "http-client.private.env.json"
    ];

    public EnvironmentSelector DiscoverAndLoad(string directoryPath)
    {
        var selector = new EnvironmentSelector();
        var parser = new EnvironmentFileParser();

        foreach (var fileName in EnvFileNames)
        {
            var filePath = Path.Combine(directoryPath, fileName);
            if (File.Exists(filePath))
            {
                var envFile = parser.ParseFile(filePath);
                selector.AddEnvironmentFile(envFile);
            }
        }

        // Also check for VS Code settings
        var vsCodeSettingsPath = Path.Combine(directoryPath, ".vscode", "settings.json");
        if (File.Exists(vsCodeSettingsPath))
        {
            var content = File.ReadAllText(vsCodeSettingsPath);
            var envFile = parser.ParseVsCodeSettings(content, vsCodeSettingsPath);
            if (envFile.Environments.Count > 0)
            {
                selector.AddEnvironmentFile(envFile);
            }
        }

        return selector;
    }

    public static IEnumerable<string> FindEnvironmentFiles(string directoryPath)
    {
        foreach (var fileName in EnvFileNames)
        {
            var filePath = Path.Combine(directoryPath, fileName);
            if (File.Exists(filePath))
            {
                yield return filePath;
            }
        }

        var vsCodeSettingsPath = Path.Combine(directoryPath, ".vscode", "settings.json");
        if (File.Exists(vsCodeSettingsPath))
        {
            yield return vsCodeSettingsPath;
        }
    }
}
