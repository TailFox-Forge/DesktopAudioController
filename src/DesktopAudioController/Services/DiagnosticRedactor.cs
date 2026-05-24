using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DesktopAudioController.Services;

/// <summary>
/// 공개 이슈에 첨부할 수 있도록 로컬 경로와 식별자를 안정적으로 마스킹합니다.
/// </summary>
internal static class DiagnosticRedactor
{
    private static readonly Regex SensitiveKeyPattern = new(
        @"(?<key>\b(?:deviceId|sessionId|defaultDeviceId|groupingId|profileId|matchKey|path|backup|iconPath|executablePath|outputPath|settingsPath|logDirectory|snapshotPath)=)(?<value>.*?)(?=\s+\w+=|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GenericWindowsPathPattern = new(
        @"[A-Za-z]:\\(?:[^\s\\\r\n:]+\\)*[^\s\\\r\n:]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GenericUnixPathPattern = new(
        @"\/home\/[^\/\s]+(?:\/[^\s:]+)+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DevicePathPattern = new(
        @"\\Device\\[^\s\r\n""]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string RedactText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var sanitized = SensitiveKeyPattern.Replace(input, static match =>
        {
            var key = match.Groups["key"].Value;
            var value = match.Groups["value"].Value;
            return key + MaskKnownValue(key, value);
        });

        sanitized = GenericWindowsPathPattern.Replace(sanitized, static match => MaskPathValue(match.Value));
        sanitized = GenericUnixPathPattern.Replace(sanitized, static match => MaskPathValue(match.Value));
        sanitized = DevicePathPattern.Replace(sanitized, static match => MaskPathValue(match.Value));
        return sanitized;
    }

    public static string RedactJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        try
        {
            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(
                stream,
                new JsonWriterOptions { Indented = true }))
            {
                WriteRedactedJsonElement(writer, document.RootElement, propertyName: null);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch
        {
            return RedactText(json);
        }
    }

    public static string RedactPath(string path)
    {
        return MaskPathValue(path);
    }

    private static void WriteRedactedJsonElement(
        Utf8JsonWriter writer,
        JsonElement element,
        string? propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteRedactedJsonElement(writer, property.Value, property.Name);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteRedactedJsonElement(writer, item, propertyName);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(RedactJsonString(propertyName, element.GetString() ?? string.Empty));
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static string RedactJsonString(string? propertyName, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (IsPathProperty(propertyName))
        {
            return MaskPathValue(value);
        }

        if (IsIdentifierProperty(propertyName))
        {
            return MaskIdentifier(value);
        }

        return RedactText(value);
    }

    private static bool IsPathProperty(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        var normalized = NormalizePropertyName(propertyName);
        return normalized.Contains("path", StringComparison.Ordinal) ||
            normalized.Contains("directory", StringComparison.Ordinal);
    }

    private static bool IsIdentifierProperty(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        var normalized = NormalizePropertyName(propertyName);
        return normalized is "id" or "ids" or "matchkey" ||
            normalized.EndsWith("id", StringComparison.Ordinal) ||
            normalized.EndsWith("ids", StringComparison.Ordinal) ||
            normalized.Contains("deviceid", StringComparison.Ordinal) ||
            normalized.Contains("sessionid", StringComparison.Ordinal) ||
            normalized.Contains("groupingid", StringComparison.Ordinal);
    }

    private static string NormalizePropertyName(string propertyName)
    {
        var builder = new StringBuilder(propertyName.Length);
        foreach (var character in propertyName)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string MaskKnownValue(string key, string value)
    {
        return key switch
        {
            "deviceId=" or "sessionId=" or "defaultDeviceId=" or "groupingId=" or "profileId=" or "matchKey=" => MaskIdentifier(value),
            "path=" or "backup=" or "iconPath=" or "executablePath=" or "outputPath=" or "settingsPath=" or "logDirectory=" or "snapshotPath=" => MaskPathValue(value),
            _ => value
        };
    }

    private static string MaskIdentifier(string value)
    {
        var trimmed = value.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "[id:redacted]";
        }

        if (IsAlreadyMaskedIdentifier(trimmed))
        {
            return trimmed;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trimmed)));
        return $"[id:{hash[..8]}]";
    }

    private static string MaskPathValue(string value)
    {
        var trimmed = value.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "[path:redacted]";
        }

        if (TryGetAlreadyMaskedPath(trimmed, out var maskedPath))
        {
            return maskedPath;
        }

        if (TryUnwrapMaskedPath(trimmed, out var unwrappedPath))
        {
            trimmed = unwrappedPath;
        }

        var normalized = trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parts = normalized.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        var fileName = parts.LastOrDefault();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = Path.GetFileName(normalized);
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "redacted";
        }

        return $"[path:{fileName}]";
    }

    private static bool IsAlreadyMaskedIdentifier(string value)
    {
        return value.StartsWith("[id:", StringComparison.Ordinal) &&
            value.EndsWith(']');
    }

    private static bool TryGetAlreadyMaskedPath(string value, out string maskedPath)
    {
        maskedPath = string.Empty;
        if (!value.StartsWith("[path:", StringComparison.Ordinal) ||
            !value.EndsWith(']'))
        {
            return false;
        }

        var inner = value["[path:".Length..^1];
        if (string.IsNullOrWhiteSpace(inner) ||
            inner.Contains('\\', StringComparison.Ordinal) ||
            inner.Contains('/', StringComparison.Ordinal))
        {
            return false;
        }

        maskedPath = value;
        return true;
    }

    private static bool TryUnwrapMaskedPath(string value, out string unwrappedPath)
    {
        unwrappedPath = string.Empty;
        if (!value.StartsWith("[path:", StringComparison.Ordinal) ||
            !value.EndsWith(']'))
        {
            return false;
        }

        unwrappedPath = value["[path:".Length..^1];
        return true;
    }
}
