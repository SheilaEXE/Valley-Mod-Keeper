using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using System.Collections.ObjectModel;
using TradutorModsStardew;

namespace TradutorModsDeck;

internal sealed class ConfigWindow : Window
{
    private static string T(string pt, string en) => UiLanguage.Text(pt, en);
    private readonly string libraryRoot;
    private readonly string modsRoot;
    private readonly TranslationService modService;
    private readonly ConfigBackupService configService = new();
    private readonly ObservableCollection<Row> rows = [];
    private readonly TextBlock summary = new() { Foreground = Brush("#AEE0E7"), VerticalAlignment = VerticalAlignment.Center };
    private List<ConfigResult> results = [];

    public ConfigWindow(string libraryRoot, string modsRoot, TranslationService modService)
    {
        this.libraryRoot = libraryRoot;
        this.modsRoot = modsRoot;
        this.modService = modService;
        Title = T("Backup de configurações dos mods", "Mod settings backup");
        Width = 1080; Height = 670; MinWidth = 820; MinHeight = 510;
        Background = Brush("#0A1F35");
        var root = new Grid { Margin = new Thickness(22), RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        root.Children.Add(new StackPanel { Spacing = 4, Children =
        {
            new TextBlock { Text = T("Backup de configurações dos mods", "Mod settings backup"), FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Brush("#A1E84A") },
            new TextBlock { Text = T("Salve seus config.json antes de atualizar. Cada restauração pede sua confirmação e protege a configuração atual em um backup datado.", "Save config.json files before updating. Each restore asks for confirmation and backs up the current settings first."), TextWrapping = TextWrapping.Wrap, Foreground = Brush("#AEE0E7") }
        }});
        var grid = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, Background = Brush("#103148"), Foreground = Brushes.White, BorderBrush = Brush("#1A617D"), Margin = new Thickness(0, 18, 0, 0) };
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = T("Restaurar", "Restore"), Binding = new Binding("Selected") });
        grid.Columns.Add(new DataGridTextColumn { Header = "Mod", Binding = new Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "UniqueID", Binding = new Binding("UniqueId"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = T("Situação", "Status"), Binding = new Binding("Status"), Width = new DataGridLength(180) });
        grid.Columns.Add(new DataGridTextColumn { Header = T("Detalhes", "Details"), Binding = new Binding("Detail"), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star) });
        Grid.SetRow(grid, 1); root.Children.Add(grid);
        var footer = new Grid { Margin = new Thickness(0, 14, 0, 0), ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        footer.Children.Add(summary);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var refresh = Button(T("Atualizar lista", "Refresh list"), "#685BA6"); refresh.Click += (_, _) => LoadResults();
        var export = Button(T("Exportar todos os configs", "Export all settings"), "#1A617D"); export.Click += ExportAll;
        var restore = Button(T("Restaurar selecionados", "Restore selected"), "#286A58"); restore.Click += RestoreSelected;
        actions.Children.Add(refresh); actions.Children.Add(export); actions.Children.Add(restore);
        Grid.SetColumn(actions, 1); footer.Children.Add(actions);
        Grid.SetRow(footer, 2); root.Children.Add(footer);
        Content = root;
        LoadResults();
    }

    private void LoadResults()
    {
        try
        {
            results = configService.Compare(libraryRoot, modService.ScanMods(modsRoot));
            rows.Clear();
            foreach (var result in results) rows.Add(new Row(result));
            summary.Text = T($"{results.Count} mods encontrados • {results.Count(x => x.State == ConfigState.ReadyToRestore)} configuração(ões) prontas para restaurar",
                $"{results.Count} mods found • {results.Count(x => x.State == ConfigState.ReadyToRestore)} settings ready to restore");
        }
        catch (Exception ex) { summary.Text = T("Erro ao ler as configurações: ", "Could not read settings: ") + ex.Message; }
    }

    private async void ExportAll(object? sender, RoutedEventArgs e)
    {
        try
        {
            var count = configService.ExportAll(libraryRoot, modService.ScanMods(modsRoot));
            LoadResults();
            await NoticeAsync(T("Backup concluído", "Backup complete"), T($"{count} config.json salvo(s) em:\n{configService.GetStorageRoot(libraryRoot)}",
                $"{count} config.json file(s) saved in:\n{configService.GetStorageRoot(libraryRoot)}"));
        }
        catch (Exception ex) { await NoticeAsync(T("Não foi possível exportar", "Could not export"), ex.Message); }
    }

    private async void RestoreSelected(object? sender, RoutedEventArgs e)
    {
        var selected = results.Where(x => x.Selected && x.State == ConfigState.ReadyToRestore).ToList();
        var restored = 0;
        foreach (var result in selected)
        {
            var answer = await ConfirmAsync(T("Confirmar restauração", "Confirm restore"), T($"Restaurar a configuração salva de:\n{result.Mod.DisplayName}?\n\nA configuração atual será guardada em um backup datado antes da substituição.",
                $"Restore saved settings for:\n{result.Mod.DisplayName}?\n\nThe current settings will be backed up before replacement."));
            if (answer is null) break;
            if (answer != true) continue;
            try { configService.Restore(libraryRoot, result); restored++; }
            catch (Exception ex) { await NoticeAsync(T("Não foi possível restaurar ", "Could not restore ") + result.Mod.DisplayName, ex.Message); }
        }
        LoadResults();
        if (restored > 0) await NoticeAsync(T("Concluído", "Done"), T($"{restored} configuração(ões) restaurada(s).", $"{restored} settings restored."));
    }

    private Task NoticeAsync(string title, string message) => ConfirmAsync(title, message, false);

    private async Task<bool?> ConfirmAsync(string title, string message, bool showChoices = true)
    {
        var dialog = new Window { Title = title, Width = 540, MinHeight = 210, SizeToContent = SizeToContent.Height, CanResize = false, Background = Brush("#0A1F35"), WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(22), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White, FontSize = 15 });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        if (showChoices)
        {
            var yes = Button(T("Restaurar", "Restore"), "#286A58"); yes.Click += (_, _) => dialog.Close(true);
            var no = Button(T("Pular", "Skip"), "#685BA6"); no.Click += (_, _) => dialog.Close(false);
            var cancel = Button(T("Parar", "Stop"), "#7A4147"); cancel.Click += (_, _) => dialog.Close(null);
            buttons.Children.Add(cancel); buttons.Children.Add(no); buttons.Children.Add(yes);
        }
        else
        {
            var ok = Button("OK", "#1A617D"); ok.Click += (_, _) => dialog.Close(true); buttons.Children.Add(ok);
        }
        panel.Children.Add(buttons); dialog.Content = panel;
        return await dialog.ShowDialog<bool?>(this);
    }

    private static Button Button(string text, string color) => new() { Content = text, Background = Brush(color), Foreground = Brushes.White, BorderBrush = Brush("#6CE0E6"), Padding = new Thickness(17, 8), FontWeight = FontWeight.SemiBold };
    private static IBrush Brush(string hex) => SolidColorBrush.Parse(hex);

    private sealed class Row(ConfigResult result)
    {
        public bool Selected { get => result.Selected; set => result.Selected = value; }
        public string Name => result.Mod.DisplayName;
        public string UniqueId => result.Mod.UniqueId;
        public string Detail => result.Detail;
        public string Status => result.State switch
        {
            ConfigState.ReadyToRestore => T("Confirmar restauração", "Confirm restore"),
            ConfigState.SameAsSaved => T("Já está salva", "Already saved"),
            ConfigState.NoSavedCopy => T("Sem cópia salva", "No saved copy"),
            _ => T("Falhou", "Failed")
        };
    }
}
