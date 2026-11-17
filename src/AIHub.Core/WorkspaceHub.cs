using System.Text;
using System.Text.Json;

namespace AIHub.Core;

public sealed record HookInput(
    string Event,
    string? SessionId,
    string? TranscriptPath,
    string? Cwd,
    string? Prompt,
    string Raw);

public static class WorkspaceHub
{
    public static string HubDir(string workspace) => Path.Combine(workspace, ".aihub");
    public static string HubRoot(string workspace) => Path.Combine(HubDir(workspace), "hub");
    public static string HandoffPath(string workspace) => Path.Combine(HubDir(workspace), "handoff.md");
    public static string ContextPath(string workspace) => Path.Combine(workspace, "AGENTS.local.md");

    public static HookInput ReadHookInput()
    {
        string raw = "";
        try { raw = Console.In.ReadToEnd(); } catch { }
        string? Get(string name)
        {
            try
            {
                using var d = JsonDocument.Parse(raw);
                var root = d.RootElement;
                return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                    ? v.GetString()
                    : null;
            }
            catch (JsonException) { return null; }
        }
        var eventName = Get("hook_event_name") ?? "";
        return new HookInput(eventName, Get("session_id"), Get("transcript_path"), Get("cwd"), Get("prompt"), raw);
    }

    public static SessionMeta? ImportTranscript(string workspace, string transcriptPath)
    {
        if (!File.Exists(transcriptPath)) return null;
        var session = QoderImporter.Load(transcriptPath);
        var meta = new HubStore(HubRoot(workspace)).Save(session);

        if (!string.IsNullOrWhiteSpace(session.Cwd) &&
            Path.GetFullPath(session.Cwd).TrimEnd('\\').Equals(Path.GetFullPath(workspace).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return meta;
        return meta;
    }

    public static int ImportDsh(string workspace, string? dshRoot = null)
    {
        dshRoot ??= DshImporter.DefaultRoot;
        var store = new HubStore(HubRoot(workspace));
        var cwd = Path.GetFullPath(workspace).TrimEnd('\\');
        int n = 0;
        foreach (var f in DshImporter.EnumerateSessionFiles(dshRoot))
        {
            var sCwd = DshImporter.ReadCwd(f);
            if (string.IsNullOrWhiteSpace(sCwd) || !Path.GetFullPath(sCwd).TrimEnd('\\').Equals(cwd, StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                var s = DshImporter.Load(f, dshRoot);
                if (s.Messages.Count == 0) continue;
                store.Save(s);
                n++;
            }
            catch (IOException) { }
        }
        return n;
    }

    public static string BuildHandoff(string workspace, int maxChars = 3500)
    {
        var store = new HubStore(HubRoot(workspace));
        var sessions = store.List();
        var sb = new StringBuilder();
        sb.AppendLine("# 共享工作区交接（AIHub 自动生成）").AppendLine();
        sb.AppendLine($"工作区：{workspace}");
        sb.AppendLine($"生成时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm}").AppendLine();
        if (sessions.Count == 0)
        {
            sb.AppendLine("还没有任何客户端写入会话。");
            return sb.ToString();
        }

        sb.AppendLine("## 最近会话");
        foreach (var s in sessions.Take(12))
            sb.AppendLine($"- [{Clients.DisplayName(s.Client)}] {s.Title}（更新 {s.Updated.ToLocalTime():MM-dd HH:mm}，{s.MessageCount} 条消息，{s.AttachmentCount} 个附件）");
        sb.AppendLine();

        var latest = sessions.First();
        sb.AppendLine($"## 最近一次进展：{Clients.DisplayName(latest.Client)} · {latest.Title}");
        if (latest.LastUserText.Length > 0)
            sb.AppendLine($"用户上一条：{Text.Shorten(latest.LastUserText, 600)}").AppendLine();
        if (latest.LastAssistantText.Length > 0)
            sb.AppendLine($"助手上一条：{Text.Shorten(latest.LastAssistantText, 900)}").AppendLine();
        sb.AppendLine($"完整记录：{Path.Combine(HubRoot(workspace), "sessions", latest.Key, "transcript.md")}");

        var text = sb.ToString();
        return text.Length <= maxChars ? text : text[..maxChars] + "\n（已截断）\n";
    }

    public static void WriteHandoff(string workspace)
    {
        var path = HandoffPath(workspace);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, BuildHandoff(workspace), new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
        WriteDshContext(workspace);
    }

    // dsh loads project instruction files from the project root down to the session cwd.
    static void WriteDshContext(string workspace)
    {
        var handoff = BuildHandoff(workspace);
        var body = "<!-- AIHub: 本文件由 AIHub 自动生成，请勿手工编辑 -->\n" + handoff;
        var path = ContextPath(workspace);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, body, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    public static string SessionStartJson(string workspace)
    {
        var ctx = BuildHandoff(workspace);
        var payload = new
        {
            hookSpecificOutput = new
            {
                hookEventName = "SessionStart",
                additionalContext = ctx,
            },
        };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }
}
