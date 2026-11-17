using AIHub.Core;

var cmd = args.Length > 0 ? args[0] : "list";
var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var hubRoot = args.Length > 1 && args[1] != "-" ? args[1] : Path.Combine(home, "AIHub");

switch (cmd)
{
    case "import-qoder":
    {
        var transcript = args[2];
        var store = new HubStore(hubRoot);
        var s = QoderImporter.Load(transcript);
        var meta = store.Save(s);
        Print(meta);
        break;
    }
    case "import-dsh":
    {
        var dshRoot = args.Length > 2 ? args[2] : DshImporter.DefaultRoot;
        var limit = Environment.GetEnvironmentVariable("AIHUB_DSH_LIMIT") is { Length: > 0 } ls && int.TryParse(ls, out var l) ? l : 0;
        var store = new HubStore(hubRoot);
        int n = 0, fail = 0;
        foreach (var f in DshImporter.EnumerateSessionFiles(dshRoot).OrderByDescending(File.GetLastWriteTime))
        {
            if (limit > 0 && n >= limit) break;
            try
            {
                var s = DshImporter.Load(f, dshRoot);
                if (s.Messages.Count == 0) continue;
                store.Save(s);
                n++;
            }
            catch (Exception e)
            {
                fail++;
                Console.Error.WriteLine($"FAIL {f}: {e.Message}");
            }
        }
        Console.WriteLine($"imported={n} failed={fail}");
        break;
    }
    case "list":
    {
        var store = new HubStore(hubRoot);
        foreach (var m in store.List())
            Console.WriteLine($"{m.Updated:yyyy-MM-dd HH:mm}  {Clients.DisplayName(m.Client),-9} {m.MessageCount,4} msg  {m.AttachmentCount,2} att  {Text.OneLine(m.Title, 60)}");
        break;
    }
    case "scan":
    {
        // prove all three sources are readable without writing
        var qoder = Path.Combine(home, ".qoder", "projects");
        var cn = Path.Combine(home, ".qoder-cn", "projects");
        foreach (var (label, dir) in new[] { ("qoder", qoder), ("qodercn", cn) })
        {
            var files = Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTime).Take(3).ToList()
                : new List<string>();
            foreach (var f in files)
            {
                try
                {
                    var s = QoderImporter.Load(f);
                    Console.WriteLine($"{label,-9} {s.Messages.Count,4} msg  {s.SourcePath[^60..]}  {Text.OneLine(s.Title, 40)}");
                }
                catch (Exception e) { Console.WriteLine($"{label} ERR {e.Message}"); }
            }
        }
        var dshRoot = DshImporter.DefaultRoot;
        foreach (var f in DshImporter.EnumerateSessionFiles(dshRoot).OrderByDescending(File.GetLastWriteTime).Take(3))
        {
            try
            {
                var s = DshImporter.Load(f, dshRoot);
                Console.WriteLine($"{"dsh",-9} {s.Messages.Count,4} msg  {s.SourcePath[^60..]}  {Text.OneLine(s.Title, 40)}");
            }
            catch (Exception e) { Console.WriteLine($"dsh ERR {e.Message}"); }
        }
        break;
    }
    default:
        Console.WriteLine("usage: scan | list | import-qoder <transcript> | import-dsh");
        break;
}

static void Print(SessionMeta m) =>
    Console.WriteLine($"{m.Client} {m.SessionId} msgs={m.MessageCount} att={m.AttachmentCount} title={m.Title}");
