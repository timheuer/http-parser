namespace HttpFileParser.Variables;

/// <summary>
/// Resolves variables from environment configuration (http-client.env.json or VS Code settings).
/// </summary>
public sealed class EnvironmentVariableResolver : IVariableResolver
{
    private readonly Dictionary<string, string> _variables = new(StringComparer.OrdinalIgnoreCase);

    public EnvironmentVariableResolver()
    {
    }

    public EnvironmentVariableResolver(IDictionary<string, string> variables)
    {
        foreach (var kvp in variables)
        {
            _variables[kvp.Key] = kvp.Value;
        }
    }

    public void SetVariable(string name, string value)
    {
        _variables[name] = value;
    }

    public void SetVariables(IDictionary<string, string> variables)
    {
        foreach (var kvp in variables)
        {
            _variables[kvp.Key] = kvp.Value;
        }
    }

    public void Clear()
    {
        _variables.Clear();
    }

    public string? Resolve(string name)
    {
        return _variables.TryGetValue(name, out var value) ? value : null;
    }

    public bool CanResolve(string name)
    {
        return _variables.ContainsKey(name);
    }

    public IReadOnlyDictionary<string, string> Variables => _variables;
}
