using HttpFileParser.Model;
using HttpFileParser.Variables;

namespace HttpFileParser.Execution;

/// <summary>
/// Represents a document with all requests resolved.
/// </summary>
public sealed class ResolvedHttpDocument
{
    public HttpDocument OriginalDocument { get; }
    public IReadOnlyList<ResolvedHttpRequest> Requests { get; }
    public VariableContext Context { get; }

    public ResolvedHttpDocument(
        HttpDocument originalDocument,
        IReadOnlyList<ResolvedHttpRequest> requests,
        VariableContext context)
    {
        OriginalDocument = originalDocument;
        Requests = requests;
        Context = context;
    }

    public bool HasUnresolvedVariables => Requests.Any(r => r.HasUnresolvedVariables);

    public IEnumerable<string> AllUnresolvedVariables => Requests
        .SelectMany(r => r.UnresolvedVariables)
        .Distinct();

    public ResolvedHttpRequest? GetRequestByName(string name)
    {
        return Requests.FirstOrDefault(r =>
            string.Equals(r.OriginalRequest.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
