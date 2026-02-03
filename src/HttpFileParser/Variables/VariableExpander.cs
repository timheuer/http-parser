using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace HttpFileParser.Variables;

/// <summary>
/// Expands {{variable}} tokens in strings using a VariableContext.
/// Supports recursive resolution and percent-encoding with {{%varName}}.
/// </summary>
public sealed partial class VariableExpander
{
    private readonly VariableContext _context;
    private readonly int _maxRecursionDepth;

    public VariableExpander(VariableContext context, int maxRecursionDepth = 10)
    {
        _context = context;
        _maxRecursionDepth = maxRecursionDepth;
    }

    public string Expand(string input)
    {
        return ExpandInternal(input, 0, []);
    }

    public string Expand(string input, out List<string> unresolvedVariables)
    {
        unresolvedVariables = [];
        return ExpandInternal(input, 0, unresolvedVariables);
    }

    private string ExpandInternal(string input, int depth, List<string> unresolvedVariables)
    {
        if (depth > _maxRecursionDepth)
        {
            return input;
        }

        var result = new StringBuilder();
        var i = 0;

        while (i < input.Length)
        {
            // Look for {{ pattern
            if (i + 1 < input.Length && input[i] == '{' && input[i + 1] == '{')
            {
                var closeIndex = input.IndexOf("}}", i + 2, StringComparison.Ordinal);
                if (closeIndex == -1)
                {
                    // No closing braces, append as-is
                    result.Append(input[i]);
                    i++;
                    continue;
                }

                var variableExpr = input[(i + 2)..closeIndex];
                var (resolvedValue, resolved) = ResolveVariable(variableExpr, depth, unresolvedVariables);

                if (resolved)
                {
                    result.Append(resolvedValue);
                }
                else
                {
                    // Keep unresolved variable as-is
                    result.Append("{{");
                    result.Append(variableExpr);
                    result.Append("}}");
                    if (!unresolvedVariables.Contains(variableExpr))
                    {
                        unresolvedVariables.Add(variableExpr);
                    }
                }

                i = closeIndex + 2;
            }
            else
            {
                result.Append(input[i]);
                i++;
            }
        }

        return result.ToString();
    }

    private (string? Value, bool Resolved) ResolveVariable(string expression, int depth, List<string> unresolvedVariables)
    {
        var trimmed = expression.Trim();

        // Check for percent-encoding prefix
        var percentEncode = false;
        if (trimmed.StartsWith('%'))
        {
            percentEncode = true;
            trimmed = trimmed[1..];
        }

        // Resolve the variable
        var value = _context.Resolve(trimmed);

        if (value == null)
        {
            return (null, false);
        }

        // Recursively expand any nested variables in the resolved value
        if (value.Contains("{{"))
        {
            value = ExpandInternal(value, depth + 1, unresolvedVariables);
        }

        // Apply percent-encoding if requested
        if (percentEncode)
        {
            value = HttpUtility.UrlEncode(value);
        }

        return (value, true);
    }

    public static bool ContainsVariables(string input)
    {
        return VariablePattern().IsMatch(input);
    }

    public static IEnumerable<string> ExtractVariableNames(string input)
    {
        var matches = VariablePattern().Matches(input);
        foreach (Match match in matches)
        {
            var varName = match.Groups[1].Value.Trim();
            if (varName.StartsWith('%'))
            {
                varName = varName[1..];
            }
            yield return varName;
        }
    }

    [GeneratedRegex(@"\{\{([^}]+)\}\}")]
    private static partial Regex VariablePattern();
}
