using System.Diagnostics;
using System.Drawing.Text;
using System.Text.Json;

namespace TradutorModsStardew;

internal sealed class MainForm : Form
{
    private static string T(string pt, string en) => UiLanguage.Text(pt, en);
    private readonly TranslationService service = new();
    private readonly PrivateFontCollection titleFonts = new();
    private readonly TextBox libraryBox = StyledTextBox();
    private readonly TextBox modsBox = StyledTextBox();
    private readonly TextBox searchBox = StyledTextBox(T("Digite o nome do mod, tradução ou UniqueID…", "Search for a mod, translation, or UniqueID…"));
    private readonly DataGridView grid = new();
    private readonly Label summary = new() { AutoSize = true };
    private readonly GlowButton applyButton = new() { Enabled = false, AccentColor = Color.FromArgb(161, 232, 74) };
    private readonly GlowButton undoButton = new() { AccentColor = Color.FromArgb(255, 198, 82) };
    private readonly GlowButton analyzeButton = new() { AccentColor = Color.FromArgb(108, 224, 230) };
    private readonly GlowButton prepareButton = new() { AccentColor = Color.FromArgb(130, 114, 210) };
    private readonly GlowButton configsButton = new() { AccentColor = Color.FromArgb(130, 114, 210) };
    private readonly Label help = new();
    private readonly Label libraryLabel = new();
    private readonly Label modsLabel = new();
    private readonly Label searchLabel = new();
    private readonly GlowButton libraryBrowse = new() { AccentColor = Color.FromArgb(108, 224, 230), Margin = new Padding(0, 3, 0, 3) };
    private readonly GlowButton modsBrowse = new() { AccentColor = Color.FromArgb(108, 224, 230), Margin = new Padding(0, 3, 0, 3) };
    private readonly ComboBox languagePicker = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly ComboBox localePicker = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly Label interfaceLanguageLabel = new() { AutoSize = true, ForeColor = Color.FromArgb(174, 223, 231) };
    private readonly Label translationLanguageLabel = new() { AutoSize = true, ForeColor = Color.FromArgb(174, 223, 231) };
    private readonly ToolTip toolTips = new() { AutoPopDelay = 12000, InitialDelay = 450, ReshowDelay = 150 };
    private List<InstalledMod> mods = [];
    private List<MatchResult> results = [];
    private readonly HashSet<string> expandedPackages = new(StringComparer.OrdinalIgnoreCase);
    private bool populatingGrid;
    private List<ApplyRecord> lastApply = [];
    private string? lastBackup;

    public MainForm()
    {
        Text = "Valley Modkeeper";
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(iconPath))
        {
            try { Icon = new Icon(iconPath); }
            catch { }
        }
        MinimumSize = new Size(960, 620);
        Size = new Size(1180, 740);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(10, 31, 53);

        var title = new Label
        {
            Text = "Valley Modkeeper",
            Font = LoadTitleFont(),
            AutoSize = true,
            ForeColor = Color.FromArgb(161, 232, 74),
            Margin = new Padding(0, 0, 0, 4)
        };
        help.AutoSize = true;
        help.ForeColor = Color.FromArgb(174, 223, 231);
        help.Margin = new Padding(0, 0, 0, 16);

        var paths = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, RowCount = 3, BackColor = Color.FromArgb(19, 58, 83), Padding = new Padding(14, 8, 14, 8) };
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        AddPathRow(paths, 0, libraryLabel, libraryBox, libraryBrowse, () => Browse(libraryBox));
        AddPathRow(paths, 1, modsLabel, modsBox, modsBrowse, () => Browse(modsBox));
        searchLabel.ForeColor = Color.FromArgb(194, 239, 235);
        searchLabel.Anchor = AnchorStyles.Left;
        searchLabel.AutoSize = true;
        searchLabel.Margin = new Padding(0, 9, 8, 5);
        paths.Controls.Add(searchLabel, 0, 2);
        searchBox.Margin = new Padding(0, 5, 8, 5);
        paths.Controls.Add(searchBox, 1, 2);
        libraryBox.Text = @"D:\Traduções PT-BR Stardew Valley";
        modsBox.Text = GuessModsPath() ?? "";

        ConfigureGrid();
        analyzeButton.Click += (_, _) => Analyze();
        prepareButton.Click += (_, _) => SearchNewMods();
        applyButton.Click += (_, _) => ApplySelected();
        undoButton.Click += (_, _) => UndoLast();
        configsButton.Click += (_, _) => OpenConfigBackup();
        searchBox.TextChanged += (_, _) => PopulateGrid();

        var actions = new FlowLayoutPanel
        {
            AutoSize = false,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0)
        };
        actions.Controls.Add(analyzeButton);
        actions.Controls.Add(prepareButton);
        actions.Controls.Add(configsButton);
        actions.Controls.Add(applyButton);
        actions.Controls.Add(undoButton);
        Control? disabledTipTarget = null;
        actions.MouseMove += (_, e) =>
        {
            var target = actions.GetChildAtPoint(e.Location);
            if (target is not null && !target.Enabled && target != disabledTipTarget)
            {
                disabledTipTarget = target;
                toolTips.Show(toolTips.GetToolTip(target), actions, e.Location.X + 12, e.Location.Y - 28, 5000);
            }
            else if (target is null)
            {
                disabledTipTarget = null;
            }
        };
        actions.MouseLeave += (_, _) => { disabledTipTarget = null; toolTips.Hide(actions); };
        summary.ForeColor = Color.FromArgb(174, 223, 231);
        actions.Dock = DockStyle.Fill;
        actions.WrapContents = true;
        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 112, ColumnCount = 1, RowCount = 2, BackColor = Color.FromArgb(10, 31, 53), Padding = new Padding(0, 8, 0, 4) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footer.Controls.Add(summary, 0, 0);
        footer.Controls.Add(actions, 0, 1);

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22), BackColor = Color.FromArgb(10, 31, 53) };
        var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 82, ColumnCount = 2, RowCount = 2, BackColor = Color.FromArgb(10, 31, 53) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(help, 0, 1);
        languagePicker.Items.AddRange(["English", "Português (Brasil)"]);
        languagePicker.SelectedIndex = UiLanguage.Portuguese ? 1 : 0;
        languagePicker.SelectedIndexChanged += (_, _) =>
        {
            try { UiLanguage.Set(languagePicker.SelectedIndex == 1); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, T("Não foi possível salvar o idioma", "Could not save language"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                languagePicker.SelectedIndex = UiLanguage.Portuguese ? 1 : 0;
                return;
            }
            ApplyLanguage();
            // Rebuild analysis details while keeping the user's current row selections.
            if (results.Count > 0)
            {
                var selectedKeys = results.Where(result => result.Selected).Select(result => MappingKey(result.Translation)).ToHashSet();
                Analyze();
                foreach (var result in results.Where(result => result.State == MatchState.Safe))
                    result.Selected = selectedKeys.Contains(MappingKey(result.Translation));
                PopulateGrid();
                UpdateSummary();
            }
            else UpdateSummary();
        };
        var interfaceRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        interfaceRow.Controls.Add(interfaceLanguageLabel);
        interfaceRow.Controls.Add(languagePicker);
        header.Controls.Add(interfaceRow, 1, 0);
        RefreshLocales();
        var translationRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        translationRow.Controls.Add(translationLanguageLabel);
        translationRow.Controls.Add(localePicker);
        header.Controls.Add(translationRow, 1, 1);
        body.Controls.Add(grid);
        body.Controls.Add(footer);
        body.Controls.Add(paths);
        body.Controls.Add(header);
        Controls.Add(body);
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        help.Text = T("Transfira traduções com segurança e preserve configurações dos mods para futuras atualizações.", "Safely transfer translations and save mod settings before future updates.");
        interfaceLanguageLabel.Text = T("Interface:", "Interface:");
        translationLanguageLabel.Text = T("Idioma da tradução:", "Translation language:");
        libraryLabel.Text = T("Biblioteca de traduções:", "Translation library:");
        modsLabel.Text = T("Pasta Mods do Stardew:", "Stardew Mods folder:");
        searchLabel.Text = T("Pesquisar na lista:", "Search list:");
        searchBox.PlaceholderText = T("Digite o nome do mod, tradução ou UniqueID…", "Search for a mod, translation, or UniqueID…");
        libraryBrowse.Text = modsBrowse.Text = T("Escolher…", "Browse…");
        analyzeButton.Text = T("Analisar", "Analyze");
        prepareButton.Text = T("Procurar novos mods", "Find new mods");
        configsButton.Text = T("Backup de configurações", "Settings backup");
        applyButton.Text = T("Aplicar selecionadas", "Apply selected");
        undoButton.Text = T("Desfazer última aplicação", "Undo last application");
        grid.Columns["Use"]!.HeaderText = T("Aplicar", "Apply");
        grid.Columns["Translation"]!.HeaderText = T("Tradução", "Translation");
        grid.Columns["Target"]!.HeaderText = T("Mod encontrado", "Matched mod");
        grid.Columns["Status"]!.HeaderText = T("Situação", "Status");
        grid.Columns["Detail"]!.HeaderText = T("Detalhes", "Details");
        summary.Text = T("Escolha as duas pastas para começar.", "Choose both folders to get started.");
        toolTips.SetToolTip(analyzeButton, T("Procura traduções na biblioteca e compara com os mods instalados.", "Finds library translations and matches them to installed mods."));
        toolTips.SetToolTip(prepareButton, T("Procura novos mods, guarda referências e copia traduções do idioma escolhido que ainda não estão na biblioteca.", "Finds new mods, saves references, and copies selected-language translations not yet in the library."));
        toolTips.SetToolTip(configsButton, T("Salva e restaura arquivos config.json dos mods.", "Back up and restore mod config.json files."));
        toolTips.SetToolTip(applyButton, T("Aplica aos mods as traduções marcadas para o idioma selecionado.", "Applies checked translations for the selected language to the mods."));
        toolTips.SetToolTip(undoButton, T("Desfaz os arquivos aplicados na última operação desta sessão.", "Undoes files applied in the last operation of this session."));
        toolTips.SetToolTip(libraryBrowse, T("Escolha onde sua biblioteca de traduções fica guardada.", "Choose where your translation library is stored."));
        toolTips.SetToolTip(modsBrowse, T("Escolha a pasta Mods da instalação do Stardew Valley.", "Choose your Stardew Valley installation's Mods folder."));
        toolTips.SetToolTip(languagePicker, T("Idioma da interface do programa.", "Program interface language."));
        toolTips.SetToolTip(localePicker, T("Idioma das traduções exibidas e aplicadas; um de cada vez.", "Language of the translations shown and applied, one at a time."));
        toolTips.SetToolTip(searchBox, T("Filtra a lista por nome do mod, tradução ou UniqueID.", "Filters the list by mod name, translation, or UniqueID."));
        toolTips.SetToolTip(grid, T("Clique duas vezes para confirmar um destino incerto ou procurar um arquivo de tradução ausente.", "Double-click to confirm an uncertain target or browse for a missing translation file."));
        PopulateGrid();
    }

    private Font LoadTitleFont()
    {
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "PixelifySans.ttf");
        try
        {
            if (File.Exists(fontPath))
            {
                titleFonts.AddFontFile(fontPath);
                return new Font(titleFonts.Families[0], 23, FontStyle.Bold);
            }
        }
        catch (Exception)
        {
            // Keep the title readable if the optional font cannot be loaded.
        }

        return new Font("Segoe UI Semibold", 18);
    }

    private void ConfigureGrid()
    {
        grid.Dock = DockStyle.Fill;
        grid.Margin = new Padding(0, 14, 0, 0);
        grid.BackgroundColor = Color.FromArgb(16, 49, 72);
        grid.BorderStyle = BorderStyle.None;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.AutoGenerateColumns = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
        grid.ColumnHeadersHeight = 38;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(26, 97, 125), ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 9.5f), Padding = new Padding(5, 2, 5, 2), Alignment = DataGridViewContentAlignment.MiddleLeft };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(19, 58, 83), ForeColor = Color.FromArgb(231, 249, 247), SelectionBackColor = Color.FromArgb(31, 112, 132), SelectionForeColor = Color.White, Padding = new Padding(4, 3, 4, 3) };
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(16, 49, 72), ForeColor = Color.FromArgb(231, 249, 247) };
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Use", HeaderText = "Aplicar", Width = 65 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Translation", HeaderText = "Tradução", Width = 260, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Target", HeaderText = "Mod encontrado", Width = 270, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Situação", Width = 170, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Detail", HeaderText = "Detalhes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        grid.CellValueChanged += GridCellValueChanged;
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.CellDoubleClick += GridCellDoubleClick;
    }

    private static TextBox StyledTextBox(string placeholder = "") => new()
    {
        Dock = DockStyle.Fill,
        PlaceholderText = placeholder,
        BackColor = Color.FromArgb(12, 41, 61),
        ForeColor = Color.FromArgb(231, 249, 247),
        BorderStyle = BorderStyle.FixedSingle
    };

    private static void AddPathRow(TableLayoutPanel panel, int row, Label label, TextBox box, GlowButton button, Action browse)
    {
        label.ForeColor = Color.FromArgb(194, 239, 235);
        label.Anchor = AnchorStyles.Left;
        label.AutoSize = true;
        label.Margin = new Padding(0, 9, 8, 5);
        panel.Controls.Add(label, 0, row);
        box.Margin = new Padding(0, 5, 8, 5);
        panel.Controls.Add(box, 1, row);
        button.Click += (_, _) => browse();
        panel.Controls.Add(button, 2, row);
    }

    private void Browse(TextBox target)
    {
        using var dialog = new FolderBrowserDialog { ShowNewFolderButton = false, InitialDirectory = Directory.Exists(target.Text) ? target.Text : "" };
        if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.SelectedPath;
    }

    private void Analyze()
    {
        if (!Directory.Exists(libraryBox.Text) || !Directory.Exists(modsBox.Text))
        {
            MessageBox.Show(this, T("Escolha pastas existentes para a biblioteca e para os mods.", "Choose existing translation-library and Mods folders."), T("Pastas necessárias", "Folders required"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            UseWaitCursor = true;
            mods = service.ScanMods(modsBox.Text);
            var translations = service.ScanLibrary(libraryBox.Text);
            results = service.Match(translations, mods);
            LoadSavedMappings();
            RefreshLocales();
            RefreshMissingTranslations();
            PopulateGrid();
            UpdateSummary();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, T("Não foi possível concluir a análise:\n\n", "Could not complete the analysis:\n\n") + ex.Message, T("Erro", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private void PopulateGrid(string? focusPackage = null)
    {
        var oldTop = -1;
        object? topTag = null;
        var focusOffset = 0;
        if (grid.Rows.Count > 0)
        {
            try { oldTop = grid.FirstDisplayedScrollingRowIndex; } catch (InvalidOperationException) { }
            if (oldTop >= 0 && oldTop < grid.Rows.Count)
            {
                topTag = grid.Rows[oldTop].Tag;
                if (focusPackage is not null)
                {
                    var oldFocus = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(row =>
                        row.Tag is PackageGroup package && package.Name.Equals(focusPackage, StringComparison.OrdinalIgnoreCase));
                    if (oldFocus is not null) focusOffset = oldFocus.Index - oldTop;
                }
            }
        }
        populatingGrid = true;
        try
        {
        grid.Rows.Clear();
        foreach (var group in FilteredResults().GroupBy(result => result.Translation.LibraryPackage, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Min(result => StateOrder(result.State)))
            .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var members = group.OrderBy(result => result.Translation.RelativeComponentPath, StringComparer.CurrentCultureIgnoreCase).ToList();
            if (members.Count == 1)
            {
                AddResultRow(members[0], false);
                continue;
            }
            var safe = members.Where(result => result.State == MatchState.Safe).ToList();
            var selected = safe.Count(result => result.Selected);
            var expanded = expandedPackages.Contains(group.Key);
            var parentIndex = grid.Rows.Add(safe.Count > 0 && selected == safe.Count,
                (expanded ? "▼ " : "▶ ") + group.Key,
                string.Format(T("{0} componentes", "{0} components"), members.Count),
                string.Format(T("{0} prontas", "{0} ready"), safe.Count),
                string.Format(T("{0} selecionadas • {1} a confirmar", "{0} selected • {1} to review"),
                    selected, members.Count(result => result.State == MatchState.NeedsConfirmation)));
            var parent = grid.Rows[parentIndex];
            parent.Tag = new PackageGroup(group.Key, members);
            parent.Cells["Use"].ReadOnly = safe.Count == 0;
            parent.DefaultCellStyle.BackColor = Color.FromArgb(28, 83, 105);
            parent.DefaultCellStyle.Font = new Font(grid.Font, FontStyle.Bold);
            if (expanded)
                foreach (var result in members) AddResultRow(result, true);
        }
        }
        finally
        {
            populatingGrid = false;
            if (grid.Rows.Count > 0 && oldTop >= 0)
            {
                var focusRow = focusPackage is null ? null : grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(row =>
                    row.Tag is PackageGroup package && package.Name.Equals(focusPackage, StringComparison.OrdinalIgnoreCase));
                var matchingTop = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(row =>
                    ReferenceEquals(row.Tag, topTag) || row.Tag is PackageGroup current &&
                    topTag is PackageGroup previous && current.Name.Equals(previous.Name, StringComparison.OrdinalIgnoreCase));
                var restoreIndex = focusRow is not null ? focusRow.Index - focusOffset : matchingTop?.Index ?? oldTop;
                try { grid.FirstDisplayedScrollingRowIndex = Math.Clamp(restoreIndex, 0, grid.Rows.Count - 1); }
                catch (InvalidOperationException) { }
            }
        }
    }

    private void AddResultRow(MatchResult result, bool child)
    {
        var translationName = result.State == MatchState.MissingTranslationFile
            ? result.Target?.DisplayName ?? "—" : DisplayTranslation(result.Translation);
        if (child) translationName = "    ↳ " + result.Translation.ComponentName;
        var rowIndex = grid.Rows.Add(result.Selected, translationName, DisplayTarget(result.Target), StatusText(result.State), result.Detail);
        var row = grid.Rows[rowIndex];
        row.Tag = result;
        row.Cells["Use"].ReadOnly = result.State != MatchState.Safe;
        row.Cells["Translation"].ToolTipText = result.Translation.SourcePath;
        row.Cells["Target"].ToolTipText = result.Target?.Directory ?? "";
        row.DefaultCellStyle.BackColor = result.State switch
        {
            MatchState.Safe => Color.FromArgb(23, 92, 85),
            MatchState.NeedsConfirmation => Color.FromArgb(94, 78, 38),
            MatchState.AlreadyTranslated => Color.FromArgb(25, 68, 91),
            MatchState.NoMatch => Color.FromArgb(91, 50, 65),
            MatchState.MissingTranslationFile => Color.FromArgb(88, 53, 74),
            MatchState.Applied => Color.FromArgb(25, 104, 82),
            MatchState.Failed => Color.FromArgb(112, 49, 58),
            _ => Color.FromArgb(19, 58, 83)
        };
    }

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

    private sealed record PackageGroup(string Name, List<MatchResult> Members);

    private string SelectedLocale => localePicker.SelectedItem as string ?? UiLanguage.TranslationLocale;

    private void RefreshLocales()
    {
        var previous = SelectedLocale;
        var commonLocales = new[] { "pt-BR", "en", "es", "fr", "de", "it", "ja", "ko", "zh-Hans", "zh-Hant", "ru", "tr", "pl", "cs" };
        var locales = commonLocales.Append(previous).Concat(results.Select(result => result.Translation.Locale)).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase).ToList();
        localePicker.SelectedIndexChanged -= LocaleChanged;
        localePicker.Items.Clear();
        localePicker.Items.AddRange(locales.Cast<object>().ToArray());
        localePicker.SelectedItem = locales.FirstOrDefault(code => code.Equals(previous, StringComparison.OrdinalIgnoreCase)) ?? locales[0];
        localePicker.SelectedIndexChanged += LocaleChanged;
        foreach (var result in results)
            if (result.Translation.Locale != SelectedLocale) result.Selected = false;
    }

    private void LocaleChanged(object? sender, EventArgs e)
    {
        try { UiLanguage.SetTranslationLocale(SelectedLocale); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("Não foi possível salvar o idioma", "Could not save language"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        foreach (var result in results)
            if (result.State == MatchState.Safe && result.Translation.Locale != SelectedLocale)
                result.Selected = false;
        RefreshMissingTranslations();
        PopulateGrid();
        UpdateSummary();
    }

    private void RefreshMissingTranslations()
    {
        results.RemoveAll(result => result.State == MatchState.MissingTranslationFile);
        if (mods.Count > 0)
            results.AddRange(service.FindMissingTranslationFiles(mods, results, SelectedLocale));
    }

    private void GridCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (grid.Rows[e.RowIndex].Tag is PackageGroup package)
        {
            if (!expandedPackages.Add(package.Name)) expandedPackages.Remove(package.Name);
            PopulateGrid(package.Name);
            return;
        }
        if (grid.Rows[e.RowIndex].Tag is not MatchResult result) return;
        if (result.State == MatchState.MissingTranslationFile && result.Target is { } missingMod)
        {
            ImportTranslationForMod(missingMod);
            return;
        }
        using var picker = new TargetPickerForm(mods, result.Target, result.Translation);
        if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedMod is null) return;
        result.Target = picker.SelectedMod;
        if (service.HasTranslation(result.Target, result.Translation))
        {
            result.State = MatchState.AlreadyTranslated;
            result.Selected = false;
            result.Detail = T("O mod escolhido já possui tradução neste idioma.", "The selected mod already has a translation in this language.");
        }
        else
        {
            result.State = MatchState.Safe;
            result.Selected = true;
            result.Detail = T("Destino confirmado manualmente.", "Target confirmed manually.");
            SaveMapping(result);
        }
        RefreshMissingTranslations(); PopulateGrid(); UpdateSummary();
    }

    private void GridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (populatingGrid) return;
        var useColumn = grid.Columns["Use"];
        if (e.RowIndex < 0 || useColumn is null || e.ColumnIndex != useColumn.Index) return;
        if (grid.Rows[e.RowIndex].Tag is PackageGroup package)
        {
            var selected = Convert.ToBoolean(grid.Rows[e.RowIndex].Cells["Use"].Value);
            foreach (var member in package.Members.Where(member => member.State == MatchState.Safe))
                member.Selected = selected;
            PopulateGrid();
        }
        else if (grid.Rows[e.RowIndex].Tag is MatchResult result)
        {
            result.Selected = Convert.ToBoolean(grid.Rows[e.RowIndex].Cells["Use"].Value);
            if (expandedPackages.Contains(result.Translation.LibraryPackage)) PopulateGrid();
        }
        UpdateSummary();
    }

    private void ApplySelected()
    {
        var selected = results.Where(p => p.Translation.Locale == SelectedLocale && p.Selected && p.Target is not null && p.State == MatchState.Safe).ToList();
        if (selected.Count == 0) return;
        var answer = MessageBox.Show(this, string.Format(T("Aplicar {0} tradução(ões) em {1}?\n\nTraduções existentes serão preservadas.", "Apply {0} translation(s) in {1}?\n\nExisting translations will be preserved."), selected.Count, SelectedLocale), T("Confirmar aplicação", "Confirm application"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;
        lastBackup = Path.Combine(AppDataDirectory(), "Backups", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        lastApply = [];
        foreach (var result in selected)
        {
            try
            {
                lastApply.Add(service.Apply(result, lastBackup));
                result.State = MatchState.Applied; result.Selected = false; result.Detail = T("Tradução aplicada com sucesso.", "Translation applied successfully.");
            }
            catch (Exception ex)
            {
                result.State = MatchState.Failed; result.Selected = false; result.Detail = ex.Message;
            }
        }
        WriteReport();
        undoButton.Enabled = lastApply.Count > 0;
        PopulateGrid(); UpdateSummary();
        MessageBox.Show(this, string.Format(T("Processo concluído. {0} tradução(ões) aplicada(s).", "Done. {0} translation(s) applied."), lastApply.Count), T("Concluído", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UndoLast()
    {
        if (lastApply.Count == 0) return;
        if (MessageBox.Show(this, T("Desfazer os arquivos aplicados na última operação?", "Undo the files applied in the last operation?"), T("Confirmar", "Confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            service.Undo(lastApply);
            lastApply = []; undoButton.Enabled = false;
            Analyze();
            MessageBox.Show(this, T("A última aplicação foi desfeita.", "The last application was undone."), T("Concluído", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, T("Não foi possível desfazer", "Could not undo"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void OpenConfigBackup()
    {
        if (!Directory.Exists(libraryBox.Text) || !Directory.Exists(modsBox.Text))
        {
            MessageBox.Show(this, T("Escolha primeiro a biblioteca de traduções e a pasta Mods.", "Choose the translation library and Mods folder first."), T("Pastas necessárias", "Folders required"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new ConfigBackupForm(libraryBox.Text, modsBox.Text, service);
        dialog.ShowDialog(this);
    }

    private void SearchNewMods()
    {
        if (!Directory.Exists(libraryBox.Text) || !Directory.Exists(modsBox.Text))
        {
            MessageBox.Show(this, T("Escolha primeiro a biblioteca de traduções e a pasta Mods.", "Choose the translation library and Mods folder first."), T("Pastas necessárias", "Folders required"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;
            var installed = service.ScanMods(modsBox.Text);
            var report = service.PrepareLibrary(libraryBox.Text, installed);
            var imported = service.ImportInstalledTranslations(libraryBox.Text, installed, SelectedLocale);
            Analyze();
            MessageBox.Show(this,
                string.Format(T("Busca concluída para {0}:\n\n• {1} pasta(s) nova(s) na biblioteca\n• {2} referência(s) i18n/default copiadas\n• {3} content.json copiado(s)\n• {4} tradução(ões) do idioma copiadas\n• {5} arquivo(s) inválido(s) ignorado(s)\n\nOs mods instalados não foram alterados.",
                    "Search finished for {0}:\n\n• {1} new library folder(s)\n• {2} i18n/default reference(s) copied\n• {3} content.json file(s) copied\n• {4} selected-language translation(s) copied\n• {5} invalid file(s) skipped\n\nInstalled mods were not changed."),
                    SelectedLocale, report.CreatedFolders, report.CopiedI18nReferences, report.CopiedContentReferences, imported.Imported, imported.Invalid),
                T("Busca concluída", "Search complete"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, T("Não foi possível procurar novos mods:\n\n", "Could not search for new mods:\n\n") + ex.Message, T("Erro", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private void ImportTranslationForMod(InstalledMod mod)
    {
        if (!Directory.Exists(libraryBox.Text) || !Directory.Exists(modsBox.Text))
        {
            MessageBox.Show(this, T("Escolha primeiro a biblioteca de traduções e a pasta Mods.", "Choose the translation library and Mods folder first."), T("Pastas necessárias", "Folders required"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var initialFolder = Path.Combine(mod.Directory, "i18n");
            var folderStyle = Directory.Exists(initialFolder) && Directory.EnumerateDirectories(initialFolder)
                .Any(path => TranslationLocales.SameTargetLanguage(Path.GetFileName(path), SelectedLocale));
            var chooseFolder = MessageBox.Show(this,
                T("Como está guardada a tradução?\n\nSim: pasta do idioma (por exemplo, i18n/pt/).\nNão: arquivo do idioma (por exemplo, i18n/pt.json).\nCancelar: voltar à lista.",
                  "How is the translation stored?\n\nYes: language folder (for example, i18n/pt/).\nNo: language file (for example, i18n/pt.json).\nCancel: return to the list."),
                T("Escolher formato da tradução", "Choose translation format"), MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question, folderStyle ? MessageBoxDefaultButton.Button1 : MessageBoxDefaultButton.Button2);
            if (chooseFolder == DialogResult.Cancel) return;
            if (chooseFolder == DialogResult.Yes)
            {
                using var folderDialog = new FolderBrowserDialog
                {
                    Description = string.Format(T("Escolha a pasta de idioma de {0}", "Choose {0}'s language folder"), mod.DisplayName),
                    InitialDirectory = Directory.Exists(initialFolder) ? initialFolder : mod.Directory,
                    ShowNewFolderButton = false
                };
                if (folderDialog.ShowDialog(this) != DialogResult.OK) return;
                var sourceFolder = folderDialog.SelectedPath;
                var detectedFolder = TranslationLocales.Normalize(Path.GetFileName(sourceFolder));
                if (detectedFolder is not null && !TranslationLocales.SameTargetLanguage(detectedFolder, SelectedLocale))
                {
                    MessageBox.Show(this, string.Format(T("A pasta indica {0}, mas você selecionou {1}. Nada foi copiado.",
                        "The folder indicates {0}, but you selected {1}. Nothing was copied."), detectedFolder, SelectedLocale),
                        T("Idioma diferente", "Different language"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (detectedFolder is null && MessageBox.Show(this,
                    string.Format(T("O nome da pasta não identifica o idioma. Confirmar que ela contém a tradução em {0}?",
                        "The folder name does not identify a language. Confirm that it contains the translation in {0}?"), SelectedLocale),
                    T("Confirmar idioma", "Confirm language"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                var folderLocale = detectedFolder ?? SelectedLocale;
                if (MessageBox.Show(this,
                    string.Format(T("Guardar a pasta inteira na biblioteca?\n\nOrigem: {0}\nIdioma: {1}\n\nA pasta original não será alterada.",
                        "Save the whole folder in the library?\n\nSource: {0}\nLanguage: {1}\n\nThe original folder will not be changed."), sourceFolder, folderLocale),
                    T("Confirmar importação", "Confirm import"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                service.ImportI18nDirectory(libraryBox.Text, mod, sourceFolder, folderLocale);
                Analyze();
                MessageBox.Show(this, T("A pasta inteira foi salva na biblioteca. A original não foi alterada.",
                    "The whole folder was saved in the library. The original was not changed."),
                    T("Concluído", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var fileDialog = new OpenFileDialog
            {
                Title = string.Format(T("Escolha a tradução de {0} para {1}", "Choose {0}'s translation for {1}"), mod.DisplayName, SelectedLocale),
                InitialDirectory = Directory.Exists(initialFolder) ? initialFolder : mod.Directory,
                Filter = T("Arquivos JSON (*.json)|*.json|Todos os arquivos (*.*)|*.*", "JSON files (*.json)|*.json|All files (*.*)|*.*"),
                CheckFileExists = true
            };
            if (fileDialog.ShowDialog(this) != DialogResult.OK) return;
            var sourcePath = fileDialog.FileName;
            if (Path.GetFileName(sourcePath).StartsWith("content.", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, T("Arquivos content.<idioma>.json usam um formato especial. Esta importação manual é para arquivos i18n; não alterei nada.", "content.<language>.json files use a special format. Manual import is for i18n files; nothing was changed."), T("Formato diferente", "Different format"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var detected = TranslationLocales.FromFileName(sourcePath);
            if (detected is not null && !TranslationLocales.SameTargetLanguage(detected, SelectedLocale))
            {
                MessageBox.Show(this,
                    string.Format(T("O nome do arquivo indica {0}, mas você selecionou {1}. Escolha o idioma correto no topo ou procure outro arquivo; nada foi copiado.",
                        "The file name indicates {0}, but you selected {1}. Choose the correct language at the top or browse for another file; nothing was copied."), detected, SelectedLocale),
                    T("Idioma diferente", "Different language"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (detected is null && MessageBox.Show(this,
                string.Format(T("O nome do arquivo não identifica o idioma. Confirmar que este arquivo é uma tradução em {0}?", "The file name does not identify a language. Confirm that this is a translation in {0}?"), SelectedLocale),
                T("Confirmar idioma", "Confirm language"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var locale = detected ?? SelectedLocale;
            var destination = service.GetImportDestination(libraryBox.Text, mod, locale);
            var exists = File.Exists(destination);
            var prompt = string.Format(T("Guardar esta tradução na biblioteca?\n\nOrigem: {0}\nIdioma: {1}\nDestino: {2}{3}",
                "Save this translation in the library?\n\nSource: {0}\nLanguage: {1}\nDestination: {2}{3}"),
                sourcePath, locale, destination,
                exists ? T("\n\nJá existe uma cópia. Se substituir, a versão atual será guardada em backup.", "\n\nA copy already exists. If replaced, the current version will be backed up.") : "");
            if (MessageBox.Show(this, prompt, exists ? T("Confirmar substituição", "Confirm replacement") : T("Confirmar importação", "Confirm import"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            service.ImportI18nFile(libraryBox.Text, mod, sourcePath, locale, exists);
            Analyze();
            MessageBox.Show(this, T("A tradução foi salva na biblioteca. O arquivo original não foi alterado.", "The translation was saved in the library. The original file was not changed."), T("Concluído", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("Não foi possível importar", "Could not import"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateSummary()
    {
        var localeResults = results.Where(p => p.Translation.Locale == SelectedLocale).ToList();
        int safe = localeResults.Count(p => p.State == MatchState.Safe);
        int existing = localeResults.Count(p => p.State == MatchState.AlreadyTranslated);
        int review = localeResults.Count(p => p.State == MatchState.NeedsConfirmation);
        int missing = localeResults.Count(p => p.State == MatchState.NoMatch);
        int missingFiles = localeResults.Count(p => p.State == MatchState.MissingTranslationFile);
        var filterSuffix = string.IsNullOrWhiteSpace(searchBox.Text) ? "" : string.Format(T(" • {0} exibidas", " • {0} shown"), FilteredResults().Count());
        summary.Text = localeResults.Count == 0 ? T("Nenhum resultado.", "No results.") : string.Format(
            T("{0} mods • {1} prontas • {2} já traduzidas • {3} para confirmar • {4} sem correspondência • {5} sem arquivo de tradução{6}",
                "{0} mods • {1} ready • {2} translated • {3} to review • {4} unmatched • {5} missing translation files{6}"),
            mods.Count, safe, existing, review, missing, missingFiles, filterSuffix);
        applyButton.Enabled = localeResults.Any(p => p.Selected && p.State == MatchState.Safe);
    }

    private IEnumerable<MatchResult> FilteredResults()
    {
        var query = searchBox.Text.Trim();
        var localeResults = results.Where(result => result.Translation.Locale == SelectedLocale);
        if (string.IsNullOrWhiteSpace(query)) return localeResults;
        return localeResults.Where(result =>
            DisplayTranslation(result.Translation).Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            result.Target?.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true ||
            result.Target?.UniqueId.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
    }

    private void LoadSavedMappings()
    {
        var path = MappingPath();
        if (!File.Exists(path)) return;
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
            foreach (var result in results.Where(p => p.State is MatchState.NeedsConfirmation or MatchState.NoMatch))
            {
                var key = MappingKey(result.Translation);
                if (!map.TryGetValue(key, out var uniqueId)) continue;
                var target = mods.FirstOrDefault(p => p.UniqueId.Equals(uniqueId, StringComparison.OrdinalIgnoreCase));
                if (target is null) continue;
                // A saved choice must not override an installed component at the exact source path.
                var exact = mods.FirstOrDefault(p => p.RelativeFolder.Equals(result.Translation.RelativeComponentPath, StringComparison.OrdinalIgnoreCase));
                if (exact is not null && !exact.UniqueId.Equals(target.UniqueId, StringComparison.OrdinalIgnoreCase)) continue;
                result.Target = target;
                result.State = service.HasTranslation(target, result.Translation) ? MatchState.AlreadyTranslated : MatchState.Safe;
                result.Selected = result.State == MatchState.Safe;
                result.Detail = result.Selected ? T("Correspondência lembrada de uma confirmação anterior.", "Match remembered from an earlier confirmation.") : T("O mod já possui tradução neste idioma.", "The mod already has a translation in this language.");
            }
        }
        catch { }
    }

    private void SaveMapping(MatchResult result)
    {
        if (result.Target is null || string.IsNullOrWhiteSpace(result.Target.UniqueId)) return;
        Directory.CreateDirectory(AppDataDirectory());
        var path = MappingPath();
        Dictionary<string, string> map = [];
        try { if (File.Exists(path)) map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? []; } catch { }
        map[MappingKey(result.Translation)] = result.Target.UniqueId;
        File.WriteAllText(path, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void WriteReport()
    {
        var reportDir = Path.Combine(AppDataDirectory(), "Relatorios");
        Directory.CreateDirectory(reportDir);
        var lines = new List<string> { T("Valley Modkeeper — relatório", "Valley Modkeeper — report"), DateTime.Now.ToString("F"), "" };
        lines.AddRange(results.Select(r => $"[{StatusText(r.State)}] {DisplayTranslation(r.Translation)} -> {DisplayTarget(r.Target)}: {r.Detail}"));
        File.WriteAllLines(Path.Combine(reportDir, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt"), lines);
    }

    private static string DisplayTranslation(TranslationUnit unit) => unit.IsContentTextMap
        ? unit.DisplayName + T(" › textos do content.json", " › content.json text")
        : unit.DisplayName;
    private static string DisplayTarget(InstalledMod? mod) => mod is null ? "—" : $"{mod.DisplayName} › {mod.RelativeFolder}";
    private static string MappingKey(TranslationUnit unit) => unit.LibraryPackage + "|" + unit.ComponentName + "|" + unit.TargetRelativePath;
    private static string StatusText(MatchState state) => state switch
    {
        MatchState.Safe => T("Pronta", "Ready"),
        MatchState.NeedsConfirmation => T("Confirmar destino", "Confirm target"),
        MatchState.AlreadyTranslated => T("Já traduzido", "Already translated"),
        MatchState.NoMatch => T("Não encontrada", "Not found"),
        MatchState.MissingTranslationFile => T("Arquivo de tradução ausente", "Missing translation file"),
        MatchState.Applied => T("Aplicada", "Applied"),
        MatchState.Failed => T("Falhou", "Failed"),
        _ => state.ToString()
    };
    private static string AppDataDirectory() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TradutorModsStardew");
    private static string MappingPath() => Path.Combine(AppDataDirectory(), "associacoes.json");
    private static string? GuessModsPath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Stardew Valley", "Mods"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StardewValley", "Mods")
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }
}

internal sealed class TargetPickerForm : Form
{
    private readonly ListBox list = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true, HorizontalExtent = 1800 };
    public InstalledMod? SelectedMod => list.SelectedItem as InstalledMod;

    public TargetPickerForm(List<InstalledMod> mods, InstalledMod? current, TranslationUnit translation)
    {
        Text = UiLanguage.Text("Confirmar mod de destino", "Confirm target mod");
        Size = new Size(1000, 600);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10);
        var filter = new TextBox { Dock = DockStyle.Top, PlaceholderText = UiLanguage.Text("Pesquisar mod…", "Search mods…"), Margin = new Padding(8) };
        var sourcePath = translation.SourcePath;
        var label = new Label { Dock = DockStyle.Top, Height = 72,
            Text = UiLanguage.Text("Origem da tradução: ", "Translation source: ") + sourcePath + "\n" +
                UiLanguage.Text("Escolha o componente de destino pelo caminho e UniqueID. A escolha será lembrada.",
                    "Choose the target component by its path and UniqueID. Your choice will be remembered.") };
        var openSource = new Button { Text = UiLanguage.Text("Abrir origem no Explorador", "Open source in Explorer"), AutoSize = true };
        openSource.Click += (_, _) =>
        {
            if (File.Exists(sourcePath) || Directory.Exists(sourcePath))
                Process.Start(new ProcessStartInfo("explorer.exe", File.Exists(sourcePath) ? $"/select,\"{sourcePath}\"" : $"\"{sourcePath}\"") { UseShellExecute = true });
        };
        var ok = new Button { Text = UiLanguage.Text("Confirmar", "Confirm"), DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = UiLanguage.Text("Cancelar", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel); buttons.Controls.Add(openSource);
        void Fill(string query)
        {
            list.Items.Clear();
            foreach (var mod in mods.Where(p => string.IsNullOrWhiteSpace(query) || p.PickerName.Contains(query, StringComparison.CurrentCultureIgnoreCase))) list.Items.Add(mod);
            list.DisplayMember = nameof(InstalledMod.PickerName);
            if (current is not null) list.SelectedItem = current;
        }
        filter.TextChanged += (_, _) => Fill(filter.Text);
        list.DoubleClick += (_, _) => { if (SelectedMod is not null) { DialogResult = DialogResult.OK; Close(); } };
        Fill(translation.ComponentName);
        filter.Text = translation.ComponentName;
        Controls.Add(list); Controls.Add(filter); Controls.Add(label); Controls.Add(buttons);
        AcceptButton = ok; CancelButton = cancel;
    }
}
