using System.Xml.Linq;
using CodeWalker.GameFiles;

namespace YtypEditor;

internal static class Tests
{
    static int checks;
    static readonly List<string> results = new();
    static void Check(bool condition, string description)
    {
        if (!condition)
            throw new Exception(description);
        results.Add("PASS " + description);
        checks++;
    }
    static void Reject(Action action, string description)
    {
        try
        {
            action();
        }
        catch { Check(true, description); return; }
        throw new Exception("Expected rejection: " + description);
    }
    public static void Run()
    {
        var testDir = Path.Combine(AppContext.BaseDirectory, "test-work");
        Directory.CreateDirectory(testDir);
        var doc = Document.New("test_props");
        var staticProp = new PropData { Name = "batlocker", AssetName = "batlocker", TextureMode = "External", TextureDictionary = "shared_props", PhysicsDictionary = "batlocker", Flags = 32 };
        var embedded = new PropData { Name = "modern_chair", AssetName = "modern_chair", TextureDictionary = "modern_chair", Flags = uint.MaxValue };
        doc.Archetypes.Add(staticProp.ToElement(), embedded.ToElement());
        var bytes = doc.Build();
        Check(bytes.AsSpan(0, 4).SequenceEqual("RSC7"u8), "Build emits native RSC7, not renamed XML");
        var bin = Path.Combine(testDir, "test_props.ytyp");
        Document.AtomicWrite(bin, bytes);
        JenkIndex.Clear();
        var reopened = Document.Open(bin);
        Check(reopened.Props.Count() == 2, "Native binary opens with both props");
        Check(Document.Text(reopened.Props.First(), "name") == "batlocker", "Prop names reopen with an empty process hash index");
        Check(PropData.Read(reopened.Props.Last()).Flags == uint.MaxValue, "Full unsigned 32-bit flags survive native round trip");
        Check(Document.Text(reopened.Props.First(), "textureDictionary") == "shared_props", "External dictionary survives native round trip");
        Check(Document.Text(reopened.Props.Last(), "textureDictionary") == "modern_chair", "Embedded dictionary survives native round trip");
        var changed = PropData.Read(reopened.Props.First());
        changed.LodDistance = 725;
        reopened.Props.First().ReplaceWith(changed.ToElement(reopened.Props.First()));
        reopened.Archetypes.Add(new PropData { Name = "added_prop", AssetName = "added_prop" }.ToElement());
        Document.AtomicWrite(bin, reopened.Build());
        Check(File.Exists(bin + ".bak"), "Overwriting native file creates backup");
        Check(Document.Open(bin).Props.Count() == 3 && Document.Float(Document.Open(bin).Props.First(), "lodDist") == 725, "Open, edit, add, build and reopen native file");
        Check(File.ReadAllBytes(bin + ".bak").SequenceEqual(bytes), "Backup retains exact prior native file");
        var xmlPath = Path.Combine(testDir, "test_props.ytyp.xml");
        doc.SaveXml(xmlPath);
        var xml = Document.Open(xmlPath);
        Check(XNode.DeepEquals(Document.ParseXml(doc.Xml.ToString()).Root, xml.Root), "XML open/save preserves complete document");
        var json = Document.ReadJson(Document.Json(doc.Props));
        Check(json.Length == 2 && XNode.DeepEquals(Document.ParseXml(json[0].ToString()).Root, Document.ParseXml(doc.Props.First().ToString()).Root), "Editor JSON export/import preserves prop fields");
        Reject(() => Document.ParseXml("<!DOCTYPE CMapTypes [<!ENTITY x SYSTEM 'file:///secret'>]><CMapTypes>&x;</CMapTypes>"), "XML DTD/external entity expansion rejected");
        Reject(() => Document.Open(Path.Combine(testDir, "missing.ytyp")), "Missing input raises error");
        Reject(() => new Document(Document.ParseXml("<CMapData/>")), "Wrong document type rejected");
        Reject(() => doc.EnsureUnique("batlocker"), "Duplicate prop rejected");
        Reject(() => doc.EnsureUnique("hash_" + ((uint)XmlMeta.GetHash("batlocker")).ToString("X8")), "Hash-equivalent duplicate rejected");
        Reject(() => Document.CheckName("bad/name", "name"), "Invalid asset name rejected");
        Reject(() => Document.CheckName("hash_nope", "name"), "Invalid literal hash rejected");
        Reject(() => new PropData { Name = "p", AssetName = "p", BoundsMin = [1, 1, 1], BoundsMax = [0, 0, 0] }.ToElement(), "Inverted bounds rejected");
        Reject(() => new PropData { Name = "p", AssetName = "p", SphereRadius = float.NaN }.ToElement(), "Non-finite float rejected");
        Reject(() => new PropData { Name = "p", AssetName = "p", TextureMode = "External" }.ToElement(), "External textures require dictionary name");
        Reject(() => Document.ParseUInt("4294967296"), "Overflowing uint rejected");
        Check(Document.ParseUInt("0x80000000") == 2147483648, "Hex uint flag accepted");
        Reject(() => Document.ReadJson("[null]"), "Null JSON entry rejected");

        var complex = Document.New("complex_props");
        var time = new PropData { Name = "timed_prop", AssetName = "timed_prop" }.ToElement();
        time.SetAttributeValue("type", "CTimeArchetypeDef");
        time.Add(new XElement("timeFlags", new XAttribute("value", "4294967295")));
        complex.Archetypes.Add(time);
        var mlo = new PropData { Name = "test_interior", AssetName = "test_interior", AssetType = "ASSET_TYPE_ASSETLESS" }.ToElement();
        mlo.SetAttributeValue("type", "CMloArchetypeDef");
        mlo.Add(new XElement("mloFlags", new XAttribute("value", "0")), new XElement("entities"), new XElement("rooms", new XElement("Item", new XElement("name", "limbo"), new XElement("bbMin", new XAttribute("x", "-10"), new XAttribute("y", "-10"), new XAttribute("z", "-10")), new XElement("bbMax", new XAttribute("x", "10"), new XAttribute("y", "10"), new XAttribute("z", "10")), new XElement("blend", new XAttribute("value", "1")), new XElement("timecycleName"), new XElement("secondaryTimecycleName"), new XElement("flags", new XAttribute("value", "0")), new XElement("portalCount", new XAttribute("value", "0")), new XElement("floorId", new XAttribute("value", "0")), new XElement("exteriorVisibiltyDepth", new XAttribute("value", "-1")), new XElement("attachedObjects"))), new XElement("portals"), new XElement("entitySets"), new XElement("timeCycleModifiers"));
        complex.Archetypes.Add(mlo);
        complex.Root.Element("dependencies")!.Add(new XElement("Item", "shared_props"), new XElement("Item", "other_props"));
        time.Element("extensions")!.Add(Document.ParseXml("<Item type='CExtensionDefAudioCollisionSettings'><name>collision_sound</name><offsetPosition x='1' y='2' z='3'/><settings>test_audio</settings></Item>").Root!);
        complex.Root.Element("extensions")!.Add(Document.ParseXml("<Item type='CExtensionDefAudioCollisionSettings'><name>root_sound</name><offsetPosition x='0' y='0' z='0'/><settings>root_audio</settings></Item>").Root!);
        complex.Root.Element("compositeEntityTypes")!.Add(Document.ParseXml("<Item><Name>test_composite</Name><lodDist value='80'/><flags value='32'/><specialAttribute value='0'/><bbMin x='-1' y='-1' z='-1'/><bbMax x='1' y='1' z='1'/><bsCentre x='0' y='0' z='0'/><bsRadius value='2'/><StartModel>start_model</StartModel><EndModel>end_model</EndModel><StartImapFile>start_map</StartImapFile><EndImapFile>end_map</EndImapFile><PtFxAssetName/><Animations/></Item>").Root!);
        var nativeComplex = complex.Build();
        var complexFile = Path.Combine(testDir, "complex.ytyp");
        Document.AtomicWrite(complexFile, nativeComplex);
        var loadedComplex = Document.Open(complexFile);
        Check(Document.UInt(loadedComplex.Props.First(), "timeFlags") == uint.MaxValue, "Time archetype timeFlags preserved in native build");
        Check(loadedComplex.Props.Last().Element("rooms")!.Elements().Count() == 1, "MLO room preserved in native build");
        Check(loadedComplex.Root.Element("dependencies")!.Value.Contains("shared_props"), "Root dependencies preserved in native build");
        Check(loadedComplex.Root.Element("extensions")!.Elements().Count() == 1 && loadedComplex.Props.First().Element("extensions")!.Elements().Count() == 1, "Root and archetype extensions preserved in native build");
        Check(loadedComplex.Root.Element("compositeEntityTypes")!.Element("Item")!.Element("Name")!.Value == "test_composite", "Composite entity definition preserved in native build");
        var priorRoom = loadedComplex.Props.Last().Element("rooms")!.ToString();
        var mloEdit = PropData.Read(loadedComplex.Props.Last());
        mloEdit.LodDistance = 1000;
        loadedComplex.Props.Last().ReplaceWith(mloEdit.ToElement(loadedComplex.Props.Last()));
        loadedComplex.Build();
        Check(loadedComplex.Props.Last().Element("rooms")!.ToString() == priorRoom, "Common-field editing retains specialised MLO data");
        var unknown = Document.New("unknown");
        unknown.Archetypes.Add(staticProp.ToElement());
        unknown.Props.First().Add(new XElement("unsupportedField", "retain me"));
        Reject(() => unknown.Build(), "Build refuses unsupported field loss");
        unknown.SaveXml(Path.Combine(testDir, "unknown.ytyp.xml"));
        Check(Document.Open(Path.Combine(testDir, "unknown.ytyp.xml")).Props.First().Element("unsupportedField")!.Value == "retain me", "XML export retains unknown fields");

        var ydr = new YdrFile { Drawable = new Drawable { BoundingBoxMin = new SharpDX.Vector3(-2, -3, -4), BoundingBoxMax = new SharpDX.Vector3(2, 3, 4), BoundingCenter = new SharpDX.Vector3(0, 0, 0), BoundingSphereRadius = 6, ShaderGroup = new ShaderGroup(), LightAttributes = new ResourceSimpleList64<LightAttributes>() } };
        var ydrPath = Path.Combine(testDir, "import_prop.ydr");
        Document.AtomicWrite(ydrPath, ydr.Save());
        var imported = PropData.FromYdr(ydrPath);
        Check(imported.Name == "import_prop" && imported.BoundsMin.SequenceEqual(new[] { -2f, -3f, -4f }) && imported.SphereRadius == 6, "Native YDR import reads name and bounds");
        Check(imported.TextureMode == "External", "Native YDR import detects absent embedded dictionary");
        var ydrXml = Path.Combine(testDir, "xml_prop.ydr.xml");
        File.WriteAllText(ydrXml, "<Drawable><BoundingBoxMin x='-2' y='-3' z='-4'/><BoundingBoxMax x='2' y='3' z='4'/><BoundingSphereCenter x='0' y='0' z='0'/><BoundingSphereRadius value='6'/><ShaderGroup><TextureDictionary><Item/></TextureDictionary></ShaderGroup></Drawable>");
        var importedXml = PropData.FromYdr(ydrXml);
        Check(importedXml.Name == "xml_prop" && importedXml.TextureMode == "Embedded" && importedXml.SphereRadius == 6, "YDR XML import reads bounds and embedded texture hint");
        ApplicationConfiguration.Initialize();
        using var main = new MainForm();
        main.Preview(doc, Path.Combine(AppContext.BaseDirectory, "editor-preview.png"), @"C:\GTA V Mods\Custom Props\My DLC Pack\stream\test_props.ytyp");
        using var addForm = new PropDialog();
        var addControls = Controls(addForm).ToArray();
        var nameInput = addControls.OfType<TextBox>().Single(c => c.AccessibleName == "Prop name");
        var assetInput = addControls.OfType<TextBox>().Single(c => c.AccessibleName == "Asset name");
        var textureInput = addControls.OfType<TextBox>().Single(c => c.AccessibleName == "Texture dictionary");
        var textureMode = addControls.OfType<ComboBox>().Single(c => c.AccessibleName == "Textures");
        nameInput.Text = "new_prop";
        Check(assetInput.Text == "new_prop" && textureInput.Text == "new_prop", "New prop name fills model and embedded texture references");
        assetInput.Text = "existing_model";
        nameInput.Text = "renamed_prop";
        Check(assetInput.Text == "existing_model" && textureInput.Text == "existing_model", "Renaming retains a manually chosen model reference");
        textureMode.SelectedItem = "External";
        textureInput.Text = "shared_textures";
        nameInput.Text = "renamed_again";
        Check(textureInput.Text == "shared_textures", "Renaming retains external texture reference");

        using var propForm = new PropDialog(doc.Props.First());
        var editControls = Controls(propForm).ToArray();
        Check(editControls.OfType<TextBox>().Single(c => c.AccessibleName == "Flags").Text == "32"
            && editControls.OfType<TextBox>().Single(c => c.AccessibleName == "Texture dictionary").Text == "shared_props",
            "Edit dialog loads numeric fields and dictionary references without reflection");
        propForm.ShowInTaskbar = false;
        propForm.StartPosition = FormStartPosition.Manual;
        propForm.Location = new Point(-5000, -5000);
        propForm.Show();
        Application.DoEvents();
        using (var bitmap = new Bitmap(propForm.Width, propForm.Height))
        {
            propForm.DrawToBitmap(bitmap, new Rectangle(0, 0, propForm.Width, propForm.Height));
            bitmap.Save(Path.Combine(AppContext.BaseDirectory, "prop-preview.png"));
        }
        propForm.Size = propForm.MinimumSize;
        Application.DoEvents();
        using (var bitmap = new Bitmap(propForm.Width, propForm.Height))
        {
            propForm.DrawToBitmap(bitmap, new Rectangle(0, 0, propForm.Width, propForm.Height));
            bitmap.Save(Path.Combine(AppContext.BaseDirectory, "prop-small-preview.png"));
        }
        propForm.Hide();
        Check(File.Exists(Path.Combine(AppContext.BaseDirectory, "editor-preview.png")), "Windows Forms UI renders successfully");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "test-results.txt"), $"{checks} checks passed\n" + string.Join("\n", results));
    }

    private static IEnumerable<Control> Controls(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Controls(child))
                yield return descendant;
        }
    }
}
