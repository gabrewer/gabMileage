using Spectre.Console;

namespace AgentLoop.Ui;

public static class Theme
{
    public static readonly Style Brand = new(new Color(99, 102, 241));
    public static readonly Style Success = new(new Color(34, 197, 94));
    public static readonly Style Warning = new(new Color(245, 158, 11));
    public static readonly Style Error = new(new Color(239, 68, 68));
    public static readonly Style Muted = new(decoration: Decoration.Dim);
    public static readonly Style Task = new(new Color(226, 232, 240));

    public static readonly string[] SpinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    public static class Icons
    {
        public const string Done = "✓";
        public const string Failed = "✗";
        public const string Pending = "·";
        public const string Logo = "⬡";
    }

    public static string Styled(string text, Style style) =>
        new Markup(Markup.Escape(text), style).ToString() ?? text;

    public static string FixedWidth(string str, int len)
    {
        if (str.Length > len) return str[..(len - 1)] + "…";
        return str.PadRight(len);
    }

    public static string FormatElapsed(string? startedAt)
    {
        if (startedAt is null) return "   —";
        if (!DateTime.TryParse(startedAt, out var start)) return "   —";
        var elapsed = (int)(DateTime.UtcNow - start).TotalSeconds;
        var m = elapsed / 60;
        var s = elapsed % 60;
        return $"{m}m {s:D2}s";
    }

    public static int TermWidth => System.Console.WindowWidth > 0 ? System.Console.WindowWidth : 80;
}
