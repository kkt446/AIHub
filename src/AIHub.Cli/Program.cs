using System.Text;
using AIHub.Core;

try
{
    Console.OutputEncoding = new UTF8Encoding(false);
    Console.InputEncoding = new UTF8Encoding(false);
}
catch (IOException) { }

var cmd = args.Length > 0 ? args[0] : "help";
string? Opt(string name)
{
    var i = Array.IndexOf(args, "--" + name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

string ResolveWorkspace(HookInput? input)
{
    var ws = Opt("workspace");
    if (ws is { Length: > 0 } && Directory.Exists(ws)) return ws;
    if (input?.Cwd is { Length: > 0 } cwd && Directory.Exists(cwd)) return cwd;
    return Opt("workspace") ?? Directory.GetCurrentDirectory();
}

(int toQoder, int toCn) SyncDbs(string workspace)
{
    int a = 0, b = 0;
    try { a = DbSync.Sync(workspace, Clients.QoderCn, Clients.Qoder); } catch (Exception) { }
    try { b = DbSync.Sync(workspace, Clients.Qoder, Clients.QoderCn); } catch (Exception) { }
    return (a, b);
}

switch (cmd)
{
    case "hook-session-start":
    {
        var input = WorkspaceHub.ReadHookInput();
        var workspace = ResolveWorkspace(input);
        if (input.TranscriptPath is { Length: > 0 } tp) WorkspaceHub.ImportTranscript(workspace, tp);
        WorkspaceHub.ImportQoder(workspace);
        WorkspaceHub.ImportDsh(workspace);
        WorkspaceHub.WriteHandoff(workspace);
        SyncDbs(workspace);
        Console.Out.Write(WorkspaceHub.SessionStartJson(workspace));
        return 0;
    }
    case "hook-user-prompt":
    {
        var input = WorkspaceHub.ReadHookInput();
        var workspace = ResolveWorkspace(input);
        if (input.TranscriptPath is { Length: > 0 } tp) WorkspaceHub.ImportTranscript(workspace, tp);
        WorkspaceHub.ImportQoder(workspace);
        WorkspaceHub.ImportDsh(workspace);
        WorkspaceHub.WriteHandoff(workspace);
        SyncDbs(workspace);
        Console.Out.Write(WorkspaceHub.UserPromptJson(workspace));
        return 0;
    }
    case "hook-stop":
    {
        var input = WorkspaceHub.ReadHookInput();
        var workspace = ResolveWorkspace(input);
        if (input.TranscriptPath is { Length: > 0 } tp) WorkspaceHub.ImportTranscript(workspace, tp);
        WorkspaceHub.ImportQoder(workspace);
        WorkspaceHub.ImportDsh(workspace);
        WorkspaceHub.WriteHandoff(workspace);
        SyncDbs(workspace);
        return 0;
    }
    case "import":
    {
        var workspace = ResolveWorkspace(null);
        var q = WorkspaceHub.ImportQoder(workspace);
        var d = WorkspaceHub.ImportDsh(workspace);
        WorkspaceHub.WriteHandoff(workspace);
        Console.WriteLine($"qoder={q} dsh={d} workspace={workspace}");
        return 0;
    }
    case "setup-hooks":
    {
        var workspace = ResolveWorkspace(null);
        var path = WorkspaceHub.EnsureHooks(workspace);
        WorkspaceHub.ImportQoder(workspace);
        WorkspaceHub.WriteHandoff(workspace);
        Console.WriteLine($"hooks={path} workspace={workspace}");
        return 0;
    }
    case "db-sync":
    {
        var workspace = ResolveWorkspace(null);
        int a = 0, b = 0;
        string err = "";
        try { a = DbSync.Sync(workspace, Clients.QoderCn, Clients.Qoder); } catch (Exception e) { err += " qoder:" + e.Message; }
        try { b = DbSync.Sync(workspace, Clients.Qoder, Clients.QoderCn); } catch (Exception e) { err += " qodercn:" + e.Message; }
        Console.WriteLine($"cn->qoder={a} qoder->cn={b} workspace={workspace}{err}");
        return err.Length == 0 ? 0 : 1;
    }
    case "db-sync-undo":
    {
        var workspace = ResolveWorkspace(null);
        var q = DbSync.Undo(workspace, Clients.Qoder);
        var c = DbSync.Undo(workspace, Clients.QoderCn);
        Console.WriteLine($"removed qoder={q} qodercn={c} workspace={workspace}");
        return 0;
    }
    case "handoff":
    {
        Console.Out.Write(WorkspaceHub.BuildHandoff(ResolveWorkspace(null)));
        return 0;
    }
    case "export":
    {
        var workspace = ResolveWorkspace(null);
        var outPath = Opt("out") ?? Path.Combine(workspace, "aihub-export.json");
        WebExport.Write(workspace, outPath);
        Console.WriteLine($"export={Path.GetFullPath(outPath)} workspace={workspace}");
        return 0;
    }
    case "list":
    {
        foreach (var m in new HubStore(WorkspaceHub.HubRoot(ResolveWorkspace(null))).List())
            Console.WriteLine($"{m.Updated:MM-dd HH:mm} {Clients.DisplayName(m.Client),-8} {m.MessageCount,4} msg {m.AttachmentCount,3} att  {Text.OneLine(m.Title, 50)}");
        return 0;
    }
    default:
        Console.WriteLine("aihub hook-session-start|hook-user-prompt|hook-stop|import|setup-hooks|db-sync|db-sync-undo|handoff|list|export [--workspace <dir>] [--out <file>]");
        return 1;
}
