using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIHub.Core;

/// <summary>
/// 把归档的会话导出为 Web 控制台（web/）可直接导入的 JSON，附图为文件名引用。
/// </summary>
public static class WebExport
{
    public static string Build(string workspace)
    {
        var cwd = Path.GetFullPath(workspace).TrimEnd('\\');
        var store = new HubStore(WorkspaceHub.HubRoot(cwd));
        var sessions = new JsonArray();
        foreach (var meta in store.List())
        {
            var messages = new JsonArray();
            var i = 0;
            foreach (var m in ReadMessages(store, meta.Key))
            {
                var item = new JsonObject
                {
                    ["id"] = $"{meta.Key}-{i++}",
                    ["role"] = m.Role,
                    ["text"] = m.Text,
                };
                if (m.ToolName is { Length: > 0 }) item["tool"] = m.ToolName;
                if (m.Attachments.Count > 0)
                {
                    var atts = new JsonArray();
                    foreach (var a in m.Attachments)
                        atts.Add(new JsonObject { ["name"] = a, ["kind"] = IsImage(a) ? "image" : "file" });
                    item["attachments"] = atts;
                }
                messages.Add(item);
            }
            sessions.Add(new JsonObject
            {
                ["key"] = meta.Key,
                ["client"] = meta.Client,
                ["title"] = meta.Title,
                ["updated"] = meta.Updated.ToString("o"),
                ["messages"] = messages,
            });
        }

        var root = new JsonObject
        {
            ["workspace"] = cwd,
            ["generatedAt"] = DateTimeOffset.Now.ToString("o"),
            ["sessions"] = sessions,
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    static IEnumerable<HubMessage> ReadMessages(HubStore store, string key)
    {
        var path = Path.Combine(store.Root, "sessions", key, "messages.jsonl");
        if (!File.Exists(path)) yield break;
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0) continue;
            JsonNode? node = null;
            try { node = JsonNode.Parse(line); }
            catch (JsonException) { continue; }
            if (node is not JsonObject o) continue;
            var role = o["role"]?.GetValue<string>() ?? "";
            if (role.Length == 0) continue;
            var time = DateTimeOffset.TryParse(o["time"]?.GetValue<string>(), out var t) ? t : default;
            var atts = new List<string>();
            if (o["attachments"] is JsonArray arr)
                foreach (var a in arr)
                    if (a?.GetValue<string>() is { Length: > 0 } name) atts.Add(name);
            yield return new HubMessage(role, time, o["text"]?.GetValue<string>() ?? "", atts, o["tool"]?.GetValue<string>());
        }
    }

    static bool IsImage(string name) => Path.GetExtension(name).ToLowerInvariant() is
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp";

    public static void Write(string workspace, string outPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
        if (dir is { Length: > 0 }) Directory.CreateDirectory(dir);
        File.WriteAllText(outPath, Build(workspace), new UTF8Encoding(false));
    }
}
