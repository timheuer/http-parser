namespace HttpFileParser.Environment;

/// <summary>
/// Represents a parsed environment file with multiple environments.
/// </summary>
public sealed class EnvironmentFile
{
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Environments { get; }
    public IReadOnlyDictionary<string, string>? SharedVariables { get; }
    public string? FilePath { get; }

    public EnvironmentFile(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> environments,
        IReadOnlyDictionary<string, string>? sharedVariables,
        string? filePath)
    {
        Environments = environments;
        SharedVariables = sharedVariables;
        FilePath = filePath;
    }

    public IEnumerable<string> EnvironmentNames => Environments.Keys.Where(k => k != "$shared");

    public IReadOnlyDictionary<string, string>? GetEnvironment(string name)
    {
        return Environments.TryGetValue(name, out var env) ? env : null;
    }

    public Dictionary<string, string> GetMergedEnvironment(string name)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Start with shared variables
        if (SharedVariables != null)
        {
            foreach (var kvp in SharedVariables)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        // Override with environment-specific variables
        if (Environments.TryGetValue(name, out var envVars))
        {
            foreach (var kvp in envVars)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }
}
