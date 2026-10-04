using System.Text.RegularExpressions;
using System.Globalization;

namespace TradutorModsStardew;

internal static class TranslationLocales
{
    private static readonly Regex CodePattern = new(@"^[a-zA-Z]{2,3}(?:-[a-zA-Z0-9]{2,8})*$", RegexOptions.Compiled);

    public static string? FromFileName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (name.StartsWith("content.", StringComparison.OrdinalIgnoreCase))
            name = name["content.".Length..];
        return Normalize(name);
    }

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim().Replace('_', '-');
        if (!CodePattern.IsMatch(value)) return null;
        try { _ = CultureInfo.GetCultureInfo(value); }
        catch (CultureNotFoundException) { return null; }
        var parts = value.Split('-');
        parts[0] = parts[0].ToLowerInvariant();
        for (var i = 1; i < parts.Length; i++)
            parts[i] = parts[i].Length == 2 ? parts[i].ToUpperInvariant()
                : parts[i].Length == 4 ? char.ToUpperInvariant(parts[i][0]) + parts[i][1..].ToLowerInvariant()
                : parts[i].ToLowerInvariant();
        return string.Join('-', parts);
    }

    public static bool SameTargetLanguage(string left, string right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        if (a is null || b is null) return false;
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return true;
        // Preserve the original application's conservative pt/pt-BR overwrite rule.
        return (a == "pt" && b == "pt-BR") || (a == "pt-BR" && b == "pt");
    }

    public static string SelectionGroup(string locale) => locale == "pt" ? "pt-BR" : locale;
}
