using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TradutorModsStardew;

internal sealed class TranslationService
{
    private static readonly JsonDocumentOptions ManifestJsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };
    private static readonly JsonSerializerOptions RelaxedJsonStringOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public List<InstalledMod> ScanMods(string modsRoot)
    {
        var mods = new List<InstalledMod>();
        foreach (var manifest in Directory.EnumerateFiles(modsRoot, "manifest.json", SearchOption.AllDirectories))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(manifest), ManifestJsonOptions);
                var root = doc.RootElement;
                var directory = Path.GetDirectoryName(manifest)!;
                var name = GetString(root, "Name") ?? Path.GetFileName(directory);
                var uniqueId = GetString(root, "UniqueID") ?? "";
                var relativeFolder = Path.GetRelativePath(modsRoot, directory);
                var packageFolder = relativeFolder.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                mods.Add(new InstalledMod(directory, Path.GetFileName(directory), name, uniqueId, packageFolder, relativeFolder));
            }
            catch
            {
                // A broken manifest is SMAPI's concern; the report simply omits it.
            }
        }
        return mods.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public List<TranslationUnit> ScanLibrary(string libraryRoot)
    {
        var units = new List<TranslationUnit>();
        foreach (var file in Directory.EnumerateFiles(libraryRoot, "*.json", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(file);
            var i18n = Path.GetDirectoryName(file)!;
            if (!Path.GetFileName(i18n).Equals("i18n", StringComparison.OrdinalIgnoreCase))
                continue;
            var locale = TranslationLocales.FromFileName(fileName);
            if (locale is null) continue;
            var component = Directory.GetParent(i18n)!;
            var package = FirstPart(libraryRoot, component.FullName);
            units.Add(new TranslationUnit(package, component.Name, file,
                Path.Combine("i18n", fileName), false, Path.GetRelativePath(libraryRoot, component.FullName), Locale: TranslationLocales.SelectionGroup(locale)));
        }

        foreach (var dir in Directory.EnumerateDirectories(libraryRoot, "*", SearchOption.AllDirectories))
        {
            var parent = Directory.GetParent(dir);
            if (parent?.Name.Equals("i18n", StringComparison.OrdinalIgnoreCase) == true)
            {
                var localeFolder = TranslationLocales.Normalize(Path.GetFileName(dir));
                if (localeFolder is null || !ContainsJson(dir)) continue;
                var component = parent.Parent!;
                var packageFolder = FirstPart(libraryRoot, component.FullName);
                units.Add(new TranslationUnit(packageFolder, component.Name, dir,
                    Path.Combine("i18n", Path.GetFileName(dir)), true,
                    Path.GetRelativePath(libraryRoot, component.FullName), Locale: TranslationLocales.SelectionGroup(localeFolder)));
                continue;
            }
            if (parent is null || !parent.Name.Equals("Dialogues", StringComparison.OrdinalIgnoreCase) ||
                parent.Parent is null || !parent.Parent.Name.Equals("assets", StringComparison.OrdinalIgnoreCase))
                continue;
            var locale = TranslationLocales.Normalize(Path.GetFileName(dir));
            if (locale is null) continue;
            var package = FirstPart(libraryRoot, dir);
            units.Add(new TranslationUnit(package, package, dir,
                Path.Combine("assets", "Dialogues", Path.GetFileName(dir)), true, Path.GetRelativePath(libraryRoot, dir), Locale: TranslationLocales.SelectionGroup(locale)));
        }

        foreach (var file in Directory.EnumerateFiles(libraryRoot, "content.*.json", SearchOption.AllDirectories))
        {
            var locale = TranslationLocales.FromFileName(file);
            if (locale is null) continue;
            var component = Directory.GetParent(file)!;
            var package = FirstPart(libraryRoot, component.FullName);
            units.Add(new TranslationUnit(package, component.Name, file,
                "content.json", false, Path.GetRelativePath(libraryRoot, component.FullName), true, TranslationLocales.SelectionGroup(locale)));
        }
        return units
            .GroupBy(unit => $"{unit.RelativeComponentPath}|{unit.IsDirectory}|{unit.IsContentTextMap}|{unit.Locale}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(unit =>
                Path.GetFileName(unit.SourcePath).Equals("pt-BR.json", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(unit.SourcePath).Equals("content.pt-BR.json", StringComparison.OrdinalIgnoreCase)).First())
            .ToList();
    }

    /// <summary>
    /// Sets up safe source references for mods that were newly added to the Mods folder.
    /// References are deliberately named default.json/content.json, which ScanLibrary ignores;
    /// only a translation file with a language code can later be applied to a game mod.
    /// </summary>
    public LibraryPreparationReport PrepareLibrary(string libraryRoot, IEnumerable<InstalledMod> mods)
    {
        var createdFolders = 0;
        var copiedI18n = 0;
        var copiedContent = 0;
        var alreadyPrepared = 0;

        foreach (var mod in mods)
        {
        var sourceI18n = Path.Combine(mod.Directory, "i18n", "default.json");
        var sourceDefaultFolder = Path.Combine(mod.Directory, "i18n", "default");
            var sourceContent = Path.Combine(mod.Directory, "content.json");
            if (!File.Exists(sourceI18n) && !Directory.Exists(sourceDefaultFolder) && !File.Exists(sourceContent))
                continue;

            var libraryModFolder = Path.Combine(libraryRoot, mod.RelativeFolder);
            if (!Directory.Exists(libraryModFolder))
            {
                Directory.CreateDirectory(libraryModFolder);
                createdFolders++;
            }

            var copiedAnything = false;
            if (File.Exists(sourceI18n))
            {
                var targetI18n = Path.Combine(libraryModFolder, "i18n", "default.json");
                if (!File.Exists(targetI18n))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetI18n)!);
                    File.Copy(sourceI18n, targetI18n);
                    copiedI18n++;
                    copiedAnything = true;
                }
            }
            if (Directory.Exists(sourceDefaultFolder))
            {
                var targetDefaultFolder = Path.Combine(libraryModFolder, "i18n", "default");
                if (!Directory.Exists(targetDefaultFolder))
                {
                    CopyPath(sourceDefaultFolder, targetDefaultFolder, false);
                    copiedI18n++;
                    copiedAnything = true;
                }
            }

            if (File.Exists(sourceContent))
            {
                var targetContent = Path.Combine(libraryModFolder, "content.json");
                if (!File.Exists(targetContent))
                {
                    File.Copy(sourceContent, targetContent);
                    copiedContent++;
                    copiedAnything = true;
                }
            }

            if (!copiedAnything)
                alreadyPrepared++;
        }

        return new LibraryPreparationReport(createdFolders, copiedI18n, copiedContent, alreadyPrepared);
    }

    public (int Imported, int Invalid) ImportInstalledTranslations(string libraryRoot, IEnumerable<InstalledMod> mods, string selectedLocale)
    {
        var imported = 0;
        var invalid = 0;
        foreach (var mod in mods)
        {
            var i18n = Path.Combine(mod.Directory, "i18n");
            if (!Directory.Exists(i18n)) continue;
            var sourceFile = Directory.EnumerateFiles(i18n, "*.json")
                .Where(path => TranslationLocales.SameTargetLanguage(TranslationLocales.FromFileName(path) ?? "", selectedLocale))
                .OrderByDescending(path => Path.GetFileName(path).Equals("pt-BR.json", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
            var sourceFolder = Directory.EnumerateDirectories(i18n)
                .Where(path => TranslationLocales.SameTargetLanguage(Path.GetFileName(path), selectedLocale) && ContainsJson(path))
                .OrderByDescending(path => Path.GetFileName(path).Equals("pt-BR", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
            var useFolder = sourceFolder is not null && (sourceFile is null || Directory.Exists(Path.Combine(i18n, "default")));
            var source = useFolder ? sourceFolder : sourceFile ?? sourceFolder;
            if (source is null) continue;
            var sourceLocale = useFolder || sourceFile is null ? Path.GetFileName(source) : TranslationLocales.FromFileName(source)!;
            if (LibraryHasLocale(libraryRoot, mod, sourceLocale)) continue;
            try
            {
                if (useFolder || sourceFile is null)
                    ImportI18nDirectory(libraryRoot, mod, source, sourceLocale);
                else
                    ImportI18nFile(libraryRoot, mod, source, sourceLocale, overwrite: false);
                imported++;
            }
            catch (JsonException) { invalid++; }
            catch (InvalidOperationException) { invalid++; }
        }
        return (imported, invalid);
    }

    public List<MatchResult> FindMissingTranslationFiles(IEnumerable<InstalledMod> mods, IEnumerable<MatchResult> matched, string locale)
    {
        var represented = matched.Where(result => result.Target is not null && result.State != MatchState.NeedsConfirmation
                && result.Translation.Locale == locale
                && !result.Translation.IsContentTextMap
                && Path.GetDirectoryName(result.Translation.TargetRelativePath)?.Equals("i18n", StringComparison.OrdinalIgnoreCase) == true)
            .Select(result => result.Target!.Directory).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = new List<MatchResult>();
        foreach (var mod in mods)
        {
            if (represented.Contains(mod.Directory)) continue;
            var i18n = Path.Combine(mod.Directory, "i18n");
            if (!Directory.Exists(i18n)) continue;
            var hasLocale = HasI18nLocale(i18n, locale);
            if (hasLocale) continue;
            var placeholder = new TranslationUnit(mod.PackageFolder, mod.FolderName, "",
                Path.Combine("i18n", locale + ".json"), false, mod.RelativeFolder, Locale: locale);
            missing.Add(new MatchResult
            {
                Translation = placeholder,
                Target = mod,
                State = MatchState.MissingTranslationFile,
                Detail = UiLanguage.Text("Arquivo de tradução não encontrado. Clique duas vezes para procurar no computador.",
                    "Translation file not found. Double-click to browse your computer.")
            });
        }
        return missing;
    }

    public string ImportI18nFile(string libraryRoot, InstalledMod mod, string sourcePath, string locale, bool overwrite)
    {
        locale = TranslationLocales.Normalize(locale)
            ?? throw new InvalidOperationException(UiLanguage.Text("Código de idioma inválido.", "Invalid language code."));
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException(UiLanguage.Text("O arquivo escolhido não foi encontrado.", "The selected file was not found."), sourcePath);
        if (!Path.GetExtension(sourcePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(UiLanguage.Text("Escolha um arquivo JSON de tradução.", "Choose a JSON translation file."));
        if (new[] { "manifest.json", "config.json", "content.json" }.Contains(Path.GetFileName(sourcePath), StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(UiLanguage.Text("Esse arquivo não é uma tradução i18n. Escolha o arquivo de idioma.", "This is not an i18n translation. Choose a language file."));
        using (var doc = JsonDocument.Parse(File.ReadAllText(sourcePath), ManifestJsonOptions))
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException(UiLanguage.Text("O arquivo JSON não contém um objeto de tradução.", "The JSON file does not contain a translation object."));
        }

        var destination = GetImportDestination(libraryRoot, mod, locale);
        if (LibraryHasLocaleFolder(libraryRoot, mod, locale))
            throw new InvalidOperationException(UiLanguage.Text("Já existe uma pasta de tradução desse idioma na biblioteca.", "A translation folder for this language already exists in the library."));
        if (Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(UiLanguage.Text("Esse arquivo já está na biblioteca.", "This file is already in the library."));
        if (File.Exists(destination) && !overwrite)
            throw new IOException(UiLanguage.Text("Já existe uma cópia desse idioma na biblioteca.", "A copy in this language already exists in the library."));

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
        {
            var backupDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TradutorModsStardew", "Backups", "ImportedTranslations", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backupDirectory);
            File.Copy(destination, Path.Combine(backupDirectory, Path.GetFileName(destination)));
        }
        File.Copy(sourcePath, destination, overwrite);
        return destination;
    }

    public string ImportI18nDirectory(string libraryRoot, InstalledMod mod, string sourcePath, string locale)
    {
        locale = TranslationLocales.Normalize(locale)
            ?? throw new InvalidOperationException(UiLanguage.Text("Código de idioma inválido.", "Invalid language code."));
        if (!Directory.Exists(sourcePath) || !ContainsJson(sourcePath))
            throw new InvalidOperationException(UiLanguage.Text("Escolha uma pasta de idioma que contenha arquivos JSON.", "Choose a language folder containing JSON files."));
        if (LibraryHasLocale(libraryRoot, mod, locale))
            throw new IOException(UiLanguage.Text("Já existe uma tradução desse idioma na biblioteca; nada foi substituído.", "A translation in this language already exists in the library; nothing was replaced."));
        var destination = Path.Combine(libraryRoot, mod.RelativeFolder, "i18n", locale);
        if (Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(UiLanguage.Text("Essa pasta já está na biblioteca.", "This folder is already in the library."));
        CopyPath(sourcePath, destination, false);
        return destination;
    }

    private static bool ContainsJson(string directory) =>
        Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories).Any();

    private static bool HasI18nLocale(string i18n, string locale) =>
        Directory.Exists(i18n) &&
        (Directory.EnumerateFiles(i18n, "*.json").Any(path =>
            TranslationLocales.SameTargetLanguage(TranslationLocales.FromFileName(path) ?? "", locale)) ||
         Directory.EnumerateDirectories(i18n).Any(path =>
            TranslationLocales.SameTargetLanguage(Path.GetFileName(path), locale) && ContainsJson(path)));

    private static bool LibraryHasLocale(string libraryRoot, InstalledMod mod, string locale) =>
        HasI18nLocale(Path.Combine(libraryRoot, mod.RelativeFolder, "i18n"), locale);

    private static bool LibraryHasLocaleFolder(string libraryRoot, InstalledMod mod, string locale)
    {
        var i18n = Path.Combine(libraryRoot, mod.RelativeFolder, "i18n");
        return Directory.Exists(i18n) && Directory.EnumerateDirectories(i18n)
            .Any(path => TranslationLocales.SameTargetLanguage(Path.GetFileName(path), locale));
    }

    public string GetImportDestination(string libraryRoot, InstalledMod mod, string locale)
    {
        locale = TranslationLocales.Normalize(locale)
            ?? throw new InvalidOperationException(UiLanguage.Text("Código de idioma inválido.", "Invalid language code."));
        var i18n = Path.Combine(libraryRoot, mod.RelativeFolder, "i18n");
        var exact = Path.Combine(i18n, locale + ".json");
        if (File.Exists(exact)) return exact;
        if (locale is "pt" or "pt-BR" && Directory.Exists(i18n))
        {
            var equivalent = Directory.EnumerateFiles(i18n, "*.json")
                .FirstOrDefault(path => TranslationLocales.SameTargetLanguage(
                    TranslationLocales.FromFileName(path) ?? "", locale));
            if (equivalent is not null) return equivalent;
        }
        return exact;
    }

    public List<MatchResult> Match(List<TranslationUnit> translations, List<InstalledMod> mods)
    {
        var results = new List<MatchResult>();
        foreach (var tr in translations)
        {
            var ranked = mods.Select(mod => (Mod: mod, Score: Score(tr, mod)))
                .OrderByDescending(p => p.Score).ThenBy(p => p.Mod.DisplayName).ToList();
            var best = ranked.FirstOrDefault();
            var secondScore = ranked.Count > 1 ? ranked[1].Score : 0;
            var result = new MatchResult { Translation = tr, State = MatchState.NoMatch };

            if (best.Mod is null || best.Score < 55)
            {
                result.Detail = UiLanguage.Text("Nenhum mod correspondente foi encontrado.", "No matching mod was found.");
            }
            else
            {
                result.Target = best.Mod;
                result.Score = best.Score;
                bool safe = best.Score >= 90 && best.Score - secondScore >= 15;
                result.State = safe ? MatchState.Safe : MatchState.NeedsConfirmation;
                result.Selected = safe;
                result.Detail = safe ? UiLanguage.Text("Correspondência segura.", "Safe match.") : UiLanguage.Text("Confirme o mod de destino.", "Confirm the target mod.");

                if (HasTranslation(best.Mod, tr))
                {
                    result.State = MatchState.AlreadyTranslated;
                    result.Selected = false;
                    result.Detail = UiLanguage.Text("O mod já possui tradução neste idioma; nada será substituído.", "The mod already has a translation in this language; nothing will be overwritten.");
                }
            }
            results.Add(result);
        }
        return results;
    }

    public ApplyRecord Apply(MatchResult result, string backupRoot)
    {
        if (result.Target is null)
            throw new InvalidOperationException(UiLanguage.Text("Escolha primeiro o mod de destino.", "Choose the target mod first."));
        if (HasTranslation(result.Target, result.Translation))
            throw new InvalidOperationException(UiLanguage.Text("O mod já possui tradução neste idioma.", "The mod already has a translation in this language."));

        var destination = Path.Combine(result.Target.Directory, result.Translation.TargetRelativePath);
        if (result.Translation.IsContentTextMap)
        {
            if (!File.Exists(destination))
                throw new InvalidOperationException(UiLanguage.Text("O content.json do mod não foi encontrado.", "The mod's content.json was not found."));
            var contentBackup = Path.Combine(backupRoot, Path.GetRelativePath(Path.GetPathRoot(destination)!, destination));
            CopyPath(destination, contentBackup, true);
            ApplyContentTextMap(result.Translation.SourcePath, destination);
            return new ApplyRecord(destination, contentBackup, false);
        }
        string? backup = null;
        bool createdNew = !File.Exists(destination) && !Directory.Exists(destination);
        if (!createdNew)
        {
            var relative = Path.GetRelativePath(Path.GetPathRoot(destination)!, destination);
            backup = Path.Combine(backupRoot, relative);
            CopyPath(destination, backup, true);
        }
        CopyPath(result.Translation.SourcePath, destination, true);
        return new ApplyRecord(destination, backup, createdNew);
    }

    public void Undo(IEnumerable<ApplyRecord> records)
    {
        foreach (var record in records.Reverse())
        {
            if (record.CreatedNew)
            {
                if (File.Exists(record.TargetPath)) File.Delete(record.TargetPath);
                else if (Directory.Exists(record.TargetPath)) Directory.Delete(record.TargetPath, true);
            }
            else if (record.BackupPath is not null)
            {
                CopyPath(record.BackupPath, record.TargetPath, true);
            }
        }
    }

    public bool HasTranslation(InstalledMod mod, TranslationUnit unit)
    {
        if (unit.IsContentTextMap)
        {
            var destination = Path.Combine(mod.Directory, unit.TargetRelativePath);
            return File.Exists(destination) && IsContentTextMapApplied(unit.SourcePath, destination);
        }
        if (unit.IsDirectory)
        {
            if (Path.GetDirectoryName(unit.TargetRelativePath)?.Equals("i18n", StringComparison.OrdinalIgnoreCase) == true)
                return HasI18nLocale(Path.Combine(mod.Directory, "i18n"), unit.Locale);
            var dialogues = Path.Combine(mod.Directory, "assets", "Dialogues");
            return Directory.Exists(dialogues) && Directory.EnumerateDirectories(dialogues)
                .Any(path => TranslationLocales.SameTargetLanguage(Path.GetFileName(path), unit.Locale));
        }
        var i18n = Path.Combine(mod.Directory, "i18n");
        return HasI18nLocale(i18n, unit.Locale);
    }

    private static bool IsContentTextMapApplied(string mapPath, string targetPath)
    {
        try
        {
            var map = ReadContentTextMap(mapPath);
            var target = File.ReadAllText(targetPath);
            return map.Entries.All(entry => JsonStringLiterals(entry.Translation)
                .Any(literal => target.Contains(literal, StringComparison.Ordinal)));
        }
        catch { return false; }
    }

    private static void ApplyContentTextMap(string mapPath, string targetPath)
    {
        var map = ReadContentTextMap(mapPath);
        var target = File.ReadAllText(targetPath);
        foreach (var entry in map.Entries)
        {
            var pairs = JsonStringLiteralPairs(entry).ToList();
            if (pairs.Any(pair => target.Contains(pair.Translation, StringComparison.Ordinal)))
                continue;
            var matchingPair = pairs.FirstOrDefault(pair => target.Contains(pair.Original, StringComparison.Ordinal));
            if (matchingPair.Original is null)
                throw new InvalidOperationException(UiLanguage.Text("Uma descrição do content.json foi alterada pelo autor; nenhuma alteração foi aplicada.", "The author changed a content.json description; no changes were applied."));
            target = target.Replace(matchingPair.Original, matchingPair.Translation, StringComparison.Ordinal);
        }
        File.WriteAllText(targetPath, target, new UTF8Encoding(false));
    }

    private static IEnumerable<(string Original, string Translation)> JsonStringLiteralPairs(ContentTextEntry entry)
    {
        yield return (
            JsonSerializer.Serialize(entry.Original, RelaxedJsonStringOptions),
            JsonSerializer.Serialize(entry.Translation, RelaxedJsonStringOptions));

        var escapedOriginal = JsonSerializer.Serialize(entry.Original);
        var escapedTranslation = JsonSerializer.Serialize(entry.Translation);
        if (escapedOriginal != JsonSerializer.Serialize(entry.Original, RelaxedJsonStringOptions)
            || escapedTranslation != JsonSerializer.Serialize(entry.Translation, RelaxedJsonStringOptions))
            yield return (escapedOriginal, escapedTranslation);
    }

    private static IEnumerable<string> JsonStringLiterals(string value)
    {
        yield return JsonSerializer.Serialize(value, RelaxedJsonStringOptions);
        var escaped = JsonSerializer.Serialize(value);
        if (escaped != JsonSerializer.Serialize(value, RelaxedJsonStringOptions))
            yield return escaped;
    }

    private static ContentTextMap ReadContentTextMap(string path)
    {
        var map = JsonSerializer.Deserialize<ContentTextMap>(File.ReadAllText(path))
            ?? throw new InvalidOperationException(UiLanguage.Text("O mapa de textos do content.json é inválido.", "The content.json text map is invalid."));
        if (map.TargetFile != "content.json" || map.Entries.Count == 0)
            throw new InvalidOperationException(UiLanguage.Text("O mapa de textos do content.json é inválido.", "The content.json text map is invalid."));
        return map;
    }

    private static int Score(TranslationUnit tr, InstalledMod mod)
    {
        // Matching the same component inside the same package is stronger than similar display names.
        if (tr.RelativeComponentPath.Equals(mod.RelativeFolder, StringComparison.OrdinalIgnoreCase))
            return 250;
        var component = Normalize(tr.ComponentName);
        var package = Normalize(tr.LibraryPackage);
        var folder = Normalize(mod.FolderName);
        var name = Normalize(mod.DisplayName);
        var idTail = Normalize(mod.UniqueId.Split('.').LastOrDefault() ?? "");
        var samePackage = package == Normalize(mod.PackageFolder);
        if (component == folder || component == name)
            return 100 + ContentPackTypeBonus(tr.ComponentName, mod.FolderName) + (samePackage ? 25 : 0);
        if (package == folder || package == name) return 96 + (samePackage ? 25 : 0);
        if (component == idTail || package == idTail) return 92;
        if (ContainsMeaningful(component, folder) || ContainsMeaningful(folder, component)) return 78;
        if (ContainsMeaningful(package, folder) || ContainsMeaningful(folder, package)) return 72;
        return TokenSimilarity(component + " " + package, folder + " " + name + " " + idTail);
    }

    private static int TokenSimilarity(string left, string right)
    {
        var a = Tokens(left); var b = Tokens(right);
        if (a.Count == 0 || b.Count == 0) return 0;
        var overlap = a.Intersect(b).Count();
        return (int)Math.Round(65.0 * overlap / Math.Max(a.Count, b.Count));
    }

    private static HashSet<string> Tokens(string value) => Normalize(value)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Where(p => p.Length > 1 && !int.TryParse(p, out _))
        .ToHashSet(StringComparer.Ordinal);

    private static bool ContainsMeaningful(string a, string b) =>
        a.Length >= 6 && b.Length >= 6 && (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal));

    private static int ContentPackTypeBonus(string translationComponent, string modFolder)
    {
        var translationType = PackType(translationComponent);
        var modType = PackType(modFolder);
        return translationType is not null && translationType == modType ? 20 : 0;
    }

    private static string? PackType(string value)
    {
        var match = Regex.Match(value, @"^\s*\[(CP|AT|MFM|CC)\]", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    internal static string Normalize(string value)
    {
        value = value.Normalize(NormalizationForm.FormD);
        var chars = value.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
            System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
        var cleaned = Regex.Replace(new string(chars).ToLowerInvariant(), @"\[[^]]+\]|\([^)]*\)|\bv?\d+(?:[._-]\d+)*\b|\b(cp|cc|c#|dll|gen|npc|mfm|cs)\b", " ");
        return Regex.Replace(cleaned, @"[^a-z0-9]+", " ").Trim();
    }

    private static string FirstPart(string root, string path) =>
        Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];

    private static string? GetString(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
        return null;
    }

    private static void CopyPath(string source, string destination, bool overwrite)
    {
        if (File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite);
            return;
        }
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite);
        }
    }
}
