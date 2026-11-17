using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIHub.Core;

public static class QoderImporter
{
    static readonly Regex LocalImagePath = new(
        @"[A-Za-z]:\\[^\r\n""<>|*?]+?\.(?:png|jpe?g|gif|webp|bmp)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string ClientFromTranscript(string transcriptPath) =>
        transcriptPath.Contains(@"\.qoder-cn\", StringComparison.OrdinalIgnoreCase)
            ? Clients.QoderCn
            : Clients.Qoder;

    public static HubSession Load(string transcriptPath)
    {
        var sessionId = Path.GetFileNameWithoutExtension(transcriptPath);
        var session = new HubSession
        {
            Client = ClientFromTranscript(transcriptPath),
            SessionId = sessionId,
            SourcePath = transcriptPath,
        };

        foreach (var line in ReadLinesShared(transcriptPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            JsonElement o;
            try { o = JsonDocument.Parse(line).RootElement; }
            catch (JsonException) { continue; }

            var type = o.Str("type");
            var time = o.Time("timestamp");
            if (time is { } t)
            {
                if (session.Created == default || t < session.Created) session.Created = t;
                if (t > session.Updated) session.Updated = t;
            }

            switch (type)
            {
                case "workspace-directories" when session.Cwd == "":
                    if (o.TryGetProperty("directories", out var dirs) && dirs.ValueKind == JsonValueKind.Array && dirs.GetArrayLength() > 0)
                        session.Cwd = dirs[0].GetString() ?? "";
                    break;
                case "custom-title" or "summary" or "ai-title":
                    var title = o.Str("customTitle") ?? o.Str("summary") ?? o.Str("title");
                    if (!string.IsNullOrWhiteSpace(title)) session.Title = title!;
                    break;
                case "user":
                    if (o.Bool("isMeta") || o.Bool("isSidechain")) break;
                    ReadUser(o, time ?? session.Updated, session);
                    break;
                case "assistant":
                    if (o.Bool("isSidechain")) break;
                    ReadAssistant(o, time ?? session.Updated, session);
                    break;
            }
        }

        if (session.Title == "")
            session.Title = Text.TitleFrom(session.Messages.FirstOrDefault(m => m.Role == Roles.User)?.Text ?? sessionId);
        return session;
    }

    static void ReadUser(JsonElement o, DateTimeOffset time, HubSession session)
    {
        if (!o.TryGetProperty("message", out var msg)) return;
        var content = msg.TryGetProperty("content", out var c) ? c : default;

        if (content.ValueKind == JsonValueKind.Array)
        {
            var toolResults = content.EnumerateArray().Where(b => b.Str("type") == "tool_result").ToList();
            if (toolResults.Count > 0)
            {
                foreach (var r in toolResults)
                    session.Messages.Add(new HubMessage(Roles.ToolResult, time, Text.Shorten(FlattenContent(r), 600), Array.Empty<string>()));
                return;
            }
        }

        var rawText = content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? "",
            JsonValueKind.Array => string.Join("\n", content.EnumerateArray().Where(b => b.Str("type") == "text").Select(b => b.Str("text") ?? "")),
            _ => "",
        };

        string text = rawText;
        if (o.TryGetProperty("humanInput", out var hi) && hi.Str("text") is { Length: > 0 } human)
            text = human;

        var attachments = LocalImagePath.Matches(rawText)
            .Select(m => m.Value.Trim())
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        text = Text.StripAttachmentRefs(text);
        if (text.Trim().Length == 0 && attachments.Count == 0) return;
        session.Messages.Add(new HubMessage(Roles.User, time, text.Trim(), attachments));
    }

    static void ReadAssistant(JsonElement o, DateTimeOffset time, HubSession session)
    {
        if (!o.TryGetProperty("message", out var msg) || !msg.TryGetProperty("content", out var content)) return;
        if (content.ValueKind != JsonValueKind.Array) return;

        foreach (var b in content.EnumerateArray())
        {
            switch (b.Str("type"))
            {
                case "text":
                    var t = b.Str("text") ?? "";
                    if (t.Trim().Length > 0)
                        session.Messages.Add(new HubMessage(Roles.Assistant, time, t.Trim(), Array.Empty<string>()));
                    break;
                case "tool_use":
                    var name = b.Str("name") ?? "tool";
                    var input = b.TryGetProperty("input", out var inp) ? inp.GetRawText() : "";
                    session.Messages.Add(new HubMessage(Roles.ToolCall, time, Text.Shorten(input, 400), Array.Empty<string>(), name));
                    break;
            }
        }
    }

    static string FlattenContent(JsonElement block)
    {
        if (!block.TryGetProperty("content", out var c)) return "";
        if (c.ValueKind == JsonValueKind.String) return c.GetString() ?? "";
        if (c.ValueKind != JsonValueKind.Array) return "";
        var sb = new StringBuilder();
        foreach (var x in c.EnumerateArray())
            if (x.Str("type") == "text") sb.AppendLine(x.Str("text"));
        return sb.ToString();
    }

    internal static IEnumerable<string> ReadLinesShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(fs, Encoding.UTF8);
        string? line;
        while ((line = reader.ReadLine()) != null) yield return line;
    }
}
