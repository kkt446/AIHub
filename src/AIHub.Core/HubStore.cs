using System.Text;
using System.Text.Json;

namespace AIHub.Core;

public sealed class HubStore
{
    public string Root { get; }

    public HubStore(string root)
    {
        Root = root;
        Directory.CreateDirectory(Root);
    }

    string SessionDir(string key) => Path.Combine(Root, "sessions", key);
    public string AttachmentDir(string key) => Path.Combine(SessionDir(key), "attachments");
    string MetaPath(string key) => Path.Combine(SessionDir(key), "session.json");

    public SessionMeta Save(HubSession s)
    {
        var dir = SessionDir(s.Key);
        Directory.CreateDirectory(dir);
        var attDir = AttachmentDir(s.Key);
        Directory.CreateDirectory(attDir);

        foreach (var m in s.Messages)
        {
            foreach (var spec in m.Attachments)
            {
                var parts = spec.Split('|', 2);
                var src = parts[0];
                var name = parts.Length > 1 ? parts[1] : Path.GetFileName(src);
                if (!File.Exists(src)) continue;
                var dst = Path.Combine(attDir, Sanitize(name));
                if (File.Exists(dst)) continue;
                try { File.Copy(src, dst, overwrite: false); } catch (IOException) { }
            }
        }

        WriteAtomic(Path.Combine(dir, "messages.jsonl"), sb =>
        {
            foreach (var m in s.Messages)
                sb.AppendLine(JsonSerializer.Serialize(new
                {
                    role = m.Role,
                    time = m.Time,
                    text = m.Text,
                    attachments = m.Attachments.Select(a => Path.GetFileName(a.Split('|')[0])).ToArray(),
                    tool = m.ToolName,
                }, JsonLineOpts));
        });

        WriteAtomic(Path.Combine(dir, "transcript.md"), sb =>
        {
            sb.AppendLine($"# {s.Title}").AppendLine();
            sb.AppendLine($"- client: {Clients.DisplayName(s.Client)}").AppendLine($"- session: {s.SessionId}");
            sb.AppendLine($"- cwd: {s.Cwd}").AppendLine($"- source: {s.SourcePath}").AppendLine();
            foreach (var m in s.Messages)
            {
                switch (m.Role)
                {
                    case Roles.User:
                        sb.AppendLine("## 用户").AppendLine().AppendLine(m.Text).AppendLine();
                        break;
                    case Roles.Assistant:
                        sb.AppendLine("## 助手").AppendLine().AppendLine(m.Text).AppendLine();
                        break;
                    case Roles.ToolCall:
                        sb.AppendLine($"- 工具 {m.ToolName}: `{Text.OneLine(m.Text, 160)}`");
                        break;
                    case Roles.ToolResult:
                        sb.AppendLine($"  - 结果: {Text.OneLine(m.Text, 160)}");
                        break;
                }
            }
        });

        var meta = new SessionMeta
        {
            Client = s.Client,
            SessionId = s.SessionId,
            Title = s.Title,
            Cwd = s.Cwd,
            SourcePath = s.SourcePath,
            Created = s.Created,
            Updated = s.Updated,
            MessageCount = s.Messages.Count(m => m.Role is Roles.User or Roles.Assistant),
            AttachmentCount = s.Messages.Sum(m => m.Attachments.Count),
            LastUserText = s.Messages.LastOrDefault(m => m.Role == Roles.User)?.Text ?? "",
            LastAssistantText = s.Messages.LastOrDefault(m => m.Role == Roles.Assistant)?.Text ?? "",
            SyncedAt = DateTimeOffset.Now,
        };
        WriteAtomic(MetaPath(s.Key), sb => sb.Append(JsonSerializer.Serialize(meta, JsonOpts)));
        return meta;
    }

    public IReadOnlyList<SessionMeta> List()
    {
        var dir = Path.Combine(Root, "sessions");
        if (!Directory.Exists(dir)) return Array.Empty<SessionMeta>();
        var list = new List<SessionMeta>();
        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            var f = Path.Combine(d, "session.json");
            if (!File.Exists(f)) continue;
            try { list.Add(JsonSerializer.Deserialize<SessionMeta>(File.ReadAllText(f), JsonOpts)!); }
            catch (JsonException) { }
        }
        return list.OrderByDescending(m => m.Updated).ToList();
    }

    static string Sanitize(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    static readonly JsonSerializerOptions JsonLineOpts = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    static void WriteAtomic(string path, Action<StringBuilder> fill)
    {
        var sb = new StringBuilder();
        fill(sb);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }
}
