using System.Globalization;
using System.Text.Json;

namespace AIHub.Core;

internal static class JsonExt
{
    public static string? Str(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    public static bool Bool(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    public static long? Long(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)
            ? n
            : null;

    public static DateTimeOffset? Time(this JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var ms))
            return DateTimeOffset.FromUnixTimeMilliseconds(ms);
        if (v.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(v.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t))
            return t;
        return null;
    }
}

public static class Text
{
    public static string Shorten(string s, int max)
    {
        s = s.Replace("\r", "").Trim();
        return s.Length <= max ? s : s[..max] + "…";
    }

    public static string OneLine(string s, int max) => Shorten(s.Replace('\n', ' '), max);

    public static string StripAttachmentRefs(string s)
    {
        foreach (var marker in new[] { "\n附件引用：", "\n附件：", "附件引用：", "附件：" })
        {
            var i = s.IndexOf(marker, StringComparison.Ordinal);
            if (i >= 0) return s[..i].TrimEnd();
        }
        return s;
    }

    public static string TitleFrom(string text)
    {
        text = StripAttachmentRefs(text);
        return Shorten(string.Join(' ', text.Split('\n', StringSplitOptions.RemoveEmptyEntries)), 40);
    }
}
