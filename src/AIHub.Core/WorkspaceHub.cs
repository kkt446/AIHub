using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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
        var ws = Path.GetFullPath(workspace).TrimEnd('\\');
        if (!string.IsNullOrWhiteSpace(session.Cwd) &&
            !Path.GetFullPath(session.Cwd).TrimEnd('\\').Equals(ws, StringComparison.OrdinalIgnoreCase))
            return null;
        return new HubStore(HubRoot(workspace)).Save(session);
    }

    // Qoder 把项目目录名编码为 cwd 的非字母数字字符替换成 '-'，据此快速定位，避免每次全量扫描。
    static string EncodeProjectDir(string path)
    {
        var sb = new StringBuilder(path.Length);
        foreach (var ch in path.TrimEnd('\\'))
            sb.Append(char.IsAsciiLetterOrDigit(ch) || ch == '-' ? ch : '-');
        return sb.ToString();
    }

    public static int ImportQoder(string workspace)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var ws = Path.GetFullPath(workspace).TrimEnd('\\');
        var store = new HubStore(HubRoot(workspace));
        var encoded = EncodeProjectDir(ws);
        var roots = new[]
        {
            Path.Combine(home, ".qoder", "projects"),
            Path.Combine(home, ".qoder-cn", "projects"),
        };

        var dirs = new List<string>();
        foreach (var root in roots)
        {
            var candidate = Path.Combine(root, encoded);
            if (Directory.Exists(candidate)) dirs.Add(candidate);
        }
        if (dirs.Count == 0)
        {
            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var d in Directory.EnumerateDirectories(root))
                {
                    try
                    {
                        if (Directory.EnumerateFiles(d, "*.jsonl").Any()) dirs.Add(d);
                    }
                    catch (IOException) { }
                }
            }
        }

        int n = 0;
        foreach (var dir in dirs)
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var s = QoderImporter.Load(f);
                    if (s.Messages.Count == 0) continue;
                    if (!string.IsNullOrWhiteSpace(s.Cwd) &&
                        !Path.GetFullPath(s.Cwd).TrimEnd('\\').Equals(ws, StringComparison.OrdinalIgnoreCase))
                        continue;
                    store.Save(s);
                    n++;
                }
                catch (IOException) { }
            }
        }
        return n;
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

    static string? ClientRoot(string client)
    {
        var (envName, folder) = client switch
        {
            Clients.Qoder => ("AIHUB_QODER_HOME", ".qoder"),
            Clients.QoderCn => ("AIHUB_QODER_CN_HOME", ".qoder-cn"),
            _ => ("", ""),
        };
        if (folder.Length == 0) return null;
        var custom = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(custom)) return custom;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), folder);
    }

    public static string? ClientProjectDir(string workspace, string client)
    {
        var root = ClientRoot(client);
        return root is null
            ? null
            : Path.Combine(root, "projects", EncodeProjectDir(Path.GetFullPath(workspace).TrimEnd('\\')));
    }

    /// <summary>
    /// 把 fromClient 的会话文件复制进 toClient 的项目目录，让对方的任务列表里也能直接看到这些对话。
    /// 只做新增，不覆盖已有文件；复制记录写入 .aihub-mirror.json，可用 UndoMirror 撤销。
    /// </summary>
    public static IReadOnlyList<string> MirrorSessions(string workspace, string fromClient, string toClient)
    {
        var from = ClientProjectDir(workspace, fromClient);
        var to = ClientProjectDir(workspace, toClient);
        if (from is null || to is null || !Directory.Exists(from)) return Array.Empty<string>();
        Directory.CreateDirectory(to);

        var manifestPath = Path.Combine(to, ".aihub-mirror.json");
        var manifest = LoadManifest(manifestPath);
        var added = new List<string>();
        foreach (var f in Directory.EnumerateFiles(from, "*.jsonl", SearchOption.TopDirectoryOnly))
        {
            var id = Path.GetFileNameWithoutExtension(f);
            if (!IsSafeSessionId(id)) continue;
            var dst = Path.Combine(to, id + ".jsonl");
            if (File.Exists(dst)) continue;
            try
            {
                File.Copy(f, dst);
                var srcDir = Path.Combine(from, id);
                if (Directory.Exists(srcDir)) CopyDir(srcDir, Path.Combine(to, id));
            }
            catch (IOException) { continue; }
            manifest.Add(new JsonObject { ["client"] = fromClient, ["id"] = id, ["at"] = DateTimeOffset.Now.ToString("o") });
            added.Add(id);
        }
        if (added.Count > 0) WriteManifest(manifestPath, manifest);
        return added;
    }

    public static int UndoMirror(string workspace, string toClient)
    {
        var to = ClientProjectDir(workspace, toClient);
        if (to is null) return 0;
        var manifestPath = Path.Combine(to, ".aihub-mirror.json");
        if (!File.Exists(manifestPath)) return 0;
        int n = 0;
        foreach (var node in LoadManifest(manifestPath))
        {
            var id = (node as JsonObject)?["id"]?.GetValue<string>() ?? "";
            if (!IsSafeSessionId(id)) continue;
            TryDeleteFile(Path.Combine(to, id + ".jsonl"));
            TryDeleteDir(Path.Combine(to, id));
            n++;
        }
        TryDeleteFile(manifestPath);
        return n;
    }

    static bool IsSafeSessionId(string id) =>
        id.Length is > 0 and < 80 && !id.Contains("..") &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    static JsonArray LoadManifest(string path)
    {
        try { return JsonNode.Parse(File.ReadAllText(path)) as JsonArray ?? new JsonArray(); }
        catch (Exception e) when (e is IOException or JsonException) { return new JsonArray(); }
    }

    static void WriteManifest(string path, JsonArray manifest)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.EnumerateFiles(src))
            File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), overwrite: false);
        foreach (var d in Directory.EnumerateDirectories(src))
            CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
    }

    static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    static void TryDeleteDir(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
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
        return HookJson("SessionStart", ctx);
    }

    static string HookJson(string eventName, string context) =>
        JsonSerializer.Serialize(new
        {
            hookSpecificOutput = new
            {
                hookEventName = eventName,
                additionalContext = context,
            },
        }, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

    public static string BuildLiveDigest(string workspace, int maxChars = 900)
    {
        var store = new HubStore(HubRoot(workspace));
        var sessions = store.List();
        if (sessions.Count == 0) return "";
        var sb = new StringBuilder();
        sb.AppendLine("# 共享工作区最新进展（AIHub 自动注入）");
        foreach (var s in sessions.Take(4))
        {
            sb.Append($"- [{Clients.DisplayName(s.Client)}] {s.Title}（{s.Updated.ToLocalTime():MM-dd HH:mm}）");
            if (s.LastUserText.Length > 0) sb.Append(" 用户：" + Text.Shorten(s.LastUserText, 160));
            if (s.LastAssistantText.Length > 0) sb.Append(" 助手：" + Text.Shorten(s.LastAssistantText, 200));
            sb.AppendLine();
        }
        var text = sb.ToString();
        return text.Length <= maxChars ? text : text[..maxChars] + "\n（已截断）";
    }

    public static string UserPromptJson(string workspace) =>
        HookJson("UserPromptSubmit", BuildLiveDigest(workspace));

    /// <summary>在 ${workspace}/.qoder/settings.json 写入 SessionStart / UserPromptSubmit / Stop 钩子（两个 Qoder 客户端共用）。</summary>
    public static string EnsureHooks(string workspace, string? exePath = null)
    {
        var installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "AIHub", "aihub.exe");
        exePath ??= File.Exists(installed) ? installed : Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            throw new FileNotFoundException("找不到 aihub.exe，请从安装目录运行");

        var dir = Path.Combine(workspace, ".qoder");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");

        JsonObject root;
        try
        {
            root = (File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) : null) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            root = new JsonObject();
        }
        var hooks = root["hooks"] as JsonObject ?? new JsonObject();
        root["hooks"] = hooks;

        void Install(string evt, string sub, string status)
        {
            var arr = hooks[evt] as JsonArray ?? new JsonArray();
            for (int i = arr.Count - 1; i >= 0; i--)
            {
                var hs = (arr[i] as JsonObject)?["hooks"] as JsonArray;
                if (hs is null) continue;
                for (int j = hs.Count - 1; j >= 0; j--)
                {
                    var entry = hs[j] as JsonObject;
                    var cmd = entry?["command"]?.GetValue<string>() ?? "";
                    var args = entry?["args"] as JsonArray;
                    var isOurs = cmd.Contains("aihub.exe", StringComparison.OrdinalIgnoreCase) ||
                                 cmd.Contains("capture.py", StringComparison.OrdinalIgnoreCase) ||
                                 (args is not null && args.Any(a => (a?.GetValue<string>() ?? "").StartsWith("hook-", StringComparison.Ordinal)));
                    if (isOurs) hs.RemoveAt(j);
                }
                if (hs.Count == 0) arr.RemoveAt(i);
            }
            arr.Add(new JsonObject
            {
                ["hooks"] = new JsonArray(new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = exePath,
                    ["args"] = new JsonArray(sub),
                    ["timeout"] = 120,
                    ["statusMessage"] = status,
                }),
            });
            hooks[evt] = arr;
        }

        Install("SessionStart", "hook-session-start", "AIHub 同步交接内容");
        Install("UserPromptSubmit", "hook-user-prompt", "AIHub 同步其他客户端进展");
        Install("Stop", "hook-stop", "AIHub 归档本次对话");

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
        return path;
    }
}
