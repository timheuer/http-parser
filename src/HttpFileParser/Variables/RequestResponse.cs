namespace HttpFileParser.Variables;

/// <summary>
/// Represents the response from a previous request for variable resolution.
/// </summary>
public sealed class RequestResponse
{
    public string RequestName { get; }
    public int StatusCode { get; }
    public IDictionary<string, string> Headers { get; }
    public string Body { get; }
    public string? ContentType { get; }

    public RequestResponse(
        string requestName,
        int statusCode,
        IDictionary<string, string> headers,
        string body,
        string? contentType = null)
    {
        RequestName = requestName;
        StatusCode = statusCode;
        Headers = headers;
        Body = body;
        ContentType = contentType;
    }
}
