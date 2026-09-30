using LiveSplit.Model;
using System;
using System.Globalization;
using System.Text.Json;

namespace LiveSplit.WsServer.Protocol;

/// <summary>
///     Arguments of a request. They come either from the JSON <c>args</c> object
///     (<c>{ "action": "setcomparison", "args": { "comparison": "Personal Best" } }</c>),
///     a JSON array (<c>"args": ["Personal Best"]</c>) or, for plain text messages, from
///     everything after the action (<c>setcomparison Personal Best</c>).
/// </summary>
public sealed class CommandArgs
{
    public static readonly CommandArgs Empty = new(null, null);

    private readonly JsonElement? json;

    /// <summary>
    ///     The raw text after the action for plain text messages, or a string given as <c>args</c>.
    /// </summary>
    public string Text { get; }

    public bool IsText => Text != null;

    public CommandArgs(JsonElement? json, string text)
    {
        this.json = json;
        Text = string.IsNullOrEmpty(text) ? null : text;
    }

    public static CommandArgs FromText(string text)
    {
        return new CommandArgs(null, text);
    }

    public static CommandArgs FromJson(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object or JsonValueKind.Array => new CommandArgs(element.Clone(), null),
            JsonValueKind.String => new CommandArgs(null, element.GetString()),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => new CommandArgs(null, element.GetRawText()),
            _ => Empty,
        };
    }

    /// <summary>
    ///     Looks an argument up by name (JSON object), by position (JSON array), or
    ///     returns the whole text for the first positional argument of a plain text message.
    /// </summary>
    public bool TryGet(string name, int position, out JsonElement value)
    {
        value = default;
        if (json is not JsonElement element)
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
                }
            }

            return false;
        }

        if (element.ValueKind == JsonValueKind.Array && position >= 0 && position < element.GetArrayLength())
        {
            value = element[position];
            return value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
        }

        return false;
    }

    public bool Has(string name, int position = 0)
    {
        return TryGet(name, position, out _) || (position == 0 && IsText);
    }

    public string GetString(string name, int position = 0)
    {
        if (TryGet(name, position, out JsonElement value))
        {
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        }

        return position == 0 ? Text : null;
    }

    public string RequireString(string name, int position = 0)
    {
        string value = GetString(name, position);
        if (string.IsNullOrEmpty(value))
        {
            throw CommandException.InvalidArgs($"Missing argument '{name}'.");
        }

        return value;
    }

    public int? GetInt(string name, int position = 0)
    {
        if (TryGet(name, position, out JsonElement value) && value.ValueKind == JsonValueKind.Number)
        {
            if (value.TryGetInt32(out int number))
            {
                return number;
            }

            throw CommandException.InvalidArgs($"Argument '{name}' must be an integer.");
        }

        string text = GetString(name, position);
        if (text == null)
        {
            return null;
        }

        if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }

        throw CommandException.InvalidArgs($"Argument '{name}' must be an integer.");
    }

    public bool? GetBool(string name, int position = 0)
    {
        if (TryGet(name, position, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        string text = GetString(name, position);
        if (text == null)
        {
            return null;
        }

        return text.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "on" => true,
            "false" or "0" or "no" or "off" => false,
            _ => throw CommandException.InvalidArgs($"Argument '{name}' must be a boolean."),
        };
    }

    /// <summary>
    ///     Reads a time either as a number of milliseconds or as a LiveSplit time string
    ///     such as <c>1:23:45.678</c>. <c>"-"</c> and <c>null</c> mean "no time".
    /// </summary>
    public TimeSpan? GetTime(string name, int position = 0)
    {
        if (TryGet(name, position, out JsonElement value) && value.ValueKind == JsonValueKind.Number)
        {
            return TimeSpan.FromMilliseconds(value.GetDouble());
        }

        string text = GetString(name, position)?.Trim();
        if (string.IsNullOrEmpty(text) || text == "-")
        {
            return null;
        }

        try
        {
            return TimeSpanParser.Parse(text);
        }
        catch (Exception)
        {
            throw CommandException.InvalidArgs($"Argument '{name}' is not a valid time: {text}");
        }
    }

    public TimeSpan RequireTime(string name, int position = 0)
    {
        return GetTime(name, position) ?? throw CommandException.InvalidArgs($"Missing argument '{name}'.");
    }
}
