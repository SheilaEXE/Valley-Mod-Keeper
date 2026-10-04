namespace TradutorModsStardew;

internal enum MatchState
{
    Safe,
    NeedsConfirmation,
    AlreadyTranslated,
    NoMatch,
    MissingTranslationFile,
    Applied,
    Failed
}

internal sealed record InstalledMod(
    string Directory,
    string FolderName,
    string DisplayName,
    string UniqueId,
    string PackageFolder,
    string RelativeFolder)
{
    public string PickerName => $"{DisplayName}  —  {RelativeFolder}  —  {UniqueId}  —  {Directory}";
}

internal sealed record TranslationUnit(
    string LibraryPackage,
    string ComponentName,
    string SourcePath,
    string TargetRelativePath,
    bool IsDirectory,
    string RelativeComponentPath,
    bool IsContentTextMap = false,
    string Locale = "pt-BR")
{
    public string DisplayName => LibraryPackage == ComponentName
        ? ComponentName
        : $"{LibraryPackage} › {ComponentName}";
}

internal sealed record ContentTextMap(string TargetFile, List<ContentTextEntry> Entries);
internal sealed record ContentTextEntry(string Original, string Translation);

internal sealed class MatchResult
{
    public required TranslationUnit Translation { get; init; }
    public InstalledMod? Target { get; set; }
    public MatchState State { get; set; }
    public int Score { get; set; }
    public string Detail { get; set; } = "";
    public bool Selected { get; set; }
}

internal sealed record ApplyRecord(string TargetPath, string? BackupPath, bool CreatedNew);

internal sealed record LibraryPreparationReport(
    int CreatedFolders,
    int CopiedI18nReferences,
    int CopiedContentReferences,
    int AlreadyPrepared);

internal enum ConfigState
{
    ReadyToRestore,
    SameAsSaved,
    NoSavedCopy,
    Failed
}

internal sealed record ConfigSnapshot(
    string UniqueId,
    string DisplayName,
    string ConfigPath,
    string MetadataPath,
    DateTimeOffset ExportedAt);

internal sealed class ConfigResult
{
    public required InstalledMod Mod { get; init; }
    public ConfigSnapshot? Snapshot { get; set; }
    public ConfigState State { get; set; }
    public string Detail { get; set; } = "";
    public bool Selected { get; set; }
}
