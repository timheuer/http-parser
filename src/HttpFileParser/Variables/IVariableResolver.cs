namespace HttpFileParser.Variables;

/// <summary>
/// Interface for resolving variable values.
/// </summary>
public interface IVariableResolver
{
    /// <summary>
    /// Resolves the value of a variable by name.
    /// </summary>
    string? Resolve(string name);

    /// <summary>
    /// Checks if this resolver can resolve the given variable name.
    /// </summary>
    bool CanResolve(string name);
}
