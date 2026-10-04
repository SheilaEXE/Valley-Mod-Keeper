using System.Text.Json;

namespace TradutorModsStardew;

/// <summary>Guarda config.json por UniqueID, para que mudanças no nome das pastas não apaguem os atalhos da jogadora.</summary>
internal sealed class ConfigBackupService
{
    private static string T(string pt, string en) => UiLanguage.Text(pt, en);
    private const string ConfigFolderName = "Configurações dos Mods";
    private const string MetadataFileName = "informacoes.json";

    private sealed record StoredConfigInfo(string UniqueId, string DisplayName, DateTimeOffset ExportedAt);

    public string GetStorageRoot(string libraryRoot) => Path.Combine(libraryRoot, ConfigFolderName);

    public List<ConfigResult> Compare(string libraryRoot, IEnumerable<InstalledMod> mods)
    {
        var snapshots = ReadSnapshots(libraryRoot);
        return mods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.UniqueId))
            .GroupBy(mod => mod.UniqueId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(mod => File.Exists(Path.Combine(mod.Directory, "config.json")) || snapshots.ContainsKey(mod.UniqueId))
            .OrderBy(mod => mod.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(mod => CreateResult(mod, snapshots.GetValueOrDefault(mod.UniqueId)))
            .ToList();
    }

    public int ExportAll(string libraryRoot, IEnumerable<InstalledMod> mods)
    {
        var count = 0;
        foreach (var mod in mods.Where(mod => !string.IsNullOrWhiteSpace(mod.UniqueId)))
        {
            var source = Path.Combine(mod.Directory, "config.json");
            if (!File.Exists(source))
                continue;

            var folder = Path.Combine(GetStorageRoot(libraryRoot), FolderFor(mod.UniqueId));
            Directory.CreateDirectory(folder);
            var destination = Path.Combine(folder, "config.json");
            File.Copy(source, destination, true);
            var info = new StoredConfigInfo(mod.UniqueId, mod.DisplayName, DateTimeOffset.UtcNow);
            File.WriteAllText(Path.Combine(folder, MetadataFileName), JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
            count++;
        }
        return count;
    }

    public string Restore(string libraryRoot, ConfigResult result)
    {
        if (result.Snapshot is null || !File.Exists(result.Snapshot.ConfigPath))
            throw new InvalidOperationException(T("Não foi encontrada uma cópia salva deste config.json.", "No saved copy of this config.json was found."));

        var destination = Path.Combine(result.Mod.Directory, "config.json");
        var backupRoot = Path.Combine(GetStorageRoot(libraryRoot), "Backups antes de restaurar", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"), FolderFor(result.Mod.UniqueId));
        if (File.Exists(destination))
        {
            Directory.CreateDirectory(backupRoot);
            File.Copy(destination, Path.Combine(backupRoot, "config.json"), true);
        }

        Directory.CreateDirectory(result.Mod.Directory);
        File.Copy(result.Snapshot.ConfigPath, destination, true);
        return backupRoot;
    }

    private ConfigResult CreateResult(InstalledMod mod, ConfigSnapshot? snapshot)
    {
        if (snapshot is null)
            return new ConfigResult { Mod = mod, State = ConfigState.NoSavedCopy, Detail = T("Ainda não há uma cópia salva.", "There is no saved copy yet.") };

        var current = Path.Combine(mod.Directory, "config.json");
        if (File.Exists(current) && FilesEqual(current, snapshot.ConfigPath))
            return new ConfigResult { Mod = mod, Snapshot = snapshot, State = ConfigState.SameAsSaved, Detail = T("A configuração atual já é igual à cópia salva.", "The current configuration matches the saved copy.") };

        var detail = File.Exists(current)
            ? T("Há diferenças. Ao restaurar, a configuração atual será guardada em um backup datado.", "There are differences. Restoring will first save the current configuration in a dated backup.")
            : T("O mod ainda não possui config.json. A cópia salva pode ser restaurada.", "This mod has no config.json yet. The saved copy can be restored.");
        return new ConfigResult { Mod = mod, Snapshot = snapshot, State = ConfigState.ReadyToRestore, Detail = detail, Selected = true };
    }

    private Dictionary<string, ConfigSnapshot> ReadSnapshots(string libraryRoot)
    {
        var root = GetStorageRoot(libraryRoot);
        var snapshots = new Dictionary<string, ConfigSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root))
            return snapshots;

        foreach (var metadataPath in Directory.EnumerateFiles(root, MetadataFileName, SearchOption.AllDirectories))
            AddSnapshot(metadataPath, snapshots);
        return snapshots;
    }

    private static void AddSnapshot(string metadataPath, Dictionary<string, ConfigSnapshot> snapshots)
    {
        try
        {
            var info = JsonSerializer.Deserialize<StoredConfigInfo>(File.ReadAllText(metadataPath));
            if (info is null || string.IsNullOrWhiteSpace(info.UniqueId)) return;
            var configPath = Path.Combine(Path.GetDirectoryName(metadataPath)!, "config.json");
            if (!File.Exists(configPath)) return;
            snapshots[info.UniqueId] = new ConfigSnapshot(info.UniqueId, info.DisplayName, configPath, metadataPath, info.ExportedAt);
        }
        catch
        {
            // A malformed saved file must not prevent the other mods from appearing.
        }
    }

    private static bool FilesEqual(string first, string second) => File.ReadAllBytes(first).AsSpan().SequenceEqual(File.ReadAllBytes(second));

    private static string FolderFor(string uniqueId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(uniqueId.Select(character => invalid.Contains(character) ? '_' : character));
    }
}
