using System.Text.Json;
using AIHub.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI;

namespace AIHub.App;

public sealed class SessionRow
{
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public string Client { get; init; } = "";
    public string When { get; init; } = "";
    public string Counts { get; init; } = "";
    public SessionMeta Meta { get; init; } = new();
    public override string ToString() => Title;
}

public sealed partial class MainWindow : Window
{
    string _workspace = "";
    SessionMeta? _selected;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = false;
        try { AppWindow.Resize(new Windows.Graphics.SizeInt32(1480, 940)); }
        catch (InvalidOperationException) { }

        _workspace = LoadWorkspace();
        WorkspaceBox.Text = _workspace;
        if (Content is FrameworkElement root)
        {
            root.Loaded += async (_, _) =>
            {
                var xr = root.XamlRoot;
                root.Width = xr.Size.Width;
                xr.Changed += (_, _) => root.Width = xr.Size.Width;
                await ImportAll();
                Refresh();
            };
        }
        MessageScroll.SizeChanged += (_, _) =>
            MessagePanel.Width = Math.Max(200, MessageScroll.ViewportWidth - 12);
    }

    static string DefaultWorkspace =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AIHub-ws");

    static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIHub", "settings.txt");

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

    void SetStatus(string text) => StatusText.Text = text;

    async Task ImportAll()
    {
        try
        {
            SetStatus("正在导入三端会话…");
            var ws = _workspace;
            var (q, d) = await Task.Run(() =>
            {
                int qq = 0;
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var store = new HubStore(WorkspaceHub.HubRoot(ws));
                foreach (var dir in new[]
                         {
                             Path.Combine(home, ".qoder", "projects"),
                             Path.Combine(home, ".qoder-cn", "projects"),
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
                                !Path.GetFullPath(s.Cwd).TrimEnd('\\').Equals(Path.GetFullPath(ws).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                                continue;
                            store.Save(s);
                            qq++;
                        }
                        catch (IOException) { }
                    }
                }
                var dd = WorkspaceHub.ImportDsh(ws);
                WorkspaceHub.WriteHandoff(ws);
                return (qq, dd);
            });
            SetStatus($"导入完成：Qoder {q} 个会话，dsh {d} 个会话");
        }
        catch (Exception e)
        {
            SetStatus("导入失败：" + e.Message);
        }
    }

    void Refresh()
    {
        var store = new HubStore(WorkspaceHub.HubRoot(_workspace));
        var rows = store.List().Select(m => new SessionRow
        {
            Key = m.Key,
            Title = string.IsNullOrWhiteSpace(m.Title) ? m.SessionId : m.Title,
            Client = Clients.DisplayName(m.Client),
            When = m.Updated.ToLocalTime().ToString("MM-dd HH:mm"),
            Counts = $"{m.MessageCount} 条 · {m.AttachmentCount} 附件",
            Meta = m,
        }).ToList();
        SessionList.ItemsSource = rows;
        if (rows.Count > 0 && _selected is null)
            SessionList.SelectedIndex = 0;
        SetStatus($"工作区：{_workspace}   共 {rows.Count} 个会话");
    }

    void OnSwitchWorkspace(object sender, RoutedEventArgs e)
    {
        var p = WorkspaceBox.Text.Trim();
        if (!Directory.Exists(p))
        {
            SetStatus("路径不存在：" + p);
            return;
        }
        _workspace = p;
        SaveWorkspace();
        _selected = null;
        MessagePanel.Children.Clear();
        _ = ImportAll().ContinueWith(_ => DispatcherQueue.TryEnqueue(Refresh));
    }

    void OnImport(object sender, RoutedEventArgs e) =>
        _ = ImportAll().ContinueWith(_ => DispatcherQueue.TryEnqueue(Refresh));

    void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start("explorer.exe", _workspace); }
        catch (Exception ex) { SetStatus("打开失败：" + ex.Message); }
    }

    void OnHandoff(object sender, RoutedEventArgs e)
    {
        WorkspaceHub.WriteHandoff(_workspace);
        HandoffBox.Text = File.ReadAllText(WorkspaceHub.HandoffPath(_workspace));
        HandoffExpander.IsExpanded = true;
        SetStatus("交接内容已生成：" + WorkspaceHub.HandoffPath(_workspace));
    }

    void OnCopyHandoff(object sender, RoutedEventArgs e)
    {
        var dp = new DataPackage();
        dp.SetText(HandoffBox.Text);
        Clipboard.SetContent(dp);
        SetStatus("交接内容已复制到剪贴板");
    }

    void OnSessionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SessionList.SelectedItem is not SessionRow row) return;
        _selected = row.Meta;
        DetailTitle.Text = $"{Clients.DisplayName(row.Meta.Client)} · {row.Title}";
        try { RenderSession(row.Meta); }
        catch (Exception ex) { SetStatus("渲染失败：" + ex.Message); }
    }

    void RenderSession(SessionMeta meta)
    {
        MessagePanel.Children.Clear();
        var dir = Path.Combine(WorkspaceHub.HubRoot(_workspace), "sessions", meta.Key);
        var msgFile = Path.Combine(dir, "messages.jsonl");
        if (!File.Exists(msgFile))
        {
            MessagePanel.Children.Add(new TextBlock { Text = "没有消息记录" });
            return;
        }

        var attDir = Path.Combine(dir, "attachments");
        var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(msgFile))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            JsonElement o;
            try { o = JsonDocument.Parse(line).RootElement; }
            catch (JsonException) { continue; }
            if (o.ValueKind != JsonValueKind.Object) continue;

            var role = o.TryGetProperty("role", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() ?? "" : "";
            var text = o.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
            var tool = o.TryGetProperty("tool", out var tn) && tn.ValueKind == JsonValueKind.String ? tn.GetString() : null;

            if (role == Roles.ToolCall || role == Roles.ToolResult)
            {
                MessagePanel.Children.Add(new TextBlock
                {
                    Text = (role == Roles.ToolCall ? $"工具 {tool}" : "工具结果") + "：" + Text.OneLine(text, 200),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(28, 0, 0, 0),
                });
                continue;
            }

            var isUser = role == Roles.User;
            var bubble = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                MaxWidth = 560,
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Background = new SolidColorBrush(isUser
                    ? Color.FromArgb(255, 221, 235, 255)
                    : Color.FromArgb(255, 244, 244, 246)),
            };
            var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                Foreground = new SolidColorBrush(Colors.Black),
            });

            if (o.TryGetProperty("attachments", out var atts) && atts.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in atts.EnumerateArray())
                {
                    if (a.ValueKind != JsonValueKind.String) continue;
                    var name = a.GetString();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var path = Path.Combine(attDir, name);
                    if (!File.Exists(path)) continue;
                    shown.Add(path);
                    var ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp")
                    {
                        stack.Children.Add(new Image
                        {
                            Source = new BitmapImage(new Uri(path)),
                            MaxHeight = 180,
                            MaxWidth = 520,
                            Stretch = Stretch.Uniform,
                            HorizontalAlignment = HorizontalAlignment.Left,
                        });
                    }
                    else
                    {
                        stack.Children.Add(new TextBlock { Text = "附件：" + name, FontSize = 12, Foreground = new SolidColorBrush(Colors.DimGray) });
                    }
                }
            }

            bubble.Child = stack;
            MessagePanel.Children.Add(bubble);
        }

        if (Directory.Exists(attDir))
        {
            foreach (var f in Directory.EnumerateFiles(attDir))
            {
                if (shown.Contains(f)) continue;
                MessagePanel.Children.Add(new TextBlock { Text = "附件：" + Path.GetFileName(f), FontSize = 12, Foreground = new SolidColorBrush(Colors.DimGray) });
            }
        }
    }
}
