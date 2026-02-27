using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;
using Json.Path;

namespace HttpFileParser.Variables;

/// <summary>
/// Resolves request variables that reference previous request responses.
/// Format: requestName.response.body.$.jsonPath or requestName.response.headers.HeaderName
/// </summary>
public sealed partial class RequestVariableResolver : IVariableResolver
{
    private readonly IRequestResponseProvider _responseProvider;

    public RequestVariableResolver(IRequestResponseProvider responseProvider)
    {
        _responseProvider = responseProvider;
    }

    public string? Resolve(string name)
    {
        var match = RequestVariablePattern().Match(name);
        if (!match.Success)
        {
            return null;
        }

        var requestName = match.Groups[1].Value;
        var responsePart = match.Groups[2].Value;
        var selector = match.Groups[3].Value;

        var response = _responseProvider.GetResponse(requestName);
        if (response == null)
        {
            return null;
        }

        return responsePart.ToLowerInvariant() switch
        {
            "body" => ResolveBodySelector(response, selector),
            "headers" => ResolveHeaderSelector(response, selector),
            _ => null
        };
    }

    public bool CanResolve(string name)
    {
        var match = RequestVariablePattern().Match(name);
        if (!match.Success)
        {
            return false;
        }

        var requestName = match.Groups[1].Value;
        return _responseProvider.HasResponse(requestName);
    }

    private static string? ResolveBodySelector(RequestResponse response, string selector)
    {
        if (string.IsNullOrEmpty(selector))
        {
            return response.Body;
        }

        // Check if it's a JSONPath selector (starts with $)
        if (selector.StartsWith("$", StringComparison.Ordinal))
        {
            return ResolveJsonPath(response.Body, selector);
        }

        // Check if it's an XPath selector (starts with /)
        if (selector.StartsWith("/", StringComparison.Ordinal))
        {
            return ResolveXPath(response.Body, selector);
        }

        // Try to detect content type
        if (response.ContentType?.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return ResolveJsonPath(response.Body, "$." + selector);
        }

        if (response.ContentType?.IndexOf("xml", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return ResolveXPath(response.Body, selector);
        }

        return null;
    }

    private static string? ResolveJsonPath(string json, string path)
    {
        try
        {
            var jsonNode = JsonNode.Parse(json);
            if (jsonNode == null)
            {
                return null;
            }

            var jsonPath = JsonPath.Parse(path);
            var result = jsonPath.Evaluate(jsonNode);

            if (result.Matches == null || result.Matches.Count == 0)
            {
                return null;
            }

            if (result.Matches.Count == 1)
            {
                var match = result.Matches[0].Value;
                return match switch
                {
                    JsonValue value => value.ToString(),
                    _ => match?.ToJsonString()
                };
            }

            // Multiple matches - return as JSON array
            var array = new JsonArray();
            foreach (var match in result.Matches)
            {
                if (match.Value != null)
                {
                    array.Add(JsonNode.Parse(match.Value.ToJsonString()));
                }
            }

            return array.ToJsonString();
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveXPath(string xml, string xpath)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            var nav = doc.CreateNavigator();
            if (nav == null)
            {
                return null;
            }

            var result = nav.Evaluate(xpath);

            return result switch
            {
                XPathNodeIterator iterator => iterator.MoveNext() ? iterator.Current?.Value : null,
                string s => s,
                double d => d.ToString(),
                bool b => b.ToString().ToLowerInvariant(),
                _ => result?.ToString()
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveHeaderSelector(RequestResponse response, string headerName)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            return null;
        }

        foreach (var header in response.Headers)
        {
            if (string.Equals(header.Key, headerName, StringComparison.OrdinalIgnoreCase))
            {
                return header.Value;
            }
        }

        return null;
    }

#if NET7_0_OR_GREATER
    // Pattern: requestName.response.body.selector or requestName.response.headers.headerName
    [GeneratedRegex(@"^([^.]+)\.response\.(body|headers)(?:\.(.+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex RequestVariablePattern();
#else
    // Pattern: requestName.response.body.selector or requestName.response.headers.headerName
    private static readonly Regex _requestVariablePattern = new(@"^([^.]+)\.response\.(body|headers)(?:\.(.+))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static Regex RequestVariablePattern() => _requestVariablePattern;
#endif
}
