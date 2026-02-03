using System.Text;
using HttpFileParser.Model;
using HttpFileParser.Variables;

namespace HttpFileParser.Execution;

/// <summary>
/// Resolves variables in an HTTP request.
/// </summary>
public sealed class HttpRequestResolver
{
    private readonly VariableContext _context;

    public HttpRequestResolver(VariableContext context)
    {
        _context = context;
    }

    public ResolvedHttpRequest Resolve(HttpRequest request)
    {
        var expander = new VariableExpander(_context);
        var allUnresolved = new List<string>();

        // Resolve URL
        var resolvedUrl = expander.Expand(request.RawUrl, out var urlUnresolved);
        allUnresolved.AddRange(urlUnresolved);

        // Resolve headers
        var resolvedHeaders = new List<KeyValuePair<string, string>>();
        foreach (var header in request.Headers)
        {
            var resolvedValue = expander.Expand(header.RawValue, out var headerUnresolved);
            allUnresolved.AddRange(headerUnresolved);
            resolvedHeaders.Add(new KeyValuePair<string, string>(header.Name, resolvedValue));
        }

        // Resolve body
        string? resolvedBody = null;
        if (request.Body is TextBody textBody)
        {
            resolvedBody = expander.Expand(textBody.Content, out var bodyUnresolved);
            allUnresolved.AddRange(bodyUnresolved);
        }
        else if (request.Body is FileReferenceBody fileBody && fileBody.ProcessVariables)
        {
            // File content will be resolved at build time
            resolvedBody = null;
        }

        // Deduplicate unresolved variables
        var uniqueUnresolved = allUnresolved.Distinct().ToList();

        return new ResolvedHttpRequest(
            request,
            request.Method,
            resolvedUrl,
            resolvedHeaders,
            resolvedBody,
            uniqueUnresolved);
    }
}
