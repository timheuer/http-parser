using HttpFileParser.Model;

namespace HttpFileParser.Variables;

/// <summary>
/// Resolves variables defined in the HTTP document (@var = value).
/// </summary>
public sealed class FileVariableResolver : IVariableResolver
{
    private readonly Dictionary<string, string> _variables = new(StringComparer.OrdinalIgnoreCase);

    public FileVariableResolver()
    {
    }

    public FileVariableResolver(HttpDocument document)
    {
        foreach (var variable in document.Variables)
        {
            _variables[variable.Name] = variable.RawValue;
        }
    }

    public FileVariableResolver(IEnumerable<FileVariable> variables)
    {
        foreach (var variable in variables)
        {
            _variables[variable.Name] = variable.RawValue;
        }
    }

    public void SetVariable(string name, string value)
    {
        _variables[name] = value;
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
