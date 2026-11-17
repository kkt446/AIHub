using System.Text;
using System.Text.Json;
using ZstdSharp;

namespace AIHub.Core;

public static class DshImporter
{
    public static string DefaultRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh");

    public static string SessionsRoot(string dshRoot) => Path.Combine(dshRoot, "sessions");

    public static string AttachmentPath(string dshRoot, string attachmentId)
    {
        var hash = attachmentId.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? attachmentId[7..] : attachmentId;
        return Path.Combine(dshRoot, "attachments", "v1", "objects", hash[..2], hash);
    }

    public static IEnumerable<string> EnumerateSessionFiles(string dshRoot)
    {
        var root = SessionsRoot(dshRoot);
        if (!Directory.Exists(root)) yield break;
        foreach (var proj in Directory.EnumerateDirectories(root))
        foreach (var dir in Directory.EnumerateDirectories(proj))
        {
            var f = Path.Combine(dir, "session.v4.jsonl.zstd");
            if (File.Exists(f)) yield return f;
        }
    }

    public static string? ReadCwd(string sessionFile)
    {
        foreach (var line in ReadLines(sessionFile, firstOnly: true))
        {
            try
            {
                var o = JsonDocument.Parse(line).RootElement;
                if (o.Str("type") == "session") return o.Str("cwd");
            }
            catch (JsonException) { }
            return null;
        }
        return null;
    }

    public static HubSession Load(string sessionFile, string dshRoot)
    {
        HubSession? session = null;
        var fallbackId = Path.GetFileName(Path.GetDirectoryName(sessionFile)!);

        foreach (var line in ReadLines(sessionFile, firstOnly: false))
        {
            JsonElement o;
            try { o = JsonDocument.Parse(line).RootElement; }
            catch (JsonException) { continue; }

            var type = o.Str("type");
            if (type == "session")
            {
                var id = o.Str("id") ?? fallbackId;
                session = new HubSession
                {
                    Client = Clients.Dsh,
                    SessionId = id.StartsWith("session-") ? id[8..] : id,
                    Cwd = o.Str("cwd") ?? "",
                    SourcePath = sessionFile,
                    Created = o.Time("createdAt") ?? default,
                };
                session.Updated = session.Created;
                continue;
            }
            session ??= new HubSession { Client = Clients.Dsh, SessionId = fallbackId, SourcePath = sessionFile };

            var time = o.Time("time") ?? session.Updated;
            if (time > session.Updated) session.Updated = time;
            if (!o.TryGetProperty("data", out var data)) continue;

            switch (type)
            {
                case "session/title":
                    if (data.Str("title") is { Length: > 0 } title) session.Title = title;
                    break;
                case "user/message":
                    if (data.TryGetProperty("source", out var src) && src.Str("kind") != "user") break;
                    ReadUser(data, time, session, dshRoot);
                    break;
                case "assistant/message":
                    if (data.TryGetProperty("message", out var msg))
                        foreach (var b in Blocks(msg))
                            if (b.Str("type") == "text" && b.Str("text") is { } t && t.Trim().Length > 0)
                                session.Messages.Add(new HubMessage(Roles.Assistant, time, t.Trim(), Array.Empty<string>()));
                    break;
                case "tool/call":
                    session.Messages.Add(new HubMessage(Roles.ToolCall, time,
                        Text.Shorten(data.Str("arguments") ?? "", 400), Array.Empty<string>(), data.Str("name") ?? "tool"));
                    break;
                case "tool/result":
                    if (data.TryGetProperty("message", out var rm))
                    {
                        var text = string.Join("\n", Blocks(rm).Where(b => b.Str("type") == "text").Select(b => b.Str("text") ?? ""));
                        session.Messages.Add(new HubMessage(Roles.ToolResult, time, Text.Shorten(text, 600), Array.Empty<string>()));
                    }
                    break;
            }
        }

        session ??= new HubSession { Client = Clients.Dsh, SessionId = fallbackId, SourcePath = sessionFile };
        if (session.Title == "")
            session.Title = Text.TitleFrom(session.Messages.FirstOrDefault(m => m.Role == Roles.User)?.Text ?? session.SessionId);
        return session;
    }

    static void ReadUser(JsonElement data, DateTimeOffset time, HubSession session, string dshRoot)
    {
        var texts = new List<string>();
        var attachments = new List<string>();
        foreach (var b in Blocks(data))
        {
            if (b.Str("type") == "text" && b.Str("text") is { } t) texts.Add(t);
            if (b.TryGetProperty("attachment", out var a) && a.Str("attachmentId") is { } aid)
            {
                var p = AttachmentPath(dshRoot, aid);
                if (File.Exists(p))
                {
                    var name = a.Str("name");
                    var ext = MediaExt(a.Str("mediaType"));
                    attachments.Add(p + "|" + (string.IsNullOrWhiteSpace(name) ? Path.GetFileName(p)[..12] + ext : name));
                }
            }
        }
        var text = string.Join("\n", texts).Trim();
        if (text.Length == 0 && attachments.Count == 0) return;
        session.Messages.Add(new HubMessage(Roles.User, time, text, attachments));
    }

    static string MediaExt(string? mediaType) => mediaType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        "application/pdf" => ".pdf",
        _ => ".bin",
    };

    static IEnumerable<JsonElement> Blocks(JsonElement holder) =>
        holder.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.Array
            ? c.EnumerateArray()
            : Enumerable.Empty<JsonElement>();

    // dsh appends one zstd frame per flush; the last frame may be mid-write, so keep whatever decoded cleanly.
    static IEnumerable<string> ReadLines(string path, bool firstOnly)
    {
        var buffer = new MemoryStream();
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var z = new DecompressionStream(fs);
            var chunk = new byte[64 * 1024];
            int n;
            while ((n = z.Read(chunk, 0, chunk.Length)) > 0)
            {
                buffer.Write(chunk, 0, n);
                if (firstOnly && Array.IndexOf(chunk, (byte)'\n', 0, n) >= 0) break;
            }
        }
        catch (Exception e) when (e is ZstdException or IOException or InvalidDataException) { }

        var text = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        foreach (var line in text.Split('\n'))
        {
            if (line.Trim().Length == 0) continue;
            yield return line;
            if (firstOnly) yield break;
        }
    }
}
