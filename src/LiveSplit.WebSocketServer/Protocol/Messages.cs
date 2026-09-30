using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiveSplit.WsServer.Protocol;

// Messages of protocol version 2. Every message has a "type".

public sealed class ErrorInfo
{
    public string Code { get; set; }
    public string Message { get; set; }
}

public sealed class ResponseMessage
{
    public string Type => "response";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Id { get; set; }
    public string Action { get; set; }
    public bool Ok { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object Data { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErrorInfo Error { get; set; }

    public static ResponseMessage Success(Request request, object data)
    {
        return new ResponseMessage { Id = request.Id, Action = request.Action, Ok = true, Data = data };
    }

    public static ResponseMessage Failure(JsonElement? id, string action, string code, string message)
    {
        return new ResponseMessage
        {
            Id = id,
            Action = action,
            Ok = false,
            Error = new ErrorInfo { Code = code, Message = message },
        };
    }
}

public sealed class EventMessage
{
    public string Type => "event";
    public string Event { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object Data { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object State { get; set; }
}

public sealed class HelloMessage
{
    public string Type => "hello";
    public int ProtocolVersion { get; set; }
    public string ComponentVersion { get; set; }
    public string LiveSplitVersion { get; set; }
    public bool ReadOnly { get; set; }
    public bool FileCommandsAllowed { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object State { get; set; }
}
