using System.Text;
using HttpFileParser.Environment;
using HttpFileParser.Execution;
using HttpFileParser.Model;
using HttpFileParser.Parsing;
using HttpFileParser.Variables;

namespace HttpFileParser;

/// <summary>
/// High-level API for parsing .http files.
/// </summary>
public static class HttpFile
{
    /// <summary>
    /// Parses HTTP content from a string.
    /// </summary>
    public static HttpDocument Parse(string content, string? filePath = null)
    {
        var parser = new HttpParser();
        return parser.Parse(content, filePath);
    }

    /// <summary>
    /// Parses an HTTP file from the file system.
    /// </summary>
    public static HttpDocument ParseFile(string filePath)
    {
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        return Parse(content, filePath);
    }

    /// <summary>
    /// Parses HTTP content from a stream.
    /// </summary>
    public static HttpDocument Parse(Stream stream, string? filePath = null)
    {
        var parser = new HttpParser();
        return parser.Parse(stream, filePath);
    }
}

/// <summary>
/// High-level API for working with HTTP environment files.
/// </summary>
public static class HttpEnvironment
{
    /// <summary>
    /// Loads environment files from a directory.
    /// </summary>
    public static EnvironmentSelector Load(string directoryPath)
    {
        var discovery = new EnvironmentDiscovery();
        return discovery.DiscoverAndLoad(directoryPath);
    }

    /// <summary>
    /// Parses an environment file from content.
    /// </summary>
    public static EnvironmentFile Parse(string jsonContent, string? filePath = null)
    {
        var parser = new EnvironmentFileParser();
        return parser.Parse(jsonContent, filePath);
    }

    /// <summary>
    /// Parses VS Code settings.json for REST Client environment variables.
    /// </summary>
    public static EnvironmentFile ParseVsCodeSettings(string jsonContent, string? filePath = null)
    {
        var parser = new EnvironmentFileParser();
        return parser.ParseVsCodeSettings(jsonContent, filePath);
    }
}

/// <summary>
/// Extension methods for HttpDocument.
/// </summary>
public static class HttpDocumentExtensions
{
    /// <summary>
    /// Creates a variable context from the document's file variables.
    /// </summary>
    public static VariableContext CreateVariableContext(this HttpDocument document)
    {
        var context = new VariableContext();
        context.AddResolver(new FileVariableResolver(document));
        context.AddResolver(new DynamicVariableResolver());
        return context;
    }

    /// <summary>
    /// Resolves all requests in the document with the given variable context.
    /// </summary>
    public static ResolvedHttpDocument ResolveVariables(this HttpDocument document, VariableContext? context = null)
    {
        var resolverContext = context ?? document.CreateVariableContext();
        var resolver = new HttpRequestResolver(resolverContext);

        var resolvedRequests = document.Requests
            .Select(r => resolver.Resolve(r))
            .ToList();

        return new ResolvedHttpDocument(document, resolvedRequests, resolverContext);
    }
}

/// <summary>
/// Extension methods for ResolvedHttpRequest.
/// </summary>
public static class ResolvedHttpRequestExtensions
{
    /// <summary>
    /// Converts a resolved request to an HttpRequestMessage.
    /// </summary>
    public static HttpRequestMessage ToHttpRequestMessage(this ResolvedHttpRequest resolved, string? baseDirectory = null)
    {
        var builder = new HttpRequestBuilder(baseDirectory);
        return builder.Build(resolved);
    }
}

/// <summary>
/// Extension methods for HttpRequest.
/// </summary>
public static class HttpRequestExtensions
{
    /// <summary>
    /// Resolves variables and converts to HttpRequestMessage.
    /// </summary>
    public static HttpRequestMessage ToHttpRequestMessage(this HttpRequest request, VariableContext? context = null, string? baseDirectory = null)
    {
        var builder = new HttpRequestBuilder(baseDirectory, context);
        return builder.Build(request, context);
    }

    /// <summary>
    /// Resolves variables in the request.
    /// </summary>
    public static ResolvedHttpRequest Resolve(this HttpRequest request, VariableContext? context = null)
    {
        var resolverContext = context ?? VariableContext.CreateDefault();
        var resolver = new HttpRequestResolver(resolverContext);
        return resolver.Resolve(request);
    }
}
