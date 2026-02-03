namespace HttpFileParser.Variables;

/// <summary>
/// Interface for providing responses from previous requests.
/// </summary>
public interface IRequestResponseProvider
{
    /// <summary>
    /// Gets the response for a named request.
    /// </summary>
    RequestResponse? GetResponse(string requestName);

    /// <summary>
    /// Checks if a response is available for the given request name.
    /// </summary>
    bool HasResponse(string requestName);
}
