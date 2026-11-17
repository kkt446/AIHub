namespace AIHub.Core;

public static class Clients
{
    public const string Qoder = "qoder";
    public const string QoderCn = "qodercn";
    public const string Dsh = "dsh";

    public static string DisplayName(string client) => client switch
    {
        Qoder => "Qoder",
        QoderCn => "Qoder CN",
        Dsh => "dsh",
        _ => client,
    };
}

public static class Roles
{
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string ToolCall = "tool_call";
    public const string ToolResult = "tool_result";
}

public sealed record HubMessage(
    string Role,
    DateTimeOffset Time,
    string Text,
    IReadOnlyList<string> Attachments,
    string? ToolName = null);

public sealed class HubSession
{
    public required string Client { get; init; }
    public required string SessionId { get; init; }
    public string Title { get; set; } = "";
    public string Cwd { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset Updated { get; set; }
    public List<HubMessage> Messages { get; } = new();

    public string Key => $"{Client}_{SessionId}";
}

public sealed class SessionMeta
{
    public string Client { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Cwd { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset Updated { get; set; }
    public int MessageCount { get; set; }
    public int AttachmentCount { get; set; }
    public string LastUserText { get; set; } = "";
    public string LastAssistantText { get; set; } = "";
    public DateTimeOffset SyncedAt { get; set; }

    public string Key => $"{Client}_{SessionId}";
}
