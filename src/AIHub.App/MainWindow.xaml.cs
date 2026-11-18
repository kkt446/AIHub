using System.Text.Json;
using System.Text.Json.Nodes;
using AIHub.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.Storage.Pickers;

namespace AIHub.App;

public sealed partial class MainWindow : Window
{
    readonly bool _smoke;
    string _workspace;

    public MainWindow(bool smoke = false)
    {
        InitializeComponent();
        _smoke = smoke;
        try { AppWindow.Resize(new Windows.Graphics.SizeInt32(1480, 940)); }
        catch (InvalidOperationException) { }
        if (smoke)
        {
            try { AppWindow.Move(new Windows.Graphics.PointInt32(-20000, -20000)); }
            catch (InvalidOperationException) { }
        }
        _workspace = LoadWorkspace();
        _ = InitAsync();
    }

    static string DefaultWorkspace =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AIHub-ws");

    static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIHub", "settings.txt");

    static string WebRoot => Path.Combine(AppContext.BaseDirectory, "webroot");

    static string WebViewDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIHub", "WebView2");

    static void Log(string s)
    {
        try
        {
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "aihub-app.log"),
                DateTime.Now.ToString("HH:mm:ss") + " " + s + Environment.NewLine);
        }
        catch (IOException) { }
    }

    static string LoadWorkspace()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var p = File.ReadAllText(SettingsPath).Trim();
                if (Directory.Exists(p)) return p;
            }
        }
        catch (IOException) { }
        var def = DefaultWorkspace;
        Directory.CreateDirectory(def);
        return def;
    }

    void SaveWorkspace()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, _workspace);
        }
        catch (IOException) { }
    }

    static string? FindCli()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "aihub.exe"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "cli", "aihub.exe")),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    async Task InitAsync()
    {
        try
        {
            Directory.CreateDirectory(WebViewDataDir);
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", WebViewDataDir);
            await HubView.EnsureCoreWebView2Async();
            var core = HubView.CoreWebView2;

            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.SetVirtualHostNameToFolderMapping("aihub.local", WebRoot, CoreWebView2HostResourceAccessKind.Allow);
            MapAssets(core);

            core.WebMessageReceived += OnWebMessage;
            core.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                try
                {
                    if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri) { UseShellExecute = true });
                }
                catch (Exception ex) { Log("open-url: " + ex.Message); }
            };
            core.NavigationCompleted += (sender, e) =>
            {
                if (!e.IsSuccess)
                {
                    ShowFallback("界面加载失败：" + e.WebErrorStatus);
                    FinishSmoke(false, "navigation failed: " + e.WebErrorStatus);
                    return;
                }
                if (_smoke) _ = RunSmokeAsync();
            };

            HubView.Source = new Uri("https://aihub.local/index.html");
        }
        catch (Exception e)
        {
            Log("webview-init: " + e);
            ShowFallback("无法启动界面组件（WebView2）。\n请安装 Microsoft Edge WebView2 运行时后重试。\n\n" + e.Message);
            FinishSmoke(false, "webview init: " + e.Message);
        }
    }

    void MapAssets(CoreWebView2 core)
    {
        try
        {
            core.SetVirtualHostNameToFolderMapping("aihub-assets", WorkspaceHub.HubRoot(_workspace), CoreWebView2HostResourceAccessKind.Allow);
        }
        catch (Exception e) { Log("map-assets: " + e.Message); }
    }

    void ShowFallback(string message)
    {
        FallbackText.Text = message;
        FallbackText.Visibility = Visibility.Visible;
        HubView.Visibility = Visibility.Collapsed;
    }

    async Task PushDataAsync()
    {
        if (HubView.CoreWebView2 is null) return;
        try
        {
            var node = JsonNode.Parse(WebExport.Build(_workspace))!;
            node["assetBase"] = "https://aihub-assets";
            await HubView.CoreWebView2.ExecuteScriptAsync($"window.aihubReceive({node.ToJsonString()})");
        }
        catch (Exception e)
        {
            Log("push-data: " + e.Message);
            await StatusAsync("刷新界面数据失败：" + e.Message);
        }
    }

    async Task StatusAsync(string message)
    {
        Log(message);
        try
        {
            var json = JsonSerializer.Serialize(message);
            await HubView.CoreWebView2.ExecuteScriptAsync(
                $"window.chrome.webview.postMessage({{type:'status',message:{json}}})");
        }
        catch (Exception e) { Log("status: " + e.Message); }
    }

    void OnWebMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        string type;
        var payload = "";
        try
        {
            using var doc = JsonDocument.Parse(args.WebMessageAsJson);
            var root = doc.RootElement;
            type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            if (root.TryGetProperty("payload", out var p) && p.ValueKind == JsonValueKind.String)
                payload = p.GetString() ?? "";
        }
        catch (JsonException) { return; }

        switch (type)
        {
            case "ready": _ = PushDataAsync(); break;
            case "refresh": _ = RefreshAsync(); break;
            case "sync": _ = SyncAsync(); break;
            case "syncUndo": _ = UndoSyncAsync(); break;
            case "hooks": _ = HooksAsync(); break;
            case "handoff": _ = HandoffAsync(); break;
            case "copy": CopyToClipboard(payload); break;
            case "openFolder": OpenFolder(); break;
            case "chooseWorkspace": _ = ChooseWorkspaceAsync(); break;
        }
    }

    void CopyToClipboard(string text)
    {
        try
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        }
        catch (Exception e)
        {
            _ = StatusAsync("复制失败：" + e.Message);
        }
    }

    async Task RefreshAsync()
    {
        var ws = _workspace;
        try
        {
            var (q, d, hooks, cnToQoder, qoderToCn) = await Task.Run(() =>
            {
                var qq = WorkspaceHub.ImportQoder(ws);
                var dd = WorkspaceHub.ImportDsh(ws);
                var hookPath = "";
                var cli = FindCli();
                if (cli is not null)
                {
                    try { hookPath = WorkspaceHub.EnsureHooks(ws, cli); }
                    catch (IOException) { }
                }
                WorkspaceHub.WriteHandoff(ws);
                int a = 0, b = 0;
                try { a = DbSync.Sync(ws, Clients.QoderCn, Clients.Qoder); } catch (Exception) { }
                try { b = DbSync.Sync(ws, Clients.Qoder, Clients.QoderCn); } catch (Exception) { }
                return (qq, dd, hookPath, a, b);
            });
            await PushDataAsync();
            var hooksNote = hooks.Length > 0 ? "；自动交接已启用" : "";
            await StatusAsync($"已刷新：Qoder {q} 个会话，dsh {d} 个会话；数据库同步 CN→Qoder {cnToQoder}、Qoder→CN {qoderToCn}{hooksNote}");
        }
        catch (Exception e)
        {
            await StatusAsync("刷新失败：" + e.Message);
        }
    }

    async Task SyncAsync()
    {
        var ws = _workspace;
        var (a, b, err) = await Task.Run(() =>
        {
            int x = 0, y = 0;
            var e = "";
            try { x = DbSync.Sync(ws, Clients.QoderCn, Clients.Qoder); } catch (Exception ex) { e += " Qoder:" + ex.Message; }
            try { y = DbSync.Sync(ws, Clients.Qoder, Clients.QoderCn); } catch (Exception ex) { e += " CN:" + ex.Message; }
            return (x, y, e);
        });
        await PushDataAsync();
        await StatusAsync(err.Length > 0
            ? "同步完成但有错误：" + err.Trim()
            : $"数据库同步完成：CN→Qoder {a}、Qoder→CN {b}");
    }

    async Task UndoSyncAsync()
    {
        var ws = _workspace;
        var (a, b) = await Task.Run(() =>
            (DbSync.Undo(ws, Clients.Qoder), DbSync.Undo(ws, Clients.QoderCn)));
        await PushDataAsync();
        await StatusAsync($"已撤销镜像：Qoder {a} 个、Qoder CN {b} 个会话");
    }

    async Task HooksAsync()
    {
        try
        {
            var ws = _workspace;
            var path = await Task.Run(() =>
            {
                var cli = FindCli() ?? throw new FileNotFoundException("找不到 aihub.exe");
                return WorkspaceHub.EnsureHooks(ws, cli);
            });
            await StatusAsync("自动交接已启用：" + path);
        }
        catch (Exception e)
        {
            await StatusAsync("启用失败：" + e.Message);
        }
    }

    async Task HandoffAsync()
    {
        try
        {
            var ws = _workspace;
            await Task.Run(() => WorkspaceHub.WriteHandoff(ws));
            await StatusAsync("交接内容已写入：" + WorkspaceHub.HandoffPath(ws));
            await PushDataAsync();
        }
        catch (Exception e)
        {
            await StatusAsync("写入失败：" + e.Message);
        }
    }

    void OpenFolder()
    {
        try { System.Diagnostics.Process.Start("explorer.exe", _workspace); }
        catch (Exception e) { _ = StatusAsync("打开失败：" + e.Message); }
    }

    async Task ChooseWorkspaceAsync()
    {
        try
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null) return;
            _workspace = folder.Path;
            SaveWorkspace();
            if (HubView.CoreWebView2 is not null) MapAssets(HubView.CoreWebView2);
            await StatusAsync("已切换工作区：" + _workspace);
            await RefreshAsync();
        }
        catch (Exception e)
        {
            await StatusAsync("切换工作区失败：" + e.Message);
        }
    }

    async Task RunSmokeAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(25);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var state = await HubView.CoreWebView2.ExecuteScriptAsync(
                    "window.__AIHUB_STATE__ ? JSON.stringify(window.__AIHUB_STATE__) : ''");
                if (!string.IsNullOrEmpty(state) && state != "\"\"")
                {
                    var asset = await CheckAssetAsync();
                    FinishSmoke(true, state + " asset=" + asset);
                    return;
                }
            }
            catch (Exception e)
            {
                FinishSmoke(false, "eval: " + e.Message);
                return;
            }
            await Task.Delay(400);
        }
        FinishSmoke(false, "timeout waiting for injected data");
    }

    async Task<string> CheckAssetAsync()
    {
        const string start = """
            (() => {
              window.__SMOKE_IMG = 'none';
              const d = window.__AIHUB_DATA__;
              if (!d || !d.assetBase) return;
              for (const s of d.sessions) {
                for (const m of s.messages) {
                  for (const a of (m.attachments || [])) {
                    if (a.kind !== 'image') continue;
                    const img = new Image();
                    window.__SMOKE_IMG = 'pending';
                    img.onload = () => { window.__SMOKE_IMG = 'ok ' + img.naturalWidth; };
                    img.onerror = () => { window.__SMOKE_IMG = 'error'; };
                    img.src = d.assetBase + '/sessions/' + encodeURIComponent(s.key) + '/attachments/' + encodeURIComponent(a.name);
                    return;
                  }
                }
              }
            })()
            """;
        try
        {
            await HubView.CoreWebView2.ExecuteScriptAsync(start);
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                var value = await HubView.CoreWebView2.ExecuteScriptAsync("String(window.__SMOKE_IMG || '')");
                var text = value.Trim('"');
                if (text != "none" && text != "pending" && text.Length > 0) return text;
                await Task.Delay(250);
            }
            return "timeout";
        }
        catch (Exception e)
        {
            return "eval-error " + e.Message;
        }
    }

    void FinishSmoke(bool ok, string message)
    {
        if (!_smoke) return;
        try
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "aihub-app-smoke.log"),
                (ok ? "OK " : "FAIL ") + message + Environment.NewLine);
        }
        catch (IOException) { }
        try { AppWindow.Hide(); } catch (InvalidOperationException) { }
        DispatcherQueue.TryEnqueue(() => Environment.Exit(ok ? 0 : 1));
    }
}
