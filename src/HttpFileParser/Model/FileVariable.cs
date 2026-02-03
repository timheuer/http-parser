namespace HttpFileParser.Model;

/// <summary>
/// Represents a variable definition in the HTTP document.
/// </summary>
public sealed class FileVariable : HttpDocumentItem
{
    public string Name { get; }
    public string RawValue { get; }

    public FileVariable(string name, string rawValue, SourceSpan span) : base(span)
    {
        Name = name;
        RawValue = rawValue;
    }
}
