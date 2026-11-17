using AIHub.Core;

var cmd = args.Length > 0 ? args[0] : "help";
string? Opt(string name)
{
    var i = Array.IndexOf(args, "--" + name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
var workspace = Opt("workspace") ?? Directory.GetCurrentDirectory();

switch (cmd)
{
    case "hook-session-start":
    {
        var input = WorkspaceHub.ReadHookInput();
        if (input.TranscriptPath is { Length: > 0 } tp) WorkspaceHub.ImportTranscript(workspace, tp);
        WorkspaceHub.ImportDsh(workspace);
        WorkspaceHub.WriteHandoff(workspace);
        Console.Out.Write(WorkspaceHub.SessionStartJson(workspace));
        return 0;
    }
    case "hook-stop":
    {
        var input = WorkspaceHub.ReadHookInput();
        if (input.TranscriptPath is { Length: > 0 } tp) WorkspaceHub.ImportTranscript(workspace, tp);
        WorkspaceHub.ImportDsh(workspace);
        WorkspaceHub.WriteHandoff(workspace);
        return 0;
    }
    case "import":
    {
        int q = 0;
        foreach (var dir in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qoder", "projects"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qoder-cn", "projects"),
                 })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var f in Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.AllDirectories))
            {
                if (f.Contains(@"\subagents\", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var s = QoderImporter.Load(f);
                    if (s.Messages.Count == 0) continue;
                    if (!string.IsNullOrWhiteSpace(s.Cwd) &&
                        !Path.GetFullPath(s.Cwd).TrimEnd('\\').Equals(Path.GetFullPath(workspace).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                        continue;
                    new HubStore(WorkspaceHub.HubRoot(workspace)).Save(s);
                    q++;
                }
                catch (IOException) { }
            }
        }
        var d = WorkspaceHub.ImportDsh(workspace);
        WorkspaceHub.WriteHandoff(workspace);
        Console.WriteLine($"qoder={q} dsh={d} workspace={workspace}");
        return 0;
    }
    case "handoff":
    {
        Console.Out.Write(WorkspaceHub.BuildHandoff(workspace));
        return 0;
    }
    case "list":
    {
        foreach (var m in new HubStore(WorkspaceHub.HubRoot(workspace)).List())
            Console.WriteLine($"{m.Updated:MM-dd HH:mm} {Clients.DisplayName(m.Client),-8} {m.MessageCount,4} msg {m.AttachmentCount,3} att  {Text.OneLine(m.Title, 50)}");
        return 0;
    }
    default:
        Console.WriteLine("aihub hook-session-start|hook-stop|import|handoff|list --workspace <dir>");
        return 1;
}
