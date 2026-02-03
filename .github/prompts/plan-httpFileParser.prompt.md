## Plan: C# .http File Parser Library

A full-featured .http file parser for .NET 8 supporting both VS Code REST Client and Visual Studio 2022 formats. The library provides a layered API: a document model for tooling/analysis plus convenience methods to produce `HttpRequestMessage` objects ready for HTTP execution.

**Project**: `HttpFileParser` — a single library project with optional extension packages for Azure integration.

---

**Steps**

1. **Create solution structure**
   - Create `HttpFileParser.sln` in workspace root
   - Create `src/HttpFileParser/HttpFileParser.csproj` targeting net8.0
   - Create `tests/HttpFileParser.Tests/HttpFileParser.Tests.csproj` with xUnit
   - Add standard files: `Directory.Build.props`, `.editorconfig`, `README.md`

2. **Define core document model types** in `src/HttpFileParser/Model/`
   - `HttpDocument` — root container with `IReadOnlyList<HttpDocumentItem>` (requests, variables, comments), source file path
   - `HttpRequest` — method, URL (raw and parsed), headers, body, name (from `@name`), directives (`@no-redirect`, `@note`, etc.), `SourceSpan`
   - `HttpHeader` — name, raw value (before variable resolution), `SourceSpan`
   - `HttpRequestBody` — abstract base with subtypes: `TextBody`, `FileReferenceBody` (path, encoding, processVariables flag), `MultipartBody`
   - `FileVariable` — name, raw value, `SourceSpan`
   - `Comment` — text, `SourceSpan`
   - `SourceSpan` — start line/column, end line/column, character offset

3. **Implement lexer** in `src/HttpFileParser/Parsing/HttpLexer.cs`
   - Token types: `RequestDelimiter`, `Comment`, `VariableDefinition`, `Directive`, `RequestLine`, `Header`, `BodyLine`, `Whitespace`, `EOF`
   - Stream-based reading with line/column tracking
   - Handle `###` delimiter detection, comment prefixes (`#`, `//`), variable definitions (`@name = value`)

4. **Implement parser** in `src/HttpFileParser/Parsing/HttpParser.cs`
   - Public API: `HttpDocument Parse(string content, string? filePath = null)`
   - Public API: `HttpDocument Parse(Stream stream, string? filePath = null)`
   - Produce `HttpDocument` with all items preserving source locations
   - Handle edge cases: query params across lines (`?`/`&` continuation), body after blank line, multipart boundaries, GraphQL detection via `X-REQUEST-TYPE` header
   - Collect parse diagnostics (warnings/errors) without throwing — attach to `HttpDocument.Diagnostics`

5. **Implement variable system** in `src/HttpFileParser/Variables/`
   - `VariableContext` — container for all variable sources with precedence: request > file > environment
   - `IVariableResolver` interface with `string? Resolve(string name)` and `bool CanResolve(string name)`
   - `FileVariableResolver` — resolves `@var = value` definitions from document
   - `EnvironmentVariableResolver` — loads from `http-client.env.json` / `http-client.env.json.user` (VS 2022 format) and VS Code `rest-client.environmentVariables` JSON structure
   - `DynamicVariableResolver` — implements `$guid`, `$timestamp`, `$randomInt min max`, `$datetime format [offset]`, `$localDatetime`, `$processEnv varName`, `$dotenv varName`
   - `VariableExpander` — replaces `{{variable}}` and `{{$systemVar}}` tokens, handles `{{%varName}}` percent-encoding, recursive resolution for variables referencing other variables

6. **Implement request variable resolution** in `src/HttpFileParser/Variables/RequestVariableResolver.cs`
   - Parse `{{requestName.response.body.$.jsonPath}}` and `{{requestName.response.headers.HeaderName}}` syntax
   - Requires callback/provider pattern since responses come from execution: `IRequestResponseProvider` interface
   - Support JSONPath for body extraction (use `System.Text.Json.Nodes` or reference `JsonPath.Net`)
   - Support XPath for XML responses

7. **Implement environment file parser** in `src/HttpFileParser/Environment/`
   - `EnvironmentFileParser.Parse(string jsonContent)` → `EnvironmentFile`
   - `EnvironmentFile` — dictionary of environments, each with variable dictionary, plus `$shared` section
   - Support VS 2022 `http-client.env.json` and VS Code settings.json `rest-client.environmentVariables` format
   - `EnvironmentSelector` — selects active environment by name, merges `$shared` with environment-specific values, applies `.user` file overrides

8. **Implement HttpRequestMessage builder** in `src/HttpFileParser/Execution/`
   - `HttpRequestBuilder` — transforms resolved `HttpRequest` into `System.Net.Http.HttpRequestMessage`
   - Handle all body types: string content, file stream, multipart form data
   - Public API: `HttpRequestMessage Build(HttpRequest request, VariableContext variables)`
   - Handle URL variable expansion and query string encoding

9. **Create high-level facade** in `src/HttpFileParser/HttpFileParser.cs`
   - `HttpFile.Parse(string content)` → `HttpDocument`
   - `HttpFile.ParseFile(string filePath)` → `HttpDocument` (reads file, handles encoding)
   - `HttpDocument.ResolveVariables(VariableContext context)` → `ResolvedHttpDocument`
   - `ResolvedHttpRequest.ToHttpRequestMessage()` → `HttpRequestMessage`
   - `HttpEnvironment.Load(string directoryPath)` → auto-discovers and loads env files

10. **Add comprehensive tests** in `tests/HttpFileParser.Tests/`
    - Parsing tests: single request, multiple requests, all HTTP methods, headers, bodies, comments, variables
    - Variable tests: file variables, environment variables, dynamic variables, precedence, recursive resolution, percent-encoding
    - Edge case tests: multiline URLs, GraphQL, multipart, file references, empty bodies
    - Builder tests: HttpRequestMessage output matches expectations
    - Use embedded .http sample files as test fixtures

---

**Verification**

1. Run `dotnet build` — should compile without errors
2. Run `dotnet test` — all unit tests pass
3. Manual verification with sample .http files from VS Code REST Client and Visual Studio 2022 documentation
4. Test variable resolution with nested variables and all dynamic variable types
5. Verify `HttpRequestMessage` output can be successfully sent via `HttpClient`

---

**Decisions**
- **Layered API over simple API**: Provides document model for tooling plus convenience methods — serves both analysis and execution scenarios
- **Single project initially**: Keep complexity low; Azure integrations can be separate packages later if needed
- **.NET 8 target**: Modern APIs, good performance with spans, while maintaining LTS support
- **No external parsing libraries**: Hand-written lexer/parser for full control over error recovery and source tracking
- **JsonPath.Net reference**: For `$.path` extraction from response bodies (small, focused dependency)
