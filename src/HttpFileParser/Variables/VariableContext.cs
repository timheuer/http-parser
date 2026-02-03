namespace HttpFileParser.Variables;

/// <summary>
/// Container for all variable sources with defined precedence.
/// Precedence: request > file > environment > dynamic
/// </summary>
public sealed class VariableContext
{
    private readonly List<IVariableResolver> _resolvers = [];

    public VariableContext()
    {
    }

    public VariableContext(IEnumerable<IVariableResolver> resolvers)
    {
        _resolvers.AddRange(resolvers);
    }

    public void AddResolver(IVariableResolver resolver)
    {
        _resolvers.Add(resolver);
    }

    public void InsertResolver(int index, IVariableResolver resolver)
    {
        _resolvers.Insert(index, resolver);
    }

    public void RemoveResolver(IVariableResolver resolver)
    {
        _resolvers.Remove(resolver);
    }

    public string? Resolve(string name)
    {
        foreach (var resolver in _resolvers)
        {
            if (resolver.CanResolve(name))
            {
                var value = resolver.Resolve(name);
                if (value != null)
                {
                    return value;
                }
            }
        }

        return null;
    }

    public bool CanResolve(string name)
    {
        return _resolvers.Any(r => r.CanResolve(name));
    }

    public IReadOnlyList<IVariableResolver> Resolvers => _resolvers.AsReadOnly();

    public static VariableContext CreateDefault()
    {
        var context = new VariableContext();
        context.AddResolver(new DynamicVariableResolver());
        return context;
    }
}
