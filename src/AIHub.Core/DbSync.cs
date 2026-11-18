using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace AIHub.Core;

/// <summary>
/// 在两个 Qoder 客户端的本地任务库（main.sqlite）之间同步会话，使一端的对话出现在另一端的任务列表里。
/// 只新增/补齐消息，不删除对方数据；写入记录保存在目标库旁的 .aihub-dbsync.json，可用 Undo 撤销。
/// </summary>
public static class DbSync
{
    static string? AppDataDir(string client) => client switch
    {
        Clients.Qoder => Path.Combine(Roaming, "com.qoder.app.stable"),
        Clients.QoderCn => Path.Combine(Roaming, "com.qodercn.app.stable"),
        _ => null,
    };

    static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    static string? DbPath(string client)
    {
        var env = client switch
        {
            Clients.Qoder => Environment.GetEnvironmentVariable("AIHUB_QODER_HOME"),
            Clients.QoderCn => Environment.GetEnvironmentVariable("AIHUB_QODER_CN_HOME"),
            _ => null,
        };
        if (!string.IsNullOrWhiteSpace(env))
            return Path.Combine(env, "main.sqlite");
        var dir = AppDataDir(client);
        return dir is null ? null : Path.Combine(dir, "main.sqlite");
    }

    public static bool Available(string client) => DbPath(client) is { } p && File.Exists(p);

    public static int Sync(string workspace, string fromClient, string toClient)
    {
        var srcPath = DbPath(fromClient);
        var dstPath = DbPath(toClient);
        if (srcPath is null || dstPath is null || !File.Exists(srcPath) || !File.Exists(dstPath))
            return 0;

        var cwd = Path.GetFullPath(workspace).TrimEnd('\\');
        var inserted = new List<string>();

        using var dst = Open(dstPath, SqliteOpenMode.ReadWrite, create: false);
        var dstWorkspace = EnsureWorkspace(dst, cwd);
        var targetModel = TargetModel(dst, dstWorkspace) ?? MapModel(fromClient);

        var ids = SourceSessionIds(srcPath, cwd);
        if (ids.Count == 0) return 0;

        var manifest = LoadManifest(ManifestPath(dstPath));
        bool backedUp = false;

        foreach (var id in ids)
        {
            var exists = Scalar<long>(dst, "select count(*) from chat_sessions where session_id=@id", ("@id", id)) > 0;
            if (exists)
            {
                AppendMessages(dst, srcPath, id);
                continue;
            }

            if (!backedUp)
            {
                SqliteConnection.ClearAllPools();
                Backup(dstPath);
                backedUp = true;
            }
            CopySession(dst, srcPath, id, dstWorkspace, targetModel);
            manifest.Add(new JsonObject { ["client"] = fromClient, ["id"] = id, ["at"] = DateTimeOffset.Now.ToString("o") });
            inserted.Add(id);
        }

        if (inserted.Count > 0) SaveManifest(ManifestPath(dstPath), manifest);
        return inserted.Count;
    }

    public static int Undo(string workspace, string toClient)
    {
        var dstPath = DbPath(toClient);
        if (dstPath is null || !File.Exists(dstPath)) return 0;
        var mp = ManifestPath(dstPath);
        if (!File.Exists(mp)) return 0;
        var manifest = LoadManifest(mp);
        using var dst = Open(dstPath, SqliteOpenMode.ReadWrite, create: false);
        int n = 0;
        foreach (var node in manifest)
        {
            var id = (node as JsonObject)?["id"]?.GetValue<string>() ?? "";
            if (!IsSafeId(id)) continue;
            Exec(dst, "delete from chat_session_messages where session_id=@id", ("@id", id));
            Exec(dst, "delete from chat_sessions where session_id=@id", ("@id", id));
            n++;
        }
        SqliteConnection.ClearAllPools();
        File.Delete(mp);
        return n;
    }

    static List<string> SourceSessionIds(string srcPath, string cwd)
    {
        using var src = Open(srcPath, SqliteOpenMode.ReadOnly, create: false);
        using var cmd = src.CreateCommand();
        cmd.CommandText = """
            select session_id from chat_sessions
            where lower(rtrim(cwd, '\')) = lower(@cwd)
              and coalesce(deleted_at, 0) = 0
              and coalesce(session_kind, 'standard') = 'standard'
            """;
        cmd.Parameters.AddWithValue("@cwd", cwd);
        var list = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    static void CopySession(SqliteConnection dst, string srcPath, string id, string workspaceId, string? model)
    {
        EnsureSameSchema(dst, srcPath);
        Exec(dst, "attach database @p as src", ("@p", srcPath));
        using var tx = dst.BeginTransaction();
        Exec(dst, "insert into chat_sessions select * from src.chat_sessions where session_id=@id", ("@id", id));
        Exec(dst, "update chat_sessions set workspace_id=@ws, model=coalesce(@m, model) where session_id=@id",
            ("@ws", workspaceId), ("@m", model), ("@id", id));
        Exec(dst, "insert or ignore into chat_session_messages select * from src.chat_session_messages where session_id=@id", ("@id", id));
        tx.Commit();
        Exec(dst, "detach database src");
    }

    static void AppendMessages(SqliteConnection dst, string srcPath, string id)
    {
        Exec(dst, "attach database @p as src", ("@p", srcPath));
        using var tx = dst.BeginTransaction();
        Exec(dst, """
            insert or ignore into chat_session_messages
            select s.* from src.chat_session_messages s
            where s.session_id=@id
              and not exists (select 1 from chat_session_messages t where t.message_id=s.message_id)
            """, ("@id", id));
        Exec(dst, "update chat_sessions set updated_at=(select max(updated_at) from src.chat_sessions where session_id=@id) where session_id=@id and updated_at < (select updated_at from src.chat_sessions where session_id=@id)", ("@id", id));
        tx.Commit();
        Exec(dst, "detach database src");
    }

    static void EnsureSameSchema(SqliteConnection dst, string srcPath)
    {
        var dstCols = Columns(dst, "main");
        using var src = Open(srcPath, SqliteOpenMode.ReadOnly, create: false);
        var srcCols = Columns(src, "main");
        if (!dstCols.SequenceEqual(srcCols))
            throw new InvalidOperationException("两个客户端的数据表结构不一致，已跳过同步");
    }

    static List<string> Columns(SqliteConnection con, string schema)
    {
        var list = new List<string>();
        using var cmd = con.CreateCommand();
        cmd.CommandText = $"pragma {schema}.table_info(chat_sessions)";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(1));
        return list;
    }

    static string EnsureWorkspace(SqliteConnection con, string cwd)
    {
        foreach (var (id, roots) in Workspaces(con))
            if (roots.Any(p => string.Equals(p.TrimEnd('\\'), cwd, StringComparison.OrdinalIgnoreCase)))
                return id;

        var wsId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Exec(con, "insert into workspaces (workspace_id,name,workspace_source,root_paths_json,product_mode,extra_json,created_at,updated_at,archived) values (@id,@name,'local',@roots,'coding','{}',@t,@t,0)",
            ("@id", wsId), ("@name", Path.GetFileName(cwd)), ("@roots", new JsonArray(cwd).ToJsonString()), ("@t", now));
        return wsId;
    }

    static IEnumerable<(string Id, List<string> Roots)> Workspaces(SqliteConnection con)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = "select workspace_id, root_paths_json from workspaces where coalesce(deleted_at,0)=0";
        using var r = cmd.ExecuteReader();
        var rows = new List<(string, List<string>)>();
        while (r.Read())
        {
            var roots = new List<string>();
            try
            {
                if (JsonNode.Parse(r.IsDBNull(1) ? "[]" : r.GetString(1)) is JsonArray arr)
                    foreach (var n in arr)
                        if (n?.GetValue<string>() is { Length: > 0 } s) roots.Add(s);
            }
            catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException) { }
            rows.Add((r.GetString(0), roots));
        }
        return rows;
    }

    static string? TargetModel(SqliteConnection con, string workspaceId)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = "select model from chat_sessions where workspace_id=@ws and model is not null and coalesce(deleted_at,0)=0 limit 1";
        cmd.Parameters.AddWithValue("@ws", workspaceId);
        return cmd.ExecuteScalar() as string;
    }

    static string MapModel(string fromClient) => fromClient == Clients.QoderCn ? "dfmodel" : "qfmodel";

    static SqliteConnection Open(string path, SqliteOpenMode mode, bool create)
    {
        var con = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Pooling = false,
            DefaultTimeout = 8,
        }.ToString());
        con.Open();
        return con;
    }

    static void Exec(SqliteConnection con, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    static T Scalar<T>(SqliteConnection con, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        var v = cmd.ExecuteScalar();
        return v is null or DBNull ? default! : (T)Convert.ChangeType(v, typeof(T));
    }

    static void Backup(string dbPath)
    {
        var dir = Path.Combine(Path.GetDirectoryName(dbPath)!, ".aihub-backup");
        Directory.CreateDirectory(dir);
        var dst = Path.Combine(dir, $"main-{DateTime.Now:yyyyMMdd-HHmmss}.sqlite");
        using (var src = Open(dbPath, SqliteOpenMode.ReadOnly, create: false))
        using (var bak = Open(dst, SqliteOpenMode.ReadWriteCreate, create: true))
            src.BackupDatabase(bak);

        foreach (var old in Directory.EnumerateFiles(dir, "main-*.sqlite").OrderByDescending(f => f).Skip(5))
            try { File.Delete(old); } catch (IOException) { }
    }

    static string ManifestPath(string dbPath) => Path.Combine(Path.GetDirectoryName(dbPath)!, ".aihub-dbsync.json");

    static JsonArray LoadManifest(string path)
    {
        try { return JsonNode.Parse(File.ReadAllText(path)) as JsonArray ?? new JsonArray(); }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException) { return new JsonArray(); }
    }

    static void SaveManifest(string path, JsonArray manifest)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, manifest.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    static bool IsSafeId(string id) =>
        id.Length is > 0 and < 80 && !id.Contains("..") &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
