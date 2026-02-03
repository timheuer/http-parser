# HttpFileParser

A full-featured .http file parser for C#.

## Features

- Parse .http files into a structured document model
- Support for file and environment variables
- Dynamic variables (`$guid`, `$timestamp`, `$randomInt`, etc.)
- Request variable resolution with JSONPath/XPath support
- Convert parsed requests to `HttpRequestMessage` for execution
- Source location tracking for tooling integration

## Installation

```bash
dotnet add package HttpFileParser
```

## Quick Start

```csharp
using HttpFileParser;

// Parse an .http file
var document = HttpFile.ParseFile("api-tests.http");

// Load environment variables
var environment = HttpEnvironment.Load(".");
var context = environment.CreateContext("development");

// Resolve variables and create HTTP request
var resolved = document.ResolveVariables(context);
var httpRequest = resolved.Requests[0].ToHttpRequestMessage();

// Send request
using var client = new HttpClient();
var response = await client.SendAsync(httpRequest);
```

## Supported Syntax

### Request Format

```http
### Request Name
# @name myRequest
# @no-redirect
GET https://api.example.com/users/{{userId}}
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "name": "{{userName}}"
}
```

### Variables

```http
@baseUrl = https://api.example.com
@token = my-secret-token

GET {{baseUrl}}/users
Authorization: Bearer {{token}}
```

### Dynamic Variables

- `{{$guid}}` - Generate a UUID
- `{{$timestamp}}` - Unix timestamp
- `{{$randomInt min max}}` - Random integer
- `{{$datetime format [offset]}}` - Formatted date/time
- `{{$processEnv VAR}}` - Process environment variable
- `{{$dotenv VAR}}` - .env file variable

### Environment Files

The library supports `http-client.env.json` environment files.

## Acknowledgements

This project was built with the assistance of [GitHub Copilot](https://github.com/features/copilot).

## License

MIT
