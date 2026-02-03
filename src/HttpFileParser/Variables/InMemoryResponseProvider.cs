namespace HttpFileParser.Variables;

/// <summary>
/// Simple in-memory implementation of IRequestResponseProvider.
/// </summary>
public sealed class InMemoryResponseProvider : IRequestResponseProvider
{
    private readonly Dictionary<string, RequestResponse> _responses = new(StringComparer.OrdinalIgnoreCase);

    public void AddResponse(RequestResponse response)
    {
        _responses[response.RequestName] = response;
    }

    public void RemoveResponse(string requestName)
    {
        _responses.Remove(requestName);
    }

    public void Clear()
    {
        _responses.Clear();
    }

    public RequestResponse? GetResponse(string requestName)
    {
        return _responses.TryGetValue(requestName, out var response) ? response : null;
    }

    public bool HasResponse(string requestName)
    {
        return _responses.ContainsKey(requestName);
    }
}
