using System;
using System.Text.Json;

namespace LiveSplit.WsServer.Protocol;

/// <summary>
///     A parsed client message.
/// </summary>
public sealed class Request
{
    /// <summary>
    ///     The optional <c>id</c> sent by the client, echoed back in the response.
    /// </summary>
    public JsonElement? Id { get; }

    /// <summary>
    ///     The action in lower case.
    /// </summary>
    public string Action { get; }

    public CommandArgs Args { get; }

    public Request(JsonElement? id, string action, CommandArgs args)
    {
        Id = id;
        Action = action;
        Args = args ?? CommandArgs.Empty;
    }

    /// <summary>
    ///     Parses either a JSON message (<c>{ "id": 1, "action": "split", "args": { ... } }</c>)
    ///     or a plain text message (<c>split</c>, <c>setgametime 1:23.45</c>) like the ones
    ///     accepted by LiveSplit's built-in server.
    /// </summary>
    /// <exception cref="RequestParseException">The message is not a valid request.</exception>
    public static Request Parse(string message)
    {
        string text = message?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            throw new RequestParseException(null, "The message is empty.");
        }

        if (text[0] != '{')
        {
            string[] parts = text.Split([' '], 2);
            return new Request(null, parts[0].ToLowerInvariant(), CommandArgs.FromText(parts.Length > 1 ? parts[1] : null));
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException e)
        {
            throw new RequestParseException(null, $"Invalid JSON: {e.Message}");
        }

        using (document)
        {
            JsonElement root = document.RootElement;

            JsonElement? id = null;
            if (root.TryGetProperty("id", out JsonElement idElement))
            {
                if (idElement.ValueKind is not JsonValueKind.String and not JsonValueKind.Number and not JsonValueKind.Null)
                {
                    throw new RequestParseException(null, "'id' must be a string or a number.");
                }

                id = idElement.Clone();
            }

            if (!root.TryGetProperty("action", out JsonElement actionElement)
                || actionElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(actionElement.GetString()))
            {
                throw new RequestParseException(id, "'action' must be a non empty string.");
            }

            CommandArgs args = CommandArgs.Empty;
            if (root.TryGetProperty("args", out JsonElement argsElement)
                // "data" was accepted (and ignored) by the original protocol.
                || root.TryGetProperty("data", out argsElement))
            {
                args = CommandArgs.FromJson(argsElement);
            }

            return new Request(id, actionElement.GetString().Trim().ToLowerInvariant(), args);
        }
    }
}

public sealed class RequestParseException : Exception
{
    public JsonElement? Id { get; }

    public RequestParseException(JsonElement? id, string message)
        : base(message)
    {
        Id = id;
    }
}
