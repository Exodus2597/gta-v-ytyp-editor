using System.Globalization;
using System.Xml.Linq;

namespace YtypEditor;

public sealed class PropDialog : Form
{
    private readonly XElement? original;
    private readonly Dictionary<string, TextBox> boxes = new();
    private readonly ComboBox textures = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "Textures" };
    private readonly ComboBox asset = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "Asset type" };
    private readonly Label error = new() { AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.Firebrick, Visible = false, Margin = new Padding(0, 8, 0, 0) };
    private readonly ToolTip tips = new();
    public XElement? Result
    {
        get; private set;
    }
    private bool loading;

    public PropDialog(XElement? item = null, PropData? imported = null)
    {
        original = item;
        Text = item is null ? "Add prop" : "Edit prop";
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 580);
        var screen = Screen.PrimaryScreen!.WorkingArea;
        Size = new Size(Math.Min(1180, screen.Width - 40), Math.Min(900, screen.Height - 60));
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), RowCount = 4, ColumnCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);
        root.Controls.Add(new Label { AutoSize = true, Text = item is null ? "Define your prop" : $"{Document.Text(item, "name")}  ·  {(string?)item.Attribute("type")}", Font = new Font(Font, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 0, 16) }, 0, 0);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        root.Controls.Add(scroll, 0, 1);
        var categories = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Margin = Padding.Empty };
        categories.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        categories.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        categories.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        categories.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        scroll.Controls.Add(categories);
        Section(categories, "Prop details");
        Add(categories, "Prop name", "Name", "Model/archetype name without .ydr. Lowercase recommended.");
        Add(categories, "Asset name", "AssetName", "Name of the drawable, fragment, or asset inside a dictionary.");
        Row(categories, "Textures", textures);
        textures.Items.AddRange(["Embedded", "External"]);
        Add(categories, "Texture dictionary", "TextureDictionary", "Embedded: usually the asset name, matching CodeWalker generation. External: .ytd name without extension. Existing values are preserved.");
        Add(categories, "LOD distance", "LodDistance", "Distance in game units before the prop stops rendering.");
        Add(categories, "Flags", "Flags", "Unsigned 32-bit bitmask. Decimal or 0x hexadecimal. 32 is the static prop default.");
        Add(categories, "Special attribute", "SpecialAttribute", "Unsigned integer; not a bitmask. Door behaviours generally require flag 67108864.");
        var basicNote = new Label { AutoSize = true, Dock = DockStyle.Fill, Text = "Existing texture modes are inferred. Verify the dictionary against your YDR/YTD.", ForeColor = Color.FromArgb(85, 95, 105), Margin = new Padding(8, 8, 8, 6) };
        FullRow(categories, basicNote);
        Section(categories, "Bounds  ·  Vectors use X Y Z");
        Add(categories, "Bounds minimum", "BoundsMin", "Three coordinates separated by spaces: X Y Z.");
        Add(categories, "Bounds maximum", "BoundsMax", "Three coordinates separated by spaces: X Y Z.");
        Add(categories, "Sphere centre", "SphereCentre", "Three coordinates separated by spaces: X Y Z.");
        Add(categories, "Sphere radius", "SphereRadius", "Bounding sphere radius in model units.");
        var calculate = new Button { Text = "Calculate sphere from box", AutoSize = true, Margin = new Padding(8) };
        calculate.Click += (_, _) => Guard(() =>
        {
            var min = Vector("BoundsMin");
            var max = Vector("BoundsMax");
            if (Enumerable.Range(0, 3).Any(i => min[i] > max[i]))
                throw new InvalidDataException("Bounds minimum must not exceed maximum.");
            var centre = Enumerable.Range(0, 3).Select(i => (min[i] + max[i]) / 2f).ToArray();
            boxes["SphereCentre"].Text = Vec(centre);
            boxes["SphereRadius"].Text = MathF.Sqrt(Enumerable.Range(0, 3).Sum(i => MathF.Pow(max[i] - centre[i], 2))).ToString("R", CultureInfo.InvariantCulture);
        });
        FullRow(categories, calculate);
        var note = new Label { AutoSize = true, Dock = DockStyle.Fill, Text = "Manual bounds are placeholders. Load a YDR or enter your model's actual bounds.", ForeColor = Color.FromArgb(85, 95, 105), Margin = new Padding(8, 8, 8, 6) };
        FullRow(categories, note);
        Section(categories, "Advanced");
        Row(categories, "Asset type", asset);
        asset.Items.AddRange(Document.AssetTypes);
        Add(categories, "HD texture dist.", "HdTextureDistance", "Distance used for high-resolution texture streaming.");
        Add(categories, "Physics dictionary", "PhysicsDictionary", "Collision dictionary name. For embedded collision, CodeWalker generation uses the asset name. Leave blank only if appropriate for your model.");
        Add(categories, "Drawable dict.", "DrawableDictionary", ".ydd name when using ASSET_TYPE_DRAWABLEDICTIONARY.");
        Add(categories, "Clip dictionary", "ClipDictionary", "Animation dictionary, if required.");
        root.Controls.Add(error, 0, 2);
        var buttons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 12, 0, 0), Margin = Padding.Empty };
        var save = new Button { Text = item is null ? "Add prop" : "Save changes", AutoSize = true, MinimumSize = new Size(115, 40), Margin = new Padding(6, 0, 0, 0) };
        save.Click += (_, _) => Guard(() => { Result = Read().ToElement(original); DialogResult = DialogResult.OK; Close(); });
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(95, 40), Margin = new Padding(6, 0, 0, 0) };
        var load = new Button { Text = "Load from YDR…", AutoSize = true, MinimumSize = new Size(155, 40), Margin = new Padding(6, 0, 0, 0) };
        load.Click += (_, _) => Guard(() =>
        {
            using var pick = new OpenFileDialog { Filter = "Drawable files|*.ydr;*.ydr.xml|All files|*.*" };
            if (pick.ShowDialog(this) == DialogResult.OK)
                Fill(PropData.FromYdr(pick.FileName));
        });
        buttons.Controls.AddRange([save, cancel, load]);
        root.Controls.Add(buttons, 0, 3);
        AcceptButton = save;
        CancelButton = cancel;
        Fill(imported ?? (item is not null ? PropData.Read(item) : new PropData()));
        textures.SelectedIndexChanged += (_, _) => TextureChanged();
        boxes["Name"].TextChanged += (_, _) => NameChanged();
        boxes["AssetName"].TextChanged += (_, _) => AssetChanged();
    }
    private string previousName = "";
    private void TextureChanged()
    {
        if (loading)
            return;
        if ((string?)textures.SelectedItem == "Embedded" || boxes["TextureDictionary"].Text.Length == 0)
            boxes["TextureDictionary"].Text = boxes["AssetName"].Text;
    }
    private void NameChanged()
    {
        if (!loading && original is null && (boxes["AssetName"].Text.Length == 0 || boxes["AssetName"].Text == previousName))
            boxes["AssetName"].Text = boxes["Name"].Text;
        previousName = boxes["Name"].Text;
    }
    private void AssetChanged()
    {
        if (!loading && original is null && (string?)textures.SelectedItem == "Embedded")
            boxes["TextureDictionary"].Text = boxes["AssetName"].Text;
    }
    private static void Section(TableLayoutPanel table, string title)
    {
        FullRow(table, new Label { Text = title, Font = new Font(table.Font, FontStyle.Bold), AutoSize = true, Dock = DockStyle.Fill, BackColor = Color.FromArgb(234, 239, 245), Padding = new Padding(8), Margin = new Padding(0, 8, 0, 6) });
    }
    private static void FullRow(TableLayoutPanel table, Control control)
    {
        int row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(control, 0, row);
        table.SetColumnSpan(control, 4);
    }
    private void Add(TableLayoutPanel table, string label, string key, string hint)
    {
        var input = new TextBox { Dock = DockStyle.Fill, AccessibleName = label, Margin = new Padding(6, 4, 6, 4) };
        boxes.Add(key, input);
        tips.SetToolTip(input, hint);
        Row(table, label, input);
    }
    private static void Row(TableLayoutPanel table, string label, Control input)
    {
        int r = table.RowCount - 1;
        int column = 2;
        var last = r >= 0 ? table.GetControlFromPosition(0, r) : null;
        if (last is null || table.GetColumnSpan(last) != 1 || table.GetControlFromPosition(2, r) is not null)
        {
            r = table.RowCount++;
            column = 0;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 7, 6, 7) }, column, r);
        table.Controls.Add(input, column + 1, r);
    }
    private void Guard(Action action)
    {
        try
        {
            action();
            error.Text = "";
            error.Visible = false;
        }
        catch (Exception ex) { error.Text = ex.Message; error.Visible = true; }
    }
    private void Fill(PropData p)
    {
        loading = true;
        boxes["Name"].Text = p.Name;
        boxes["AssetName"].Text = p.AssetName;
        textures.SelectedItem = p.TextureMode;
        boxes["TextureDictionary"].Text = p.TextureDictionary;
        boxes["PhysicsDictionary"].Text = p.PhysicsDictionary;
        boxes["DrawableDictionary"].Text = p.DrawableDictionary;
        boxes["ClipDictionary"].Text = p.ClipDictionary;
        boxes["LodDistance"].Text = p.LodDistance.ToString(CultureInfo.InvariantCulture);
        boxes["HdTextureDistance"].Text = p.HdTextureDistance.ToString(CultureInfo.InvariantCulture);
        boxes["Flags"].Text = p.Flags.ToString(CultureInfo.InvariantCulture);
        boxes["SpecialAttribute"].Text = p.SpecialAttribute.ToString(CultureInfo.InvariantCulture);
        boxes["SphereRadius"].Text = p.SphereRadius.ToString(CultureInfo.InvariantCulture);
        boxes["BoundsMin"].Text = Vec(p.BoundsMin);
        boxes["BoundsMax"].Text = Vec(p.BoundsMax);
        boxes["SphereCentre"].Text = Vec(p.SphereCentre);
        asset.SelectedItem = p.AssetType;
        previousName = p.Name;
        loading = false;
    }
    private static string Vec(float[] v) => string.Join(" ", v.Select(x => x.ToString("R", CultureInfo.InvariantCulture)));
    private float Float(string key) => float.Parse(boxes[key].Text, NumberStyles.Float, CultureInfo.InvariantCulture);
    private float[] Vector(string key) => boxes[key].Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(s => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
    private PropData Read() => new()
    {
        Name = boxes["Name"].Text.Trim(),
        AssetName = boxes["AssetName"].Text.Trim(),
        TextureMode = (string)textures.SelectedItem!,
        TextureDictionary = boxes["TextureDictionary"].Text.Trim(),
        PhysicsDictionary = boxes["PhysicsDictionary"].Text.Trim(),
        DrawableDictionary = boxes["DrawableDictionary"].Text.Trim(),
        ClipDictionary = boxes["ClipDictionary"].Text.Trim(),
        AssetType = (string)asset.SelectedItem!,
        LodDistance = Float("LodDistance"),
        HdTextureDistance = Float("HdTextureDistance"),
        Flags = Document.ParseUInt(boxes["Flags"].Text.Trim()),
        SpecialAttribute = Document.ParseUInt(boxes["SpecialAttribute"].Text.Trim()),
        BoundsMin = Vector("BoundsMin"),
        BoundsMax = Vector("BoundsMax"),
        SphereCentre = Vector("SphereCentre"),
        SphereRadius = Float("SphereRadius")
    };
}
