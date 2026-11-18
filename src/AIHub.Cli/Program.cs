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

static IReadOnlyList<string> MirrorBoth(string workspace)
{
    var a = WorkspaceHub.MirrorSessions(workspace, Clients.QoderCn, Clients.Qoder);
    var b = WorkspaceHub.MirrorSessions(workspace, Clients.Qoder, Clients.QoderCn);
    return a.Concat(b).ToList();
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
        MirrorBoth(workspace);
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
        MirrorBoth(workspace);
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
        MirrorBoth(workspace);
        return 0;
    }
    case "import":
    {
        var workspace = ResolveWorkspace(null);
        var q = WorkspaceHub.ImportQoder(workspace);
        var d = WorkspaceHub.ImportDsh(workspace);
        WorkspaceHub.WriteHandoff(workspace);
        var m = MirrorBoth(workspace);
        Console.WriteLine($"qoder={q} dsh={d} mirrored={m.Count} workspace={workspace}");
        return 0;
    }
    case "setup-hooks":
    {
        var workspace = ResolveWorkspace(null);
        var path = WorkspaceHub.EnsureHooks(workspace);
        WorkspaceHub.ImportQoder(workspace);
        WorkspaceHub.WriteHandoff(workspace);
        MirrorBoth(workspace);
        Console.WriteLine($"hooks={path} workspace={workspace}");
        return 0;
    }
    case "mirror":
    {
        var workspace = ResolveWorkspace(null);
        var added = MirrorBoth(workspace);
        foreach (var id in added) Console.WriteLine($"mirrored {id}");
        Console.WriteLine($"added={added.Count} workspace={workspace}");
        return 0;
    }
    case "mirror-undo":
    {
        var workspace = ResolveWorkspace(null);
        var q = WorkspaceHub.UndoMirror(workspace, Clients.Qoder);
        var c = WorkspaceHub.UndoMirror(workspace, Clients.QoderCn);
        Console.WriteLine($"removed qoder={q} qodercn={c} workspace={workspace}");
        return 0;
    }
    case "handoff":
    {
        Console.Out.Write(WorkspaceHub.BuildHandoff(ResolveWorkspace(null)));
        return 0;
    }
    case "list":
    {
        foreach (var m in new HubStore(WorkspaceHub.HubRoot(ResolveWorkspace(null))).List())
            Console.WriteLine($"{m.Updated:MM-dd HH:mm} {Clients.DisplayName(m.Client),-8} {m.MessageCount,4} msg {m.AttachmentCount,3} att  {Text.OneLine(m.Title, 50)}");
        return 0;
    }
    default:
        Console.WriteLine("aihub hook-session-start|hook-user-prompt|hook-stop|import|setup-hooks|handoff|list|mirror|mirror-undo [--workspace <dir>]");
        return 1;
}
