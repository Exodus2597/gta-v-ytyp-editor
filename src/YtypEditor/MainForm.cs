using System.Text;
using System.Xml.Linq;

namespace YtypEditor;

public sealed class MainForm : Form
{
    private Document doc = Document.New();
    private string? path;
    private string saved = "";
    private bool binary = true;
    private readonly Stack<string> undo = new(), redo = new();
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, AccessibleName = "Props" };
    private readonly TextBox search = new() { PlaceholderText = "Search props…", Width = 260, AccessibleName = "Search props" };
    private readonly Label heading = new() { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6), ForeColor = Color.White, Font = new Font("Segoe UI", 20, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label subtitle = new() { AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty, ForeColor = Color.FromArgb(183, 199, 219), TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel count = new();
    private readonly ToolStripMenuItem undoMenu, redoMenu;

    public MainForm(string? openPath = null)
    {
        Font = new Font("Segoe UI", 10);
        Size = new Size(1250, 780);
        MinimumSize = new Size(980, 560);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = SystemIcons.Application;
        AllowDrop = true;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 53));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
        Controls.Add(root);
        var menu = new MenuStrip { Dock = DockStyle.Fill, Font = Font };
        MainMenuStrip = menu;
        root.Controls.Add(menu, 0, 0);
        var file = new ToolStripMenuItem("&File");
        menu.Items.Add(file);
        Item(file, "&New YTYP", Keys.Control | Keys.N, New);
        Item(file, "&Open…", Keys.Control | Keys.O, PickOpen);
        file.DropDownItems.Add(new ToolStripSeparator());
        Item(file, "&Save", Keys.Control | Keys.S, () => Save(false));
        Item(file, "Save &as…", Keys.Control | Keys.Shift | Keys.S, () => Save(true));
        Item(file, "Export &XML…", Keys.None, ExportXml);
        file.DropDownItems.Add(new ToolStripSeparator());
        Item(file, "E&xit", Keys.None, Close);
        var props = new ToolStripMenuItem("&Props");
        menu.Items.Add(props);
        Item(props, "&Add prop (manual)…", Keys.Control | Keys.A, () => Add());
        Item(props, "Add from &YDR…", Keys.Control | Keys.I, ImportYdr);
        Item(props, "Import &JSON…", Keys.None, ImportJson);
        Item(props, "&Edit selected…", Keys.Control | Keys.E, Edit);
        Item(props, "&Duplicate selected…", Keys.Control | Keys.D, Duplicate);
        Item(props, "&Delete selected", Keys.Delete, Delete);
        props.DropDownItems.Add(new ToolStripSeparator());
        Item(props, "Export selected as JSON…", Keys.None, ExportJson);
        var edit = new ToolStripMenuItem("&Edit");
        menu.Items.Add(edit);
        undoMenu = Item(edit, "&Undo", Keys.Control | Keys.Z, Undo);
        redoMenu = Item(edit, "&Redo", Keys.Control | Keys.Y, Redo);
        Item(edit, "Rename YTYP…", Keys.None, Rename);
        var build = new ToolStripMenuItem("&Build YTYP…", null, (_, _) => Run(Build)) { ShortcutKeys = Keys.F6 };
        menu.Items.Add(build);
        var help = new ToolStripMenuItem("&Help");
        menu.Items.Add(help);
        Item(help, "Quick guide", Keys.F1, Help);
        var banner = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, BackColor = Color.FromArgb(24, 41, 64), Padding = new Padding(24, 16, 24, 16), RowCount = 2, ColumnCount = 1 };
        banner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        banner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        banner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        banner.Controls.Add(heading, 0, 0);
        banner.Controls.Add(subtitle, 0, 1);
        root.Controls.Add(banner, 0, 1);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 8, 12, 6), BackColor = Color.FromArgb(242, 245, 249), WrapContents = false };
        Button(toolbar, "+ Add prop", () => Add());
        Button(toolbar, "Import YDR", ImportYdr);
        Button(toolbar, "Edit", Edit);
        Button(toolbar, "Delete", Delete);
        Button(toolbar, "Build YTYP", Build);
        search.Margin = new Padding(22, 3, 3, 3);
        toolbar.Controls.Add(search);
        search.TextChanged += (_, _) => RefreshRows();
        root.Controls.Add(toolbar, 0, 2);
        var area = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 12, 18, 12), BackColor = Color.White };
        area.Controls.Add(grid);
        root.Controls.Add(area, 0, 3);
        foreach (var (title, width) in new[] { ("Prop name", 205), ("LOD", 85), ("Flags", 145), ("Special attr.", 125), ("Textures*", 130), ("Texture dictionary", 190), ("Archetype type", 200) })
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, FillWeight = width, MinimumWidth = width, SortMode = DataGridViewColumnSortMode.Automatic });
        grid.Columns[4].ToolTipText = "Inferred from dictionary name. YTYP has no texture-mode flag; verify against the model.";
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(234, 239, 245), ForeColor = Color.FromArgb(43, 59, 77), Font = new Font(Font, FontStyle.Bold), Padding = new Padding(4) };
        grid.ColumnHeadersHeight = 40;
        grid.RowTemplate.Height = 36;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.DefaultCellStyle.Padding = new Padding(4);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(213, 233, 252);
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(22, 43, 66);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 251, 253);
        grid.DoubleClick += (_, _) => Run(Edit);
        var context = new ContextMenuStrip();
        context.Items.Add("Edit", null, (_, _) => Run(Edit));
        context.Items.Add("Duplicate", null, (_, _) => Run(Duplicate));
        context.Items.Add("Delete", null, (_, _) => Run(Delete));
        grid.ContextMenuStrip = context;
        var strip = new StatusStrip { Dock = DockStyle.Fill, SizingGrip = true };
        strip.Items.AddRange([status, count]);
        root.Controls.Add(strip, 0, 4);
        FormClosing += (_, e) => { if (!ConfirmLeave()) e.Cancel = true; };
        DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
        DragDrop += (_, e) => Run(() => { var files = (string[]?)e.Data?.GetData(DataFormats.FileDrop); if (files?.Length == 1) Open(files[0]); else MessageBox.Show(this, "Drop one YTYP at a time."); });
        saved = doc.Xml.ToString();
        RefreshRows();
        if (openPath is not null)
            Shown += (_, _) => Run(() => Open(openPath));
    }
    private ToolStripMenuItem Item(ToolStripMenuItem parent, string title, Keys keys, Action action)
    {
        var item = new ToolStripMenuItem(title, null, (_, _) => Run(action)) { ShortcutKeys = keys };
        parent.DropDownItems.Add(item);
        return item;
    }
    private void Button(FlowLayoutPanel panel, string text, Action action)
    {
        var b = new Button { Text = text, AutoSize = true, Height = 34, MinimumSize = new Size(76, 32), Margin = new Padding(3, 0, 3, 0) };
        b.Click += (_, _) => Run(action);
        panel.Controls.Add(b);
    }
    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) { status.Text = "Action failed — current document retained."; MessageBox.Show(this, ex.Message, "YTYP Editor", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    private bool Dirty => doc.Xml.ToString() != saved;
    private XElement[] Selection => grid.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index).Select(r => (XElement)r.Tag!).ToArray();
    private void RefreshRows(XElement? select = null)
    {
        var selected = select ?? Selection.FirstOrDefault();
        grid.Rows.Clear();
        foreach (var e in doc.Props)
        {
            var name = Document.Text(e, "name");
            var dict = Document.Text(e, "textureDictionary");
            if (search.Text.Length > 0 && !(name + " " + dict).Contains(search.Text, StringComparison.OrdinalIgnoreCase))
                continue;
            int i = grid.Rows.Add(name, e.Element("lodDist")?.Attribute("value")?.Value ?? "", e.Element("flags")?.Attribute("value")?.Value ?? "", e.Element("specialAttribute")?.Attribute("value")?.Value ?? "", PropData.TextureHint(e), dict.Length == 0 ? "(none)" : dict, (string?)e.Attribute("type") ?? "");
            grid.Rows[i].Tag = e;
        }
        grid.ClearSelection();
        if (selected is not null)
            foreach (DataGridViewRow r in grid.Rows)
                if (r.Tag == selected)
                {
                    r.Selected = true;
                    grid.CurrentCell = r.Cells[0];
                }
        heading.Text = doc.Name;
        subtitle.Text = (path is null ? "New document  ·  Add props, then build your YTYP" : path) + $"  ·  {doc.Props.Count()} props";
        Text = (Dirty ? "* " : "") + doc.Name + " — YTYP Editor";
        count.Text = $"{grid.Rows.Count} shown / {doc.Props.Count()} props";
        undoMenu.Enabled = undo.Count > 0;
        redoMenu.Enabled = redo.Count > 0;
        status.Text = Dirty ? "Unsaved changes" : "Ready  ·  Native YTYP and CodeWalker XML supported";
    }
    private void Change(Action action, XElement? select = null)
    {
        var before = doc.Xml.ToString();
        try
        {
            action();
        }
        catch { doc = new Document(Document.ParseXml(before)); RefreshRows(); throw; }
        if (doc.Xml.ToString() != before)
        {
            undo.Push(before);
            redo.Clear();
        }
        RefreshRows(select);
    }
    private bool ConfirmLeave()
    {
        if (!Dirty)
            return true;
        var choice = MessageBox.Show(this, "Save changes before continuing?", "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (choice == DialogResult.Cancel)
            return false;
        if (choice == DialogResult.No)
            return true;
        try
        {
            Save(false);
            return !Dirty;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
    }
    private void New()
    {
        if (!ConfirmLeave())
            return;
        var name = Prompt("New YTYP", "YTYP name (without extension)", "my_props");
        if (name is null)
            return;
        Document.CheckName(name, "YTYP name");
        doc = Document.New(name);
        path = null;
        binary = true;
        saved = "";
        undo.Clear();
        redo.Clear();
        search.Clear();
        RefreshRows();
    }
    private void PickOpen()
    {
        using var pick = new OpenFileDialog { Filter = "YTYP files|*.ytyp;*.ytyp.xml|XML files|*.xml|All files|*.*" };
        if (pick.ShowDialog(this) == DialogResult.OK)
            Open(pick.FileName);
    }
    private void Open(string file)
    {
        // Read completely before asking to replace the current document.
        var incoming = Document.Open(file);
        if (!ConfirmLeave())
            return;
        doc = incoming;
        path = Path.GetFullPath(file);
        binary = IsBinary(file);
        saved = doc.Xml.ToString();
        undo.Clear();
        redo.Clear();
        search.Clear();
        RefreshRows();
    }
    private static bool IsBinary(string file)
    {
        using var f = File.OpenRead(file);
        Span<byte> head = stackalloc byte[4];
        return f.Read(head) == 4 && head.SequenceEqual("RSC7"u8);
    }
    private void Add(PropData? p = null)
    {
        using var dialog = new PropDialog(imported: p);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var entry = dialog.Result!;
        doc.EnsureUnique(Document.Text(entry, "name"));
        Change(() => doc.Archetypes.Add(entry), entry);
    }
    private void Edit()
    {
        var entries = Selection;
        if (entries.Length != 1)
        {
            status.Text = "Select one prop to edit.";
            return;
        }
        if (!Document.ArchetypeTypes.Contains((string?)entries[0].Attribute("type") ?? ""))
            throw new InvalidDataException("This archetype type is preserved, but cannot be edited here.");
        using var dialog = new PropDialog(entries[0]);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var replacement = dialog.Result!;
        doc.EnsureUnique(Document.Text(replacement, "name"), entries[0]);
        Change(() => entries[0].ReplaceWith(replacement), replacement);
    }
    private void ImportYdr()
    {
        using var pick = new OpenFileDialog { Filter = "Drawable files|*.ydr;*.ydr.xml|All files|*.*" };
        if (pick.ShowDialog(this) == DialogResult.OK)
            Add(PropData.FromYdr(pick.FileName));
    }
    private void ImportJson()
    {
        using var pick = new OpenFileDialog { Filter = "Prop JSON|*.json" };
        if (pick.ShowDialog(this) != DialogResult.OK)
            return;
        var items = Document.ReadJson(File.ReadAllText(pick.FileName));
        var candidate = new Document(new XDocument(doc.Xml));
        candidate.Archetypes.Add(items.Select(e => new XElement(e)));
        candidate.Validate();
        Change(() => doc.Archetypes.Add(items), items.FirstOrDefault());
        status.Text = $"Imported {items.Length} props.";
    }
    private void ExportJson()
    {
        var items = Selection;
        if (items.Length == 0)
        {
            status.Text = "Select props to export.";
            return;
        }
        using var pick = new SaveFileDialog { Filter = "Prop JSON|*.json", FileName = items.Length == 1 ? Document.Text(items[0], "name") + ".json" : doc.Name + "-props.json" };
        if (pick.ShowDialog(this) == DialogResult.OK)
        {
            Document.AtomicWrite(pick.FileName, Encoding.UTF8.GetBytes(Document.Json(items)));
            status.Text = "Prop JSON exported.";
        }
    }
    private void Delete()
    {
        var items = Selection;
        if (items.Length == 0)
            return;
        if (MessageBox.Show(this, $"Delete {items.Length} selected prop(s)?", "Delete props", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            Change(() => { foreach (var e in items) e.Remove(); });
    }
    private void Duplicate()
    {
        var items = Selection;
        if (items.Length != 1)
        {
            status.Text = "Select one prop to duplicate.";
            return;
        }
        var data = PropData.Read(items[0]);
        var name = Prompt("Duplicate prop", "New prop name", data.Name + "_copy");
        if (name is null)
            return;
        doc.EnsureUnique(name);
        data.Name = name; // Asset and dictionary references deliberately retain the original model.
        var entry = data.ToElement(items[0]);
        Change(() => doc.Archetypes.Add(entry), entry);
    }
    private void Rename()
    {
        var name = Prompt("Rename YTYP", "YTYP name (without extension)", doc.Name);
        if (name is null)
            return;
        Document.CheckName(name, "YTYP name");
        Change(() => doc.Name = name);
    }
    private void Undo()
    {
        if (undo.Count == 0)
            return;
        redo.Push(doc.Xml.ToString());
        doc = new Document(Document.ParseXml(undo.Pop()));
        RefreshRows();
    }
    private void Redo()
    {
        if (redo.Count == 0)
            return;
        undo.Push(doc.Xml.ToString());
        doc = new Document(Document.ParseXml(redo.Pop()));
        RefreshRows();
    }
    private void Save(bool saveAs)
    {
        var target = path;
        var native = binary;
        if (saveAs || target is null)
        {
            using var pick = new SaveFileDialog { Filter = "Native game YTYP|*.ytyp|CodeWalker XML|*.ytyp.xml", FilterIndex = native ? 1 : 2, FileName = doc.Name + (native ? ".ytyp" : ".ytyp.xml"), AddExtension = true };
            if (pick.ShowDialog(this) != DialogResult.OK)
                return;
            target = pick.FileName;
            native = pick.FilterIndex == 1;
            if (native && target.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Native output needs a .ytyp filename. Choose XML export for .xml.");
            if (!native && !target.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("XML output needs a .xml filename.");
        }
        SaveTo(target, native);
        status.Text = $"Saved {Path.GetFileName(target)}";
    }
    private void Build()
    {
        using var pick = new SaveFileDialog { Filter = "Native game YTYP|*.ytyp", FileName = doc.Name + ".ytyp", AddExtension = true };
        if (pick.ShowDialog(this) != DialogResult.OK)
            return;
        if (!pick.FileName.EndsWith(".ytyp", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Build output must end in .ytyp.");
        SaveTo(pick.FileName, native: true);
        status.Text = "YTYP built and verified. Ready to import into your mod.";
    }
    private void SaveTo(string target, bool native)
    {
        UseWaitCursor = true;
        try
        {
            if (native)
                Document.AtomicWrite(target, doc.Build());
            else
                doc.SaveXml(target);
        }
        finally { UseWaitCursor = false; }
        path = target;
        binary = native;
        saved = doc.Xml.ToString();
        RefreshRows();
    }
    private void ExportXml()
    {
        using var pick = new SaveFileDialog { Filter = "CodeWalker XML|*.ytyp.xml", FileName = doc.Name + ".ytyp.xml" };
        if (pick.ShowDialog(this) == DialogResult.OK)
        {
            doc.SaveXml(pick.FileName);
            status.Text = "XML exported. Current save target retained.";
        }
    }
    private void Help() => MessageBox.Show(this,
        "1. New or Open a native .ytyp / CodeWalker .ytyp.xml.\n2. Add props manually or import a .ydr to read its bounds.\n3. Review texture/collision dictionary names and bounds.\n4. Build YTYP (F6) creates a native game file.\n\nDouble-click a row to edit. Ctrl+Z / Ctrl+Y undo/redo.\nExisting time/MLO data is retained; their specialised fields are not edited.\nTexture choices describe setup; existing modes are inferred from dictionary names.\nManual bounds are placeholders until replaced with actual model bounds.\nOverwritten files receive a .bak backup.\n\nProp JSON uses this editor's format; export an entry for a template.\nCodeWalker core by dexyfex. See README and upstream notices for source terms.", "YTYP Editor — quick guide", MessageBoxButtons.OK, MessageBoxIcon.Information);
    public static string? Prompt(string title, string label, string initial)
    {
        using var form = new Form { Text = title, Font = new Font("Segoe UI", 10), ClientSize = new Size(460, 155), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false };
        var text = new TextBox { Text = initial, Left = 18, Top = 53, Width = 423, AccessibleName = label };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 265, Top = 105, Width = 85 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 356, Top = 105, Width = 85 };
        form.Controls.AddRange([new Label { Text = label, AutoSize = true, Left = 18, Top = 21 }, text, ok, cancel]);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog() == DialogResult.OK ? text.Text.Trim() : null;
    }
    internal void Preview(Document sample, string destination, string? filePath = null)
    {
        doc = sample;
        path = filePath;
        saved = doc.Xml.ToString();
        RefreshRows();
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-5000, -5000);
        Show();
        Application.DoEvents();
        using var image = new Bitmap(Width, Height);
        DrawToBitmap(image, new Rectangle(0, 0, Width, Height));
        image.Save(destination);
        Hide();
    }
}
