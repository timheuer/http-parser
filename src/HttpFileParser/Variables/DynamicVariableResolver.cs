using System.Globalization;
using System.Text.RegularExpressions;

namespace HttpFileParser.Variables;

/// <summary>
/// Resolves dynamic/system variables like $guid, $timestamp, $randomInt, etc.
/// </summary>
public sealed partial class DynamicVariableResolver : IVariableResolver
{
    private readonly Random _random = new();
    private readonly Dictionary<string, Func<string>> _staticResolvers;

    public DynamicVariableResolver()
    {
        _staticResolvers = new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["$guid"] = () => Guid.NewGuid().ToString(),
            ["$uuid"] = () => Guid.NewGuid().ToString(),
            ["$timestamp"] = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["$isoTimestamp"] = () => DateTimeOffset.UtcNow.ToString("o"),
            ["$randomInt"] = () => _random.Next().ToString()
        };
    }

    public string? Resolve(string name)
    {
        // Check static resolvers first
        if (_staticResolvers.TryGetValue(name, out var resolver))
        {
            return resolver();
        }

        // Handle parameterized dynamic variables
        if (name.StartsWith("$randomInt ", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveRandomInt(name);
        }

        if (name.StartsWith("$datetime ", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveDatetime(name, utc: true);
        }

        if (name.StartsWith("$localDatetime ", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveDatetime(name, utc: false);
        }

        if (name.StartsWith("$processEnv ", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveProcessEnv(name);
        }

        if (name.StartsWith("$dotenv ", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveDotEnv(name);
        }

        return null;
    }

    public bool CanResolve(string name)
    {
        if (_staticResolvers.ContainsKey(name))
        {
            return true;
        }

        return name.StartsWith("$randomInt ", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("$datetime ", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("$localDatetime ", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("$processEnv ", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("$dotenv ", StringComparison.OrdinalIgnoreCase);
    }

    private string? ResolveRandomInt(string name)
    {
        // Format: $randomInt min max
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return _random.Next().ToString();
        }

        if (int.TryParse(parts[1], out var min) && int.TryParse(parts[2], out var max))
        {
            return _random.Next(min, max + 1).ToString();
        }

        return _random.Next().ToString();
    }

    private static string? ResolveDatetime(string name, bool utc)
    {
        // Format: $datetime format [offset]
        // Example: $datetime "yyyy-MM-dd" 1 d
        var match = DatetimeFormatRegex().Match(name);
        if (!match.Success)
        {
            return utc ? DateTimeOffset.UtcNow.ToString("o") : DateTimeOffset.Now.ToString("o");
        }

        var format = match.Groups[1].Value;
        var dateTime = utc ? DateTimeOffset.UtcNow : DateTimeOffset.Now;

        // Handle offset if present
        if (match.Groups[2].Success && match.Groups[3].Success)
        {
            if (int.TryParse(match.Groups[2].Value, out var offsetValue))
            {
                var offsetUnit = match.Groups[3].Value.ToLowerInvariant();
                dateTime = offsetUnit switch
                {
                    "y" => dateTime.AddYears(offsetValue),
                    "m" => dateTime.AddMonths(offsetValue),
                    "w" => dateTime.AddDays(offsetValue * 7),
                    "d" => dateTime.AddDays(offsetValue),
                    "h" => dateTime.AddHours(offsetValue),
                    "n" => dateTime.AddMinutes(offsetValue),
                    "s" => dateTime.AddSeconds(offsetValue),
                    "ms" => dateTime.AddMilliseconds(offsetValue),
                    _ => dateTime
                };
            }
        }

        try
        {
            return dateTime.ToString(format, CultureInfo.InvariantCulture);
        }
        catch
        {
            return dateTime.ToString("o");
        }
    }

    private static string? ResolveProcessEnv(string name)
    {
        // Format: $processEnv VAR_NAME
        var parts = name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        return System.Environment.GetEnvironmentVariable(parts[1]);
    }

    private static string? ResolveDotEnv(string name)
    {
        // Format: $dotenv VAR_NAME
        var parts = name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        // Look for .env file in current directory
        var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (File.Exists(envFilePath))
        {
            var lines = File.ReadAllLines(envFilePath);
            var varName = parts[1];
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('#') || !trimmed.Contains('='))
                {
                    continue;
                }

                var equalsIndex = trimmed.IndexOf('=');
                var key = trimmed[..equalsIndex].Trim();
                if (string.Equals(key, varName, StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed[(equalsIndex + 1)..].Trim().Trim('"', '\'');
                }
            }
        }

        return null;
    }

    [GeneratedRegex(@"\$(?:local)?[Dd]atetime\s+""?([^""\s]+)""?(?:\s+(-?\d+)\s+([yMwdhnms]+))?", RegexOptions.IgnoreCase)]
    private static partial Regex DatetimeFormatRegex();
}
