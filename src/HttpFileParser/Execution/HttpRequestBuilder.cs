using System.Net.Http.Headers;
using System.Text;
using HttpFileParser.Model;
using HttpFileParser.Variables;

namespace HttpFileParser.Execution;

/// <summary>
/// Builds HttpRequestMessage from resolved HTTP requests.
/// </summary>
public sealed class HttpRequestBuilder
{
    private readonly string? _baseDirectory;
    private readonly VariableContext? _variableContext;

    public HttpRequestBuilder(string? baseDirectory = null, VariableContext? variableContext = null)
    {
        _baseDirectory = baseDirectory;
        _variableContext = variableContext;
    }

    public HttpRequestMessage Build(HttpRequest request, VariableContext? context = null)
    {
        var resolverContext = context ?? _variableContext ?? VariableContext.CreateDefault();
        var resolver = new HttpRequestResolver(resolverContext);
        var resolved = resolver.Resolve(request);

        return Build(resolved);
    }

    public HttpRequestMessage Build(ResolvedHttpRequest resolved)
    {
        var httpRequest = new HttpRequestMessage(
            new HttpMethod(resolved.Method),
            resolved.ResolvedUrl);

        // Add headers
        foreach (var header in resolved.ResolvedHeaders)
        {
            // Some headers go to Content, others to Request headers
            if (IsContentHeader(header.Key))
            {
                // Content headers are added after content is set
                continue;
            }

            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Set content
        var content = CreateContent(resolved);
        if (content != null)
        {
            httpRequest.Content = content;

            // Add content headers
            foreach (var header in resolved.ResolvedHeaders)
            {
                if (IsContentHeader(header.Key))
                {
                    httpRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        return httpRequest;
    }

    private HttpContent? CreateContent(ResolvedHttpRequest resolved)
    {
        var body = resolved.OriginalRequest.Body;

        if (body == null)
        {
            return null;
        }

        return body switch
        {
            TextBody textBody => CreateTextContent(resolved, textBody),
            FileReferenceBody fileBody => CreateFileContent(fileBody),
            MultipartBody multipartBody => CreateMultipartContent(multipartBody),
            _ => null
        };
    }

    private static HttpContent CreateTextContent(ResolvedHttpRequest resolved, TextBody textBody)
    {
        var bodyContent = resolved.ResolvedBody ?? textBody.Content;
        var encoding = Encoding.UTF8;

        // Determine content type from headers
        var contentType = resolved.ResolvedHeaders
            .FirstOrDefault(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            .Value;

        if (string.IsNullOrEmpty(contentType))
        {
            // Detect content type from body
            contentType = DetectContentType(bodyContent);
        }

        return new StringContent(bodyContent, encoding, contentType ?? "text/plain");
    }

    private HttpContent? CreateFileContent(FileReferenceBody fileBody)
    {
        var filePath = fileBody.FilePath;

        // Resolve relative path
        if (!Path.IsPathRooted(filePath) && _baseDirectory != null)
        {
            filePath = Path.Combine(_baseDirectory, filePath);
        }

        if (!File.Exists(filePath))
        {
            return null;
        }

        if (fileBody.ProcessVariables && _variableContext != null)
        {
            // Read and process variables
            var encoding = GetEncoding(fileBody.Encoding);
            var content = File.ReadAllText(filePath, encoding);
            var expander = new VariableExpander(_variableContext);
            content = expander.Expand(content);
            return new StringContent(content, encoding);
        }
        else
        {
            // Read file as-is
            var fileStream = File.OpenRead(filePath);
            return new StreamContent(fileStream);
        }
    }

    private HttpContent CreateMultipartContent(MultipartBody multipartBody)
    {
        var content = new MultipartFormDataContent(multipartBody.Boundary);

        foreach (var section in multipartBody.Sections)
        {
            HttpContent? sectionContent = null;

            if (section.Body is TextBody textBody)
            {
                sectionContent = new StringContent(textBody.Content);
            }
            else if (section.Body is FileReferenceBody fileBody)
            {
                sectionContent = CreateFileContent(fileBody);
            }

            if (sectionContent != null)
            {
                // Add section headers
                foreach (var header in section.Headers)
                {
                    sectionContent.Headers.TryAddWithoutValidation(header.Name, header.RawValue);
                }

                // Determine name from Content-Disposition
                var contentDisposition = section.Headers
                    .FirstOrDefault(h => string.Equals(h.Name, "Content-Disposition", StringComparison.OrdinalIgnoreCase));

                var name = ExtractDispositionName(contentDisposition?.RawValue) ?? "file";
                var fileName = ExtractDispositionFileName(contentDisposition?.RawValue);

                if (fileName != null)
                {
                    content.Add(sectionContent, name, fileName);
                }
                else
                {
                    content.Add(sectionContent, name);
                }
            }
        }

        return content;
    }

    private static bool IsContentHeader(string headerName)
    {
        return headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Language", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Location", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-MD5", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Content-Range", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Expires", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Last-Modified", StringComparison.OrdinalIgnoreCase);
    }

    private static string? DetectContentType(string content)
    {
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return "application/json";
        }

        if (trimmed.StartsWith("<", StringComparison.Ordinal))
        {
            if (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("<soap:", StringComparison.OrdinalIgnoreCase))
            {
                return "application/xml";
            }

            return "text/html";
        }

        return null;
    }

    private static Encoding GetEncoding(string? encodingName)
    {
        if (string.IsNullOrEmpty(encodingName))
        {
            return Encoding.UTF8;
        }

        return encodingName!.ToLowerInvariant() switch
        {
            "utf-8" or "utf8" => Encoding.UTF8,
            "utf-16" or "utf16" => Encoding.Unicode,
            "ascii" => Encoding.ASCII,
            "latin1" or "iso-8859-1" => Encoding.GetEncoding("iso-8859-1"),
            _ => Encoding.UTF8
        };
    }

    private static string? ExtractDispositionName(string? contentDisposition)
    {
        if (string.IsNullOrEmpty(contentDisposition))
        {
            return null;
        }

        var nameIndex = contentDisposition!.IndexOf("name=\"", StringComparison.OrdinalIgnoreCase);
        if (nameIndex == -1)
        {
            return null;
        }

        nameIndex += 6; // Length of "name=\""
        var endIndex = contentDisposition.IndexOf('"', nameIndex);
        if (endIndex == -1)
        {
            return null;
        }

        return contentDisposition.Substring(nameIndex, endIndex - nameIndex);
    }

    private static string? ExtractDispositionFileName(string? contentDisposition)
    {
        if (string.IsNullOrEmpty(contentDisposition))
        {
            return null;
        }

        var fileNameIndex = contentDisposition!.IndexOf("filename=\"", StringComparison.OrdinalIgnoreCase);
        if (fileNameIndex == -1)
        {
            return null;
        }

        fileNameIndex += 10; // Length of "filename=\""
        var endIndex = contentDisposition.IndexOf('"', fileNameIndex);
        if (endIndex == -1)
        {
            return null;
        }

        return contentDisposition.Substring(fileNameIndex, endIndex - fileNameIndex);
    }
}
