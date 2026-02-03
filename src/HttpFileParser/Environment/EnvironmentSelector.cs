using HttpFileParser.Variables;

namespace HttpFileParser.Environment;

/// <summary>
/// Manages environment selection and merging of environment-specific variables
/// with shared variables and user overrides.
/// </summary>
public sealed class EnvironmentSelector
{
    private readonly List<EnvironmentFile> _environmentFiles = [];
    private string? _selectedEnvironment;

    public EnvironmentSelector()
    {
    }

    public void AddEnvironmentFile(EnvironmentFile file)
    {
        _environmentFiles.Add(file);
    }

    public void SelectEnvironment(string? environmentName)
    {
        _selectedEnvironment = environmentName;
    }

    public string? SelectedEnvironment => _selectedEnvironment;

    public IEnumerable<string> AvailableEnvironments
    {
        get
        {
            var environments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in _environmentFiles)
            {
                foreach (var name in file.EnvironmentNames)
                {
                    environments.Add(name);
                }
            }
            return environments;
        }
    }

    public Dictionary<string, string> GetMergedVariables()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in _environmentFiles)
        {
            // Apply shared variables first
            if (file.SharedVariables != null)
            {
                foreach (var kvp in file.SharedVariables)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            // Then apply selected environment variables
            if (_selectedEnvironment != null)
            {
                var envVars = file.GetEnvironment(_selectedEnvironment);
                if (envVars != null)
                {
                    foreach (var kvp in envVars)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        return result;
    }

    public EnvironmentVariableResolver CreateResolver()
    {
        return new EnvironmentVariableResolver(GetMergedVariables());
    }

    public VariableContext CreateContext()
    {
        var context = new VariableContext();
        context.AddResolver(CreateResolver());
        context.AddResolver(new DynamicVariableResolver());
        return context;
    }
}
