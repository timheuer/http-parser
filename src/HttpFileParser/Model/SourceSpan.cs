namespace HttpFileParser.Model;

/// <summary>
/// Represents a location span in source code with start and end positions.
/// </summary>
public readonly record struct SourceSpan(
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    int StartOffset,
    int EndOffset)
{
    public static readonly SourceSpan Empty = new(0, 0, 0, 0, 0, 0);

    public int Length => EndOffset - StartOffset;

    public bool Contains(int line, int column)
    {
        if (line < StartLine || line > EndLine)
        {
            return false;
        }

        if (line == StartLine && column < StartColumn)
        {
            return false;
        }

        if (line == EndLine && column > EndColumn)
        {
            return false;
        }

        return true;
    }

    public static SourceSpan Merge(SourceSpan first, SourceSpan second)
    {
        return new SourceSpan(
            Math.Min(first.StartLine, second.StartLine),
            first.StartLine < second.StartLine ? first.StartColumn :
                first.StartLine > second.StartLine ? second.StartColumn :
                Math.Min(first.StartColumn, second.StartColumn),
            Math.Max(first.EndLine, second.EndLine),
            first.EndLine > second.EndLine ? first.EndColumn :
                first.EndLine < second.EndLine ? second.EndColumn :
                Math.Max(first.EndColumn, second.EndColumn),
            Math.Min(first.StartOffset, second.StartOffset),
            Math.Max(first.EndOffset, second.EndOffset));
    }
}
