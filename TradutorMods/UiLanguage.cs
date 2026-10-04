using System.Text.Json;

namespace TradutorModsStardew;

internal static class UiLanguage
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TradutorModsStardew", "language.json");

    private static LanguageSetting settings = Load();
    public static bool Portuguese => settings.Language == "pt-BR";
    public static string TranslationLocale => settings.TranslationLocale ?? "pt-BR";

    public static string Text(string portuguese, string english) => Portuguese ? portuguese : english;

    public static void Set(bool portuguese)
    {
        var updated = settings with { Language = portuguese ? "pt-BR" : "en" };
        Save(updated);
        settings = updated;
    }

    public static void SetTranslationLocale(string locale)
    {
        var updated = settings with { TranslationLocale = locale };
        Save(updated);
        settings = updated;
    }

    private static void Save(LanguageSetting value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(value));
    }

    private static LanguageSetting Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<LanguageSetting>(File.ReadAllText(SettingsPath)) ?? new("en");
        }
        catch { }
        return new("en");
    }

    private sealed record LanguageSetting(string Language, string? TranslationLocale = null);
}
