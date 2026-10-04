using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Input;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using TradutorModsStardew;

namespace TradutorModsDeck;

internal sealed class MainWindow : Window
{
    private static string T(string pt, string en) => UiLanguage.Text(pt, en);
    private readonly TranslationService service = new();
    private readonly TextBox library = Field(FindInitialLibraryPath());
    private readonly TextBox mods = Field(FindInitialModsPath());
    private readonly TextBox search = Field();
    private readonly ComboBox interfaceLanguage = new() { ItemsSource = new[] { "Português (Brasil)", "English" }, SelectedIndex = UiLanguage.Portuguese ? 0 : 1, Width = 155 };
    private readonly ComboBox locale = new() { ItemsSource = new[] { "pt-BR", "en", "es", "fr", "de", "it", "ja", "ko", "zh-Hans", "zh-Hant", "ru", "tr", "pl", "cs" }, SelectedItem = UiLanguage.TranslationLocale, Width = 140 };
    private readonly TextBlock summary = new() { Foreground = Brush("#AEE0E7"), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock subtitle = new() { Foreground = Brush("#AEE0E7") };
    private readonly DataGrid grid = new() { AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, Background = Brush("#103148"), Foreground = Brushes.White, BorderBrush = Brush("#1A617D") };
    private TextBlock? libraryLabel, modsLabel, searchLabel, interfaceLabel, localeLabel;
    private Button? analyzeButton, prepareButton, configsButton, applyButton, undoButton;
    private readonly ObservableCollection<Row> rows = [];
    private List<MatchResult> results = [];
    private List<InstalledMod> installed = [];
    private readonly HashSet<string> expandedPackages = new(StringComparer.OrdinalIgnoreCase);
    private List<ApplyRecord> lastApply = [];
    private string SelectedLocale => locale.SelectedItem as string ?? "pt-BR";

    public MainWindow()
    {
        Title = "Valley Modkeeper";
        Width = 1180; Height = 740; MinWidth = 900; MinHeight = 600;
        Background = Brush("#0A1F35");
        var root = new Grid { Margin = new Thickness(22), RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };

        root.Children.Add(new StackPanel { Spacing = 4, Children =
        {
            new TextBlock { Text = "Valley Modkeeper", FontSize = 23, FontWeight = FontWeight.Bold, Foreground = Brush("#A1E84A") },
            subtitle
        }});

        var fields = new Grid { Margin = new Thickness(0, 18, 0, 12), Background = Brush("#133A53"), ColumnDefinitions = new ColumnDefinitions("205,*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto") };
        libraryLabel = AddFolderField(fields, 0, "Biblioteca de traduções:", library, "Escolha sua biblioteca de traduções");
        modsLabel = AddFolderField(fields, 1, "Pasta Mods do Stardew:", mods, "Escolha a pasta Mods do Stardew Valley");
        searchLabel = AddField(fields, 2, "Pesquisar na lista:", search);
        search.Watermark = "Digite o nome do mod, tradução ou UniqueID…";
        search.TextChanged += (_, _) => RefreshRows();
        var languageRow = new Grid { ColumnDefinitions = new ColumnDefinitions("205,180,205,160,*") };
        interfaceLabel = AddChoice(languageRow, 0, "Interface:", interfaceLanguage);
        localeLabel = AddChoice(languageRow, 0, "Idioma da tradução:", locale, 2, 3);
        Grid.SetRow(languageRow, 3);
        Grid.SetColumnSpan(languageRow, 3);
        fields.Children.Add(languageRow);
        interfaceLanguage.SelectionChanged += (_, _) => { UiLanguage.Set(interfaceLanguage.SelectedIndex == 0); ApplyInterfaceLanguage(); RefreshRows(); };
        locale.SelectionChanged += (_, _) => { if (locale.SelectedItem is string code) { UiLanguage.SetTranslationLocale(code); RefreshMissing(); RefreshRows(); } };
        Grid.SetRow(fields, 1); root.Children.Add(fields);

        grid.ItemsSource = rows;
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Aplicar", Binding = new Binding("Selected") });
        grid.Columns.Add(new DataGridTextColumn { Header = "Tradução", Binding = new Binding("Translation"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Mod encontrado", Binding = new Binding("Target"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Situação", Binding = new Binding("Status"), Width = new DataGridLength(160) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Detalhes", Binding = new Binding("Detail"), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
        grid.DoubleTapped += async (_, _) => { if (grid.SelectedItem is Row row) await OpenRowAsync(row); };
        Grid.SetRow(grid, 2); root.Children.Add(grid);

        var footer = new Grid { Margin = new Thickness(0, 14, 0, 0), RowDefinitions = new RowDefinitions("Auto,Auto") };
        summary.TextWrapping = TextWrapping.Wrap;
        summary.Margin = new Thickness(0, 0, 0, 8);
        footer.Children.Add(summary);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Stretch };
        analyzeButton = Button("Analisar", "#1A617D"); analyzeButton.Click += Analyze;
        prepareButton = Button("Procurar novos mods", "#685BA6"); prepareButton.Click += PrepareNewMods;
        configsButton = Button("Backup de configurações", "#685BA6"); configsButton.Click += OpenConfigBackup;
        applyButton = Button("Aplicar selecionadas", "#286A58"); applyButton.Click += Apply;
        undoButton = Button("Desfazer última aplicação", "#685BA6"); undoButton.Click += UndoLast;
        actions.Children.Add(analyzeButton); actions.Children.Add(prepareButton); actions.Children.Add(configsButton); actions.Children.Add(applyButton); actions.Children.Add(undoButton);
        foreach (var button in actions.Children.OfType<Button>()) button.Margin = new Thickness(0, 0, 8, 8);
        Grid.SetRow(actions, 1); footer.Children.Add(actions);
        Grid.SetRow(footer, 3); root.Children.Add(footer);
        Content = root;
        ApplyInterfaceLanguage();
    }

    private void ApplyInterfaceLanguage()
    {
        subtitle.Text = T("Transfira traduções com segurança e preserve configurações dos mods.", "Safely transfer translations and save mod settings.");
        if (libraryLabel is not null) libraryLabel.Text = T("Biblioteca de traduções:", "Translation library:");
        if (modsLabel is not null) modsLabel.Text = T("Pasta Mods do Stardew:", "Stardew Mods folder:");
        if (searchLabel is not null) searchLabel.Text = T("Pesquisar na lista:", "Search list:");
        if (interfaceLabel is not null) interfaceLabel.Text = "Interface:";
        if (localeLabel is not null) localeLabel.Text = T("Idioma da tradução:", "Translation language:");
        search.Watermark = T("Digite o nome do mod, tradução ou UniqueID…", "Search for a mod, translation, or UniqueID…");
        if (analyzeButton is not null) analyzeButton.Content = T("Analisar", "Analyze");
        if (prepareButton is not null) prepareButton.Content = T("Procurar novos mods", "Find new mods");
        if (configsButton is not null) configsButton.Content = T("Backup de configurações", "Settings backup");
        if (applyButton is not null) applyButton.Content = T("Aplicar selecionadas", "Apply selected");
        if (undoButton is not null) undoButton.Content = T("Desfazer última aplicação", "Undo last application");
        if (grid.Columns.Count == 5)
        {
            grid.Columns[0].Header = T("Aplicar", "Apply");
            grid.Columns[1].Header = T("Tradução", "Translation");
            grid.Columns[2].Header = T("Mod encontrado", "Matched mod");
            grid.Columns[3].Header = T("Situação", "Status");
            grid.Columns[4].Header = T("Detalhes", "Details");
        }
    }

    private async void Analyze(object? sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(library.Text) || !Directory.Exists(mods.Text)) { summary.Text = T("Escolha duas pastas existentes.", "Choose two existing folders."); return; }
        try
        {
            installed = service.ScanMods(mods.Text!);
            results = service.Match(service.ScanLibrary(library.Text!), installed);
            LoadSavedMappings();
            RefreshMissing();
            RefreshRows();
        }
        catch (Exception ex) { summary.Text = T("Erro na análise: ", "Analysis failed: ") + ex.Message; }
        await Task.CompletedTask;
    }

    private async void Apply(object? sender, RoutedEventArgs e)
    {
        var selected = results.Where(x => x.Translation.Locale == SelectedLocale && x.Selected && x.Target is not null && x.State == MatchState.Safe).ToList();
        if (selected.Count == 0) return;
        if (!await ConfirmAsync("Confirmar aplicação", $"Aplicar {selected.Count} tradução(ões) em {SelectedLocale}?\n\nTraduções existentes serão preservadas.")) return;
        var backup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TradutorModsStardew", "Backups", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        var done = 0;
        lastApply = [];
        foreach (var result in selected) { try { lastApply.Add(service.Apply(result, backup)); result.State = MatchState.Applied; result.Selected = false; done++; } catch (Exception ex) { result.State = MatchState.Failed; result.Detail = ex.Message; } }
        RefreshRows(); summary.Text = $"{done} tradução(ões) aplicada(s).";
        await Task.CompletedTask;
    }

    private async void UndoLast(object? sender, RoutedEventArgs e)
    {
        if (lastApply.Count == 0) { summary.Text = "Nenhuma aplicação desta sessão para desfazer."; return; }
        if (!await ConfirmAsync("Confirmar", "Desfazer os arquivos aplicados na última operação?")) return;
        try { service.Undo(lastApply); lastApply = []; Analyze(this, e); }
        catch (Exception ex) { summary.Text = "Não foi possível desfazer: " + ex.Message; }
    }

    private async void OpenConfigBackup(object? sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(library.Text) || !Directory.Exists(mods.Text))
        {
            summary.Text = "Escolha duas pastas existentes antes de abrir o backup de configurações.";
            return;
        }
        var window = new ConfigWindow(library.Text!, mods.Text!, service);
        await window.ShowDialog(this);
    }

    private async void PrepareNewMods(object? sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(library.Text) || !Directory.Exists(mods.Text))
        {
            summary.Text = "Escolha duas pastas existentes antes de preparar os novos mods.";
            return;
        }

        try
        {
            installed = service.ScanMods(mods.Text!);
            var report = service.PrepareLibrary(library.Text!, installed);
            var imported = service.ImportInstalledTranslations(library.Text!, installed, SelectedLocale);
            Analyze(this, e);
            summary.Text = $"Busca concluída: {report.CreatedFolders} pasta(s), {report.CopiedI18nReferences} referência(s) i18n, {imported.Imported} tradução(ões) copiadas. Os mods instalados não foram alterados.";
        }
        catch (Exception ex)
        {
            summary.Text = "Erro ao preparar novos mods: " + ex.Message;
        }
        await Task.CompletedTask;
    }

    private void RefreshRows()
    {
        var term = search.Text?.Trim() ?? "";
        rows.Clear();
        var visible = results.Where(x => x.Translation.Locale == SelectedLocale &&
            (string.IsNullOrWhiteSpace(term) || (x.Translation.LibraryPackage + " " + x.Translation.ComponentName + " " + x.Target?.DisplayName + " " + x.Target?.UniqueId).Contains(term, StringComparison.CurrentCultureIgnoreCase)));
        foreach (var group in visible.GroupBy(x => x.Translation.LibraryPackage, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Min(x => StateOrder(x.State))).ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var members = group.OrderBy(x => x.Translation.RelativeComponentPath, StringComparer.CurrentCultureIgnoreCase).ToList();
            if (members.Count == 1) rows.Add(new Row(members[0], false, ScheduleSelectionRefresh));
            else
            {
                rows.Add(new Row(group.Key, members, expandedPackages.Contains(group.Key), ScheduleSelectionRefresh));
                if (expandedPackages.Contains(group.Key))
                    foreach (var member in members) rows.Add(new Row(member, true, ScheduleSelectionRefresh));
            }
        }
        var safe = results.Count(x => x.Translation.Locale == SelectedLocale && x.State == MatchState.Safe);
        var existing = results.Count(x => x.Translation.Locale == SelectedLocale && x.State == MatchState.AlreadyTranslated);
        summary.Text = results.Count == 0 ? T("Escolha as duas pastas para começar.", "Choose the two folders to begin.")
            : T($"{installed.Count} mods • {safe} prontas • {existing} já traduzidas • {rows.Count} linhas exibidas",
                $"{installed.Count} mods • {safe} ready • {existing} already translated • {rows.Count} rows shown");
    }

    private void ScheduleSelectionRefresh() => Dispatcher.UIThread.Post(() =>
    {
        foreach (var row in rows) row.NotifySelectionChanged();
    }, DispatcherPriority.Background);

    private void RefreshMissing()
    {
        results.RemoveAll(result => result.State == MatchState.MissingTranslationFile);
        if (installed.Count > 0)
            results.AddRange(service.FindMissingTranslationFiles(installed, results, SelectedLocale));
    }

    private async Task OpenRowAsync(Row row)
    {
        if (row.PackageName is { } package)
        {
            var parentIndex = rows.IndexOf(row);
            if (parentIndex < 0) return;
            if (expandedPackages.Add(package))
            {
                row.Expanded = true;
                var children = row.Members!;
                for (var i = 0; i < children.Count; i++)
                    rows.Insert(parentIndex + 1 + i, new Row(children[i], true, ScheduleSelectionRefresh));
            }
            else
            {
                row.Expanded = false;
                for (var i = 0; i < row.Members!.Count; i++) rows.RemoveAt(parentIndex + 1);
            }
            return;
        }
        var result = row.Result;
        if (result is null) return;
        if (result.State == MatchState.MissingTranslationFile && result.Target is { } missing)
        {
            await ImportMissingAsync(missing);
            return;
        }
        var target = await PickTargetAsync(result);
        if (target is null) return;
        result.Target = target;
        if (service.HasTranslation(target, result.Translation))
        {
            result.State = MatchState.AlreadyTranslated;
            result.Selected = false;
            result.Detail = "O componente escolhido já possui tradução neste idioma.";
        }
        else
        {
            result.State = MatchState.Safe;
            result.Selected = true;
            result.Detail = "Destino confirmado manualmente.";
            SaveMapping(result);
        }
        RefreshMissing();
        RefreshRows();
    }

    private async Task<InstalledMod?> PickTargetAsync(MatchResult result)
    {
        var dialog = DialogWindow("Confirmar mod de destino", 1050, 650);
        var panel = new DockPanel { Margin = new Thickness(16) };
        var top = new StackPanel { Spacing = 8 };
        top.Children.Add(new TextBlock { Text = "Origem: " + result.Translation.SourcePath, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White });
        var open = Button("Abrir origem no gerenciador de arquivos", "#1A617D");
        open.Click += (_, _) =>
        {
            var path = result.Translation.SourcePath;
            var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (folder is not null && Directory.Exists(folder))
                Process.Start(new ProcessStartInfo("xdg-open", folder) { UseShellExecute = false });
        };
        top.Children.Add(open);
        top.Children.Add(new TextBlock { Text = "Escolha pelo caminho completo e UniqueID:", Foreground = Brushes.White });
        var filter = Field(result.Translation.ComponentName);
        top.Children.Add(filter);
        var destinationDetails = new TextBlock { Text = "Selecione um componente para ver seu destino completo.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#AEE0E7") };
        top.Children.Add(destinationDetails);
        DockPanel.SetDock(top, Dock.Top); panel.Children.Add(top);
        var picker = new ListBox { Background = Brush("#133A53"), Foreground = Brushes.White };
        picker.SelectionChanged += (_, _) => destinationDetails.Text = picker.SelectedItem is PickerItem chosen
            ? $"Destino: {chosen.Mod.Directory}\nUniqueID: {chosen.Mod.UniqueId}"
            : "Selecione um componente para ver seu destino completo.";
        void Fill()
        {
            var query = filter.Text?.Trim() ?? "";
            picker.ItemsSource = installed.Where(mod => query.Length == 0 || mod.PickerName.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .Select(mod => new PickerItem(mod)).ToList();
            picker.SelectedItem = (picker.ItemsSource as IEnumerable<PickerItem>)?.FirstOrDefault(item => item.Mod == result.Target);
        }
        filter.TextChanged += (_, _) => Fill();
        Fill();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Button("Cancelar", "#685BA6"); cancel.Click += (_, _) => dialog.Close((InstalledMod?)null);
        var confirm = Button("Confirmar", "#286A58"); confirm.Click += (_, _) => dialog.Close((picker.SelectedItem as PickerItem)?.Mod);
        buttons.Children.Add(cancel); buttons.Children.Add(confirm);
        DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
        panel.Children.Add(picker);
        dialog.Content = panel;
        return await dialog.ShowDialog<InstalledMod?>(this);
    }

    private async Task ImportMissingAsync(InstalledMod mod)
    {
        var kind = await ChooseImportKindAsync();
        if (kind == 0) return;
        var initialPath = Path.Combine(mod.Directory, "i18n");
        if (!Directory.Exists(initialPath)) initialPath = mod.Directory;
        var start = await StorageProvider.TryGetFolderFromPathAsync(new Uri(initialPath));
        string source;
        string? detected;
        if (kind == 1)
        {
            var chosen = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = $"Escolha a pasta de tradução de {mod.DisplayName}", AllowMultiple = false, SuggestedStartLocation = start
            });
            if (chosen.Count == 0) return;
            source = chosen[0].Path.LocalPath;
            detected = TranslationLocales.Normalize(Path.GetFileName(source));
        }
        else
        {
            var chosen = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Escolha o arquivo de tradução de {mod.DisplayName}", AllowMultiple = false, SuggestedStartLocation = start,
                FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });
            if (chosen.Count == 0) return;
            source = chosen[0].Path.LocalPath;
            detected = TranslationLocales.FromFileName(source);
        }
        if (detected is not null && !TranslationLocales.SameTargetLanguage(detected, SelectedLocale))
        {
            await NoticeAsync("Idioma diferente", $"A origem indica {detected}, mas o idioma escolhido é {SelectedLocale}. Nada foi copiado.");
            return;
        }
        if (detected is null && !await ConfirmAsync("Confirmar idioma", $"O nome não identifica o idioma. Confirmar que esta tradução é em {SelectedLocale}?")) return;
        var language = detected ?? SelectedLocale;
        if (!await ConfirmAsync("Confirmar importação", $"Guardar esta {(kind == 1 ? "pasta inteira" : "tradução")} na biblioteca?\n\nOrigem: {source}\nIdioma: {language}\n\nA origem não será alterada.")) return;
        try
        {
            if (kind == 1) service.ImportI18nDirectory(library.Text!, mod, source, language);
            else
            {
                var destination = service.GetImportDestination(library.Text!, mod, language);
                service.ImportI18nFile(library.Text!, mod, source, language, File.Exists(destination));
            }
            Analyze(this, new RoutedEventArgs());
            await NoticeAsync("Concluído", "A tradução foi guardada na biblioteca. A origem não foi alterada.");
        }
        catch (Exception ex) { await NoticeAsync("Não foi possível importar", ex.Message); }
    }

    private async Task<int> ChooseImportKindAsync()
    {
        var dialog = DialogWindow("Formato da tradução", 580, 230);
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = "A tradução está em uma pasta (i18n/pt/) ou em um arquivo (i18n/pt.json)?", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var folder = Button("Pasta inteira", "#286A58"); folder.Click += (_, _) => dialog.Close(1);
        var file = Button("Arquivo JSON", "#1A617D"); file.Click += (_, _) => dialog.Close(2);
        var cancel = Button("Cancelar", "#685BA6"); cancel.Click += (_, _) => dialog.Close(0);
        buttons.Children.Add(folder); buttons.Children.Add(file); buttons.Children.Add(cancel);
        panel.Children.Add(buttons); dialog.Content = panel;
        return await dialog.ShowDialog<int>(this);
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = DialogWindow(title, 600, 260);
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 18 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var no = Button("Cancelar", "#685BA6"); no.Click += (_, _) => dialog.Close(false);
        var yes = Button("Confirmar", "#286A58"); yes.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(no); buttons.Children.Add(yes); panel.Children.Add(buttons);
        dialog.Content = panel;
        return await dialog.ShowDialog<bool>(this);
    }

    private async Task NoticeAsync(string title, string message)
    {
        var dialog = DialogWindow(title, 600, 240);
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 18 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White });
        var ok = Button("OK", "#1A617D"); ok.HorizontalAlignment = HorizontalAlignment.Right; ok.Click += (_, _) => dialog.Close();
        panel.Children.Add(ok); dialog.Content = panel;
        await dialog.ShowDialog(this);
    }

    private static Window DialogWindow(string title, int width, int height) => new()
    {
        Title = title, Width = width, Height = height, WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Background = Brush("#0A1F35")
    };

    private sealed record PickerItem(InstalledMod Mod)
    {
        public override string ToString() => Mod.PickerName;
    }

    private async Task ChooseFolderAsync(TextBox field, string title)
    {
        var chosen = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        if (chosen.Count > 0)
            field.Text = chosen[0].Path.LocalPath;
    }

    private void LoadSavedMappings()
    {
        var path = MappingPath();
        if (!File.Exists(path)) return;
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
            foreach (var result in results.Where(result => result.State is MatchState.NeedsConfirmation or MatchState.NoMatch))
            {
                if (!map.TryGetValue(MappingKey(result.Translation), out var id)) continue;
                var target = installed.FirstOrDefault(mod => mod.UniqueId.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (target is null) continue;
                var exact = installed.FirstOrDefault(mod => mod.RelativeFolder.Equals(result.Translation.RelativeComponentPath, StringComparison.OrdinalIgnoreCase));
                if (exact is not null && !exact.UniqueId.Equals(target.UniqueId, StringComparison.OrdinalIgnoreCase)) continue;
                result.Target = target;
                result.State = service.HasTranslation(target, result.Translation) ? MatchState.AlreadyTranslated : MatchState.Safe;
                result.Selected = result.State == MatchState.Safe;
                result.Detail = result.Selected ? "Destino lembrado de uma confirmação anterior." : "O destino já possui tradução neste idioma.";
            }
        }
        catch { }
    }

    private void SaveMapping(MatchResult result)
    {
        if (result.Target is null || string.IsNullOrWhiteSpace(result.Target.UniqueId)) return;
        var path = MappingPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Dictionary<string, string> map = [];
        try { if (File.Exists(path)) map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? []; } catch { }
        map[MappingKey(result.Translation)] = result.Target.UniqueId;
        File.WriteAllText(path, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string MappingKey(TranslationUnit unit) => unit.LibraryPackage + "|" + unit.ComponentName + "|" + unit.TargetRelativePath;
    private static string MappingPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TradutorModsStardew", "associacoes.json");

    private static int StateOrder(MatchState state) => state switch
    {
        MatchState.NeedsConfirmation => 0,
        MatchState.MissingTranslationFile => 1,
        MatchState.Safe => 2,
        MatchState.Failed => 3,
        MatchState.AlreadyTranslated => 4,
        MatchState.NoMatch => 5,
        _ => 6
    };

    private TextBlock AddFolderField(Grid grid, int row, string label, TextBox field, string pickerTitle)
    {
        var text = AddField(grid, row, label, field);
        var choose = Button("Escolher…", "#1A617D");
        choose.Margin = new Thickness(0, 7, 16, 7);
        choose.Click += async (_, _) => await ChooseFolderAsync(field, pickerTitle);
        Grid.SetRow(choose, row);
        Grid.SetColumn(choose, 2);
        grid.Children.Add(choose);
        return text;
    }

    private static TextBlock AddField(Grid grid, int row, string label, TextBox field)
    {
        var text = new TextBlock { Text = label, Foreground = Brush("#C2EFEB"), Margin = new Thickness(16, 10), VerticalAlignment = VerticalAlignment.Center };
        field.Margin = new Thickness(8, 7); Grid.SetRow(text, row); Grid.SetRow(field, row); Grid.SetColumn(field, 1); grid.Children.Add(text); grid.Children.Add(field);
        return text;
    }

    private static TextBlock AddChoice(Grid grid, int row, string label, ComboBox choice, int labelColumn = 0, int choiceColumn = 1)
    {
        var text = new TextBlock { Text = label, Foreground = Brush("#C2EFEB"), Margin = new Thickness(16, 10), VerticalAlignment = VerticalAlignment.Center };
        choice.Margin = new Thickness(8, 5);
        Grid.SetRow(text, row); Grid.SetRow(choice, row); Grid.SetColumn(text, labelColumn); Grid.SetColumn(choice, choiceColumn);
        grid.Children.Add(text); grid.Children.Add(choice);
        return text;
    }

    private static string FindInitialLibraryPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(home, "Traduções PT-BR Stardew Valley"),
            Path.Combine(home, "Traducoes PT-BR Stardew Valley"),
            Path.Combine(home, "Documents", "Traduções PT-BR Stardew Valley"),
            Path.Combine(home, "Documents", "Traducoes PT-BR Stardew Valley")
        };
        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    private static string FindInitialModsPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(home, ".local", "share", "Steam", "steamapps", "common", "Stardew Valley", "Mods"),
            Path.Combine(home, ".steam", "steam", "steamapps", "common", "Stardew Valley", "Mods")
        };
        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }
    private static TextBox Field(string text = "") => new() { Text = text, Background = Brush("#0C293D"), Foreground = Brush("#E7F9F7"), BorderBrush = Brush("#4DAEC0") };
    private static Button Button(string text, string color) => new() { Content = text, Background = Brush(color), Foreground = Brushes.White, BorderBrush = Brush("#6CE0E6"), Padding = new Thickness(17, 8), FontWeight = FontWeight.SemiBold };
    private static IBrush Brush(string hex) => SolidColorBrush.Parse(hex);

    private sealed class Row : INotifyPropertyChanged
    {
        private readonly List<MatchResult>? members;
        private readonly bool child;
        private bool expanded;
        private readonly Action changed;
        public event PropertyChangedEventHandler? PropertyChanged;
        public MatchResult? Result { get; }
        public string? PackageName { get; }
        public IReadOnlyList<MatchResult>? Members => members;
        public bool Expanded
        {
            get => expanded;
            set { expanded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Translation))); }
        }
        public Row(MatchResult result, bool child, Action changed) { Result = result; this.child = child; this.changed = changed; }
        public Row(string package, List<MatchResult> members, bool expanded, Action changed)
        { PackageName = package; this.members = members; this.expanded = expanded; this.changed = changed; }
        public bool Selected
        {
            get => Result is not null ? Result.Selected : members?.Any(item => item.State == MatchState.Safe) == true && members.Where(item => item.State == MatchState.Safe).All(item => item.Selected);
            set
            {
                if (Result is not null && Result.State == MatchState.Safe) Result.Selected = value;
                else if (members is not null)
                    foreach (var item in members.Where(item => item.State == MatchState.Safe)) item.Selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
                changed();
            }
        }
        public void NotifySelectionChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Detail)));
        }
        public string Translation => PackageName is not null ? (expanded ? "▼ " : "▶ ") + PackageName
            : child ? "    ↳ " + Result!.Translation.ComponentName
            : Result!.Translation.DisplayName;
        public string Target => PackageName is not null ? T($"{members!.Count} componentes", $"{members!.Count} components")
            : Result!.Target is { } mod ? $"{mod.DisplayName} › {mod.RelativeFolder}" : "—";
        public string Status => PackageName is not null ? T($"{members!.Count(item => item.State == MatchState.Safe)} prontas", $"{members!.Count(item => item.State == MatchState.Safe)} ready") : Result!.State switch
        {
            MatchState.Safe => T("Pronta", "Ready"), MatchState.AlreadyTranslated => T("Já traduzido", "Already translated"), MatchState.NeedsConfirmation => T("Confirmar destino", "Confirm target"),
            MatchState.MissingTranslationFile => T("Arquivo de tradução ausente", "Missing translation file"), MatchState.Applied => T("Aplicada", "Applied"), MatchState.Failed => T("Falhou", "Failed"), _ => T("Não encontrada", "Not found")
        };
        public string Detail => PackageName is not null
            ? T($"{members!.Count(item => item.Selected && item.State == MatchState.Safe)} selecionadas • {members!.Count(item => item.State == MatchState.NeedsConfirmation)} a confirmar",
                $"{members!.Count(item => item.Selected && item.State == MatchState.Safe)} selected • {members!.Count(item => item.State == MatchState.NeedsConfirmation)} to review")
            : Result!.Detail;
    }
}
