using System.Text.Json;

namespace HttpFileParser.Environment;

/// <summary>
/// Parses environment files in both VS 2022 (http-client.env.json) and
/// VS Code (rest-client.environmentVariables) formats.
/// </summary>
public sealed class EnvironmentFileParser
{
    public EnvironmentFile Parse(string jsonContent, string? filePath = null)
    {
        var environments = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? sharedVariables = null;

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            foreach (var envProperty in root.EnumerateObject())
            {
                var envName = envProperty.Name;
                var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (envProperty.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var varProperty in envProperty.Value.EnumerateObject())
                    {
                        var value = GetStringValue(varProperty.Value);
                        if (value != null)
                        {
                            variables[varProperty.Name] = value;
                        }
                    }
                }

                if (envName == "$shared")
                {
                    sharedVariables = variables;
                }
                else
                {
                    environments[envName] = variables;
                }
            }
        }
        catch (JsonException)
        {
            // Return empty environment file on parse error
        }

        return new EnvironmentFile(environments, sharedVariables, filePath);
    }

    public EnvironmentFile ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new EnvironmentFile(
                new Dictionary<string, IReadOnlyDictionary<string, string>>(),
                null,
                filePath);
        }

        var content = File.ReadAllText(filePath);
        return Parse(content, filePath);
    }

    public EnvironmentFile ParseVsCodeSettings(string jsonContent, string? filePath = null)
    {
        var environments = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? sharedVariables = null;

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // Look for rest-client.environmentVariables
            if (root.TryGetProperty("rest-client.environmentVariables", out var envVars))
            {
                foreach (var envProperty in envVars.EnumerateObject())
                {
                    var envName = envProperty.Name;
                    var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    if (envProperty.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var varProperty in envProperty.Value.EnumerateObject())
                        {
                            var value = GetStringValue(varProperty.Value);
                            if (value != null)
                            {
                                variables[varProperty.Name] = value;
                            }
                        }
                    }

                    if (envName == "$shared")
                    {
                        sharedVariables = variables;
                    }
                    else
                    {
                        environments[envName] = variables;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Return empty environment file on parse error
        }

        return new EnvironmentFile(environments, sharedVariables, filePath);
    }

    private static string? GetStringValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}
