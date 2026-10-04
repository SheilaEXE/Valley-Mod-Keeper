namespace TradutorModsStardew;

internal sealed class ConfigBackupForm : Form
{
    private static string T(string pt, string en) => UiLanguage.Text(pt, en);
    private readonly string libraryRoot;
    private readonly string modsRoot;
    private readonly TranslationService modService;
    private readonly ConfigBackupService configService = new();
    private readonly DataGridView grid = new();
    private readonly Label summary = new() { AutoSize = true };
    private readonly ToolTip tips = new() { AutoPopDelay = 12000, InitialDelay = 450, ReshowDelay = 150 };
    private List<ConfigResult> results = [];

    public ConfigBackupForm(string libraryRoot, string modsRoot, TranslationService modService)
    {
        this.libraryRoot = libraryRoot;
        this.modsRoot = modsRoot;
        this.modService = modService;
        Text = T("Backup de configurações dos mods", "Mod settings backup");
        Size = new Size(1020, 650);
        MinimumSize = new Size(800, 500);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(10, 31, 53);

        var title = new Label { Dock = DockStyle.Top, Height = 68, Padding = new Padding(18, 14, 18, 0), Text = T("Backup de configurações dos mods\nSalve seus config.json antes de atualizar. Na restauração, cada mod pede confirmação e a versão atual é guardada.", "Mod settings backup\nSave config.json files before updating. Each restore asks for confirmation and backs up the current file."), Font = new Font("Segoe UI", 11), ForeColor = Color.FromArgb(194, 239, 235) };
        ConfigureGrid();
        var export = new GlowButton { Text = T("Exportar todos os configs", "Back up all configs"), AccentColor = Color.FromArgb(108, 224, 230), AutoSize = true };
        var import = new GlowButton { Text = T("Restaurar selecionados", "Restore selected"), AccentColor = Color.FromArgb(161, 232, 74), AutoSize = true };
        var refresh = new GlowButton { Text = T("Procurar novas configs", "Find new configs"), AccentColor = Color.FromArgb(130, 114, 210), AutoSize = true };
        tips.SetToolTip(export, T("Guarda uma cópia dos config.json dos mods na biblioteca.", "Saves copies of the mods' config.json files in the library."));
        tips.SetToolTip(import, T("Restaura os configs marcados, pedindo confirmação para cada mod.", "Restores checked configs, asking for confirmation for each mod."));
        tips.SetToolTip(refresh, T("Procura config.json novos nos mods e atualiza a lista; não cria backup automaticamente.", "Finds new mod config.json files and refreshes the list; it does not create backups automatically."));
        export.Click += (_, _) => ExportAll();
        import.Click += (_, _) => RestoreSelected();
        refresh.Click += (_, _) => LoadResults();
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 68, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(18, 14, 18, 10), BackColor = BackColor };
        actions.Controls.Add(import); actions.Controls.Add(export); actions.Controls.Add(refresh);
        summary.ForeColor = Color.FromArgb(174, 223, 231);
        summary.Location = new Point(20, 24); actions.Controls.Add(summary);
        Controls.Add(grid); Controls.Add(actions); Controls.Add(title);
        LoadResults();
    }

    private void ConfigureGrid()
    {
        grid.Dock = DockStyle.Fill;
        grid.BackgroundColor = Color.FromArgb(16, 49, 72);
        grid.BorderStyle = BorderStyle.None;
        grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false; grid.AutoGenerateColumns = false;
        grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersHeight = 38;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(26, 97, 125), ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 9.5f), Padding = new Padding(5, 2, 5, 2) };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(19, 58, 83), ForeColor = Color.FromArgb(231, 249, 247), SelectionBackColor = Color.FromArgb(31, 112, 132), SelectionForeColor = Color.White, Padding = new Padding(4, 3, 4, 3) };
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Use", HeaderText = T("Restaurar", "Restore"), Width = 78 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mod", HeaderText = "Mod", Width = 280, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UniqueId", HeaderText = "UniqueID", Width = 260, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = T("Situação", "Status"), Width = 160, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Detail", HeaderText = T("Detalhes", "Details"), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "Use" && grid.Rows[e.RowIndex].Tag is ConfigResult result)
                result.Selected = Convert.ToBoolean(grid.Rows[e.RowIndex].Cells["Use"].Value);
        };
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
    }

    private void LoadResults()
    {
        try
        {
            results = configService.Compare(libraryRoot, modService.ScanMods(modsRoot));
            grid.Rows.Clear();
            foreach (var result in results)
            {
                var row = grid.Rows[grid.Rows.Add(result.Selected, result.Mod.DisplayName, result.Mod.UniqueId, StatusText(result.State), result.Detail)];
                row.Tag = result;
                row.Cells["Use"].ReadOnly = result.State != ConfigState.ReadyToRestore;
                row.DefaultCellStyle.BackColor = result.State switch
                {
                    ConfigState.ReadyToRestore => Color.FromArgb(94, 78, 38),
                    ConfigState.SameAsSaved => Color.FromArgb(25, 104, 82),
                    ConfigState.NoSavedCopy => Color.FromArgb(25, 68, 91),
                    _ => Color.FromArgb(112, 49, 58)
                };
            }
            summary.Text = string.Format(T("{0} mods encontrados • {1} configuração(ões) prontas para restaurar", "{0} mods found • {1} config(s) ready to restore"), results.Count, results.Count(x => x.State == ConfigState.ReadyToRestore));
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, T("Não foi possível ler as configurações", "Could not read settings"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ExportAll()
    {
        try
        {
            var count = configService.ExportAll(libraryRoot, modService.ScanMods(modsRoot));
            LoadResults();
            MessageBox.Show(this, string.Format(T("{0} config.json salvo(s) em:\n{1}", "{0} config.json file(s) saved to:\n{1}"), count, configService.GetStorageRoot(libraryRoot)), T("Backup concluído", "Backup complete"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, T("Não foi possível exportar", "Could not export"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void RestoreSelected()
    {
        var selected = results.Where(x => x.Selected && x.State == ConfigState.ReadyToRestore).ToList();
        if (selected.Count == 0) return;
        var restored = 0;
        foreach (var result in selected)
        {
            var answer = MessageBox.Show(this, string.Format(T("Restaurar a configuração salva de:\n{0}?\n\nA configuração atual será guardada em um backup datado antes da substituição.", "Restore the saved configuration for:\n{0}?\n\nThe current configuration will be backed up before replacement."), result.Mod.DisplayName), T("Confirmar restauração", "Confirm restore"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel) break;
            if (answer != DialogResult.Yes) continue;
            try { configService.Restore(libraryRoot, result); restored++; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, T("Não foi possível restaurar ", "Could not restore ") + result.Mod.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        LoadResults();
        if (restored > 0) MessageBox.Show(this, string.Format(T("{0} configuração(ões) restaurada(s).", "{0} configuration(s) restored."), restored), T("Concluído", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string StatusText(ConfigState state) => state switch
    {
        ConfigState.ReadyToRestore => T("Confirmar restauração", "Confirm restore"),
        ConfigState.SameAsSaved => T("Já está salva", "Already backed up"),
        ConfigState.NoSavedCopy => T("Sem cópia salva", "No backup"),
        _ => T("Falhou", "Failed")
    };
}
