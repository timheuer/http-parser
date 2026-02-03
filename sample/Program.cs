using HttpFileParser;
using HttpFileParser.Environment;
using HttpFileParser.Model;

// Parse command line arguments
var httpFilePath = args.Length > 0 ? args[0] : "sample-api.http";
var environment = args.Length > 1 ? args[1] : "development";

if (!File.Exists(httpFilePath))
{
    Console.Error.WriteLine($"Error: File not found: {httpFilePath}");
    Console.Error.WriteLine("Usage: HttpFileParser.Sample <path-to-.http-file> [environment]");
    return 1;
}

Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║               HttpFileParser Sample Application                   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Step 1: Parse the .http file
Console.WriteLine($"📄 Parsing: {httpFilePath}");
Console.WriteLine(new string('─', 60));

var document = HttpFile.ParseFile(httpFilePath);

Console.WriteLine($"   File: {document.FilePath}");
Console.WriteLine($"   Variables: {document.Variables.Count()}");
Console.WriteLine($"   Requests: {document.Requests.Count()}");
Console.WriteLine($"   Diagnostics: {document.Diagnostics.Count}");

if (document.Diagnostics.Count > 0)
{
    Console.WriteLine("\n   ⚠️  Diagnostics:");
    foreach (var diag in document.Diagnostics)
    {
        Console.WriteLine($"      [{diag.Severity}] {diag.Message} at line {diag.Span.StartLine}");
    }
}

// Step 2: Show file-level variables
Console.WriteLine();
Console.WriteLine("📋 File Variables:");
Console.WriteLine(new string('─', 60));
foreach (var variable in document.Variables)
{
    Console.WriteLine($"   @{variable.Name} = {variable.RawValue}");
}

// Step 3: Load environment
Console.WriteLine();
Console.WriteLine($"🌍 Loading Environment: {environment}");
Console.WriteLine(new string('─', 60));

var envDirectory = Path.GetDirectoryName(Path.GetFullPath(httpFilePath)) ?? ".";
var selector = HttpEnvironment.Load(envDirectory);

Console.WriteLine($"   Available environments: {string.Join(", ", selector.AvailableEnvironments)}");

selector.SelectEnvironment(environment);
Console.WriteLine($"   Selected: {selector.SelectedEnvironment}");

var envVars = selector.GetMergedVariables();
if (envVars.Count > 0)
{
    Console.WriteLine("   Environment variables:");
    foreach (var (key, value) in envVars)
    {
        Console.WriteLine($"      {key} = {value}");
    }
}

// Step 4: Create variable context and resolve
Console.WriteLine();
Console.WriteLine("🔧 Resolving Variables:");
Console.WriteLine(new string('─', 60));

var context = selector.CreateContext();
// Add file variables with higher precedence
context.InsertResolver(0, new HttpFileParser.Variables.FileVariableResolver(document));

var resolved = document.ResolveVariables(context);

if (resolved.HasUnresolvedVariables)
{
    Console.WriteLine($"   ⚠️  Unresolved variables: {string.Join(", ", resolved.AllUnresolvedVariables)}");
}
else
{
    Console.WriteLine("   ✅ All variables resolved successfully");
}

// Step 5: Show each request
Console.WriteLine();
Console.WriteLine("📨 Requests:");
Console.WriteLine(new string('─', 60));

foreach (var request in resolved.Requests)
{
    Console.WriteLine();
    Console.WriteLine($"   ┌─ {request.OriginalRequest.Name ?? "(unnamed)"}");
    Console.WriteLine($"   │  Method: {request.Method}");
    Console.WriteLine($"   │  URL: {request.ResolvedUrl}");

    if (request.OriginalRequest.Directives.Count > 0)
    {
        Console.WriteLine($"   │  Directives: {string.Join(", ", request.OriginalRequest.Directives.Select(d => $"@{d.Name}"))}");
    }

    if (request.ResolvedHeaders.Count > 0)
    {
        Console.WriteLine("   │  Headers:");
        foreach (var header in request.ResolvedHeaders)
        {
            Console.WriteLine($"   │     {header.Key}: {header.Value}");
        }
    }

    if (request.OriginalRequest.IsGraphQL)
    {
        Console.WriteLine("   │  Type: GraphQL");
    }

    if (request.ResolvedBody != null)
    {
        var bodyPreview = request.ResolvedBody.Length > 100
            ? request.ResolvedBody[..100] + "..."
            : request.ResolvedBody;
        Console.WriteLine($"   │  Body: {bodyPreview.Replace("\n", " ").Replace("\r", "")}");
    }

    Console.WriteLine("   └─");
}

// Step 6: Build HttpRequestMessage for first request
Console.WriteLine();
Console.WriteLine("🚀 Building HttpRequestMessage (first request):");
Console.WriteLine(new string('─', 60));

var firstRequest = resolved.Requests.FirstOrDefault();
if (firstRequest != null)
{
    var httpRequest = firstRequest.ToHttpRequestMessage(envDirectory);

    Console.WriteLine($"   {httpRequest.Method} {httpRequest.RequestUri}");
    Console.WriteLine("   Headers:");
    foreach (var header in httpRequest.Headers)
    {
        Console.WriteLine($"      {header.Key}: {string.Join(", ", header.Value)}");
    }

    if (httpRequest.Content != null)
    {
        foreach (var header in httpRequest.Content.Headers)
        {
            Console.WriteLine($"      {header.Key}: {string.Join(", ", header.Value)}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("   ✅ Ready to send via HttpClient!");
}

// Step 7: Optional - actually send a request
Console.WriteLine();
Console.Write("Send the first request? (y/N): ");
var input = Console.ReadLine();

if (input?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true && firstRequest != null)
{
    Console.WriteLine();
    Console.WriteLine("📡 Sending request...");

    using var client = new HttpClient();
    var httpRequest = firstRequest.ToHttpRequestMessage(envDirectory);

    try
    {
        var response = await client.SendAsync(httpRequest);

        Console.WriteLine($"   Status: {(int)response.StatusCode} {response.ReasonPhrase}");
        Console.WriteLine("   Response Headers:");
        foreach (var header in response.Headers.Take(5))
        {
            Console.WriteLine($"      {header.Key}: {string.Join(", ", header.Value)}");
        }

        var body = await response.Content.ReadAsStringAsync();
        var preview = body.Length > 500 ? body[..500] + "..." : body;
        Console.WriteLine($"   Body Preview:\n{preview}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"   ❌ Error: {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine("Done!");
return 0;
