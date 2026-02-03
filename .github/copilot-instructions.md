# HttpFileParser Project Instructions

## Project Overview

This is a .NET 8 library for parsing `.http` files (VS Code REST Client and Visual Studio 2022 formats). The library provides a document model for tooling/analysis plus convenience methods to produce `HttpRequestMessage` objects.

## Project Structure

```
HttpFileParser/
├── src/HttpFileParser/           # Main library
│   ├── Model/                    # Document model types
│   ├── Parsing/                  # Lexer and parser
│   ├── Variables/                # Variable resolution system
│   ├── Environment/              # Environment file handling
│   ├── Execution/                # HttpRequestMessage building
│   └── HttpFile.cs               # High-level facade
└── tests/HttpFileParser.Tests/   # xUnit tests
```

## Key Components

### Model (src/HttpFileParser/Model/)
- `HttpDocument` - Root container with requests, variables, comments
- `HttpRequest` - Method, URL, headers, body, directives
- `HttpRequestBody` - Base class with `TextBody`, `FileReferenceBody`, `MultipartBody`
- `SourceSpan` - Location tracking for tooling

### Parsing (src/HttpFileParser/Parsing/)
- `HttpLexer` - Tokenizes input into `Token` types
- `HttpParser` - Builds document model from tokens

### Variables (src/HttpFileParser/Variables/)
- `IVariableResolver` - Interface for variable sources
- `VariableContext` - Container with precedence resolution
- `VariableExpander` - Expands `{{variable}}` tokens
- Resolvers: `FileVariableResolver`, `EnvironmentVariableResolver`, `DynamicVariableResolver`, `RequestVariableResolver`

### Environment (src/HttpFileParser/Environment/)
- `EnvironmentFileParser` - Parses http-client.env.json and VS Code settings
- `EnvironmentSelector` - Manages environment selection and merging

### Execution (src/HttpFileParser/Execution/)
- `HttpRequestBuilder` - Creates `HttpRequestMessage` from requests
- `HttpRequestResolver` - Resolves variables in requests
- `ResolvedHttpRequest` / `ResolvedHttpDocument` - Resolved versions

## Usage Example

```csharp
using HttpFileParser;

// Parse
var doc = HttpFile.ParseFile("api.http");

// Load environment
var env = HttpEnvironment.Load(".");
var selector = new EnvironmentSelector();
selector.AddEnvironmentFile(env);
selector.SelectEnvironment("development");

// Resolve and build
var context = selector.CreateContext();
var resolved = doc.ResolveVariables(context);
var httpRequest = resolved.Requests.First().ToHttpRequestMessage();
```

## Testing

Run all tests with TRX output:
```bash
dotnet test --logger "trx;LogFileName=TestResults.trx"
```

Run tests without TRX (basic):
```bash
dotnet test
```

Test results are written to `tests/HttpFileParser.Tests/TestResults/` directory.

## Important Patterns

1. **Lists passed to constructors must be copied** if they'll be cleared later (see ParseRequestFromLine fix)
2. **File reference detection** uses `IsFileReference()` to distinguish `< path` from `<?xml`
3. **Directives before requests without delimiters** are collected in `pendingDirectives` and passed to parser
4. **Variable precedence**: request > file > environment > dynamic

## Dependencies

- `JsonPath.Net` - For JSONPath extraction from response bodies
- `Nerdbank.GitVersioning` - Automatic semantic versioning from git history
- `Microsoft.SourceLink.GitHub` - Source linking for debugging

## Versioning

This project uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) for automatic semantic versioning.

- Version is defined in `version.json` at repo root
- Format: `{major}.{minor}.{height}+{commit}` (e.g., `0.1.15+abc1234`)
- Public releases from `main` branch or `v*` tags
- To bump version, edit `version.json`
