using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using CodeWalker.GameFiles;

namespace YtypEditor;

public sealed class Document
{
    private const int MaxFileSize = 64 * 1024 * 1024;
    private static readonly string[] Axes = ["x", "y", "z"];
    private static readonly string[] AssetExtensions = [".ydr", ".ydd", ".yft", ".ytd", ".ybn", ".ytyp"];
    private static readonly string[] NameFields = ["name", "assetName", "textureDictionary", "physicsDictionary", "clipDictionary", "drawableDictionary", "settings"];
    public static readonly string[] AssetTypes = ["ASSET_TYPE_DRAWABLE", "ASSET_TYPE_DRAWABLEDICTIONARY", "ASSET_TYPE_FRAGMENT", "ASSET_TYPE_ASSETLESS", "ASSET_TYPE_UNINITIALIZED"];
    public static readonly string[] ArchetypeTypes = ["CBaseArchetypeDef", "CTimeArchetypeDef", "CMloArchetypeDef"];
    public XDocument Xml
    {
        get;
    }
    public XElement Root => Xml.Root!;
    public XElement Archetypes => Root.Element("archetypes")!;
    public IEnumerable<XElement> Props => Archetypes.Elements("Item");
    public string Name
    {
        get => Text(Root, "name"); set
        {
            CheckName(value, "YTYP name");
            SetText(Root, "name", value);
        }
    }
    public Document(XDocument xml)
    {
        Xml = xml;
        CheckStructure();
    }
    public static Document New(string name = "my_props") => new(new XDocument(new XElement("CMapTypes", new XElement("extensions"), new XElement("archetypes"), new XElement("name", name), new XElement("dependencies"), new XElement("compositeEntityTypes"))));
    public static XDocument ParseXml(string text)
    {
        using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxFileSize });
        return XDocument.Load(reader);
    }
    public static Document Open(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > MaxFileSize)
            throw new InvalidDataException("File exceeds the editor's 64 MB limit.");
        if (bytes.Length >= 4 && Encoding.ASCII.GetString(bytes, 0, 4) == "RSC7")
        {
            // Existing game files often contain hashes only; neighbouring asset names resolve those hashes.
            foreach (var asset in Directory.EnumerateFiles(Path.GetDirectoryName(Path.GetFullPath(path))!))
                if (AssetExtensions.Contains(Path.GetExtension(asset).ToLowerInvariant()))
                    JenkIndex.Ensure(Path.GetFileNameWithoutExtension(asset).ToLowerInvariant());
            var ytyp = new YtypFile();
            ytyp.Load(bytes);
            if (ytyp.Meta is null)
                throw new InvalidDataException("Expected a native RSC7 YTYP.");
            return new Document(ParseXml(MetaXml.GetXml(ytyp, out _)));
        }
        return new Document(ParseXml(File.ReadAllText(path)));
    }
    public void CheckStructure()
    {
        if (Root.Name != "CMapTypes" || Root.Element("archetypes") is null)
            throw new InvalidDataException("Expected CMapTypes with an archetypes section.");
        if (Root.Element("name") is null)
            throw new InvalidDataException("YTYP name is missing.");
        if (Root.Elements().GroupBy(e => e.Name).Any(g => g.Count() > 1))
            throw new InvalidDataException("Duplicate YTYP sections.");
        if (Archetypes.Elements().Any(e => e.Name != "Item"))
            throw new InvalidDataException("Unexpected element in archetypes.");
    }
    public static void CheckName(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 255 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-')))
            throw new InvalidDataException($"{label}: use 1–255 letters, numbers, underscores or hyphens; omit file extensions.");
        if (value.StartsWith("hash_", StringComparison.Ordinal) && !uint.TryParse(value.AsSpan(5), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            throw new InvalidDataException($"{label}: hash_ must be followed by up to 8 hexadecimal digits.");
    }
    public void Validate()
    {
        CheckStructure();
        CheckName(Name, "YTYP name");
        var hashes = new HashSet<uint>();
        foreach (var e in Props)
        {
            var p = PropData.Read(e);
            p.Validate();
            if (!hashes.Add((uint)XmlMeta.GetHash(p.Name)))
                throw new InvalidDataException($"Duplicate prop name/hash: {p.Name}.");
        }
    }
    public void EnsureUnique(string name, XElement? except = null)
    {
        CheckName(name, "Prop name");
        if (Props.Any(e => e != except && (uint)XmlMeta.GetHash(Text(e, "name")) == (uint)XmlMeta.GetHash(name)))
            throw new InvalidDataException($"A prop named {name} (or the same hash) already exists.");
    }
    public byte[] Build()
    {
        Validate();
        var xd = new XmlDocument { XmlResolver = null };
        xd.LoadXml(Xml.ToString());
        var meta = XmlMeta.GetMeta(xd);
        // Store human-readable names in RAGE's existing string section so reopening does not depend on this process's hash index.
        var names = Root.Descendants().Where(e => !e.HasElements && NameFields.Contains(e.Name.LocalName))
            .Select(e => e.Value).Where(s => s.Length > 0 && s.Length <= 255 && !s.StartsWith("hash_") && s.All(char.IsAscii) && !s.Contains('\0')).Distinct().ToArray();
        if (names.Length > 0)
        {
            var data = Encoding.UTF8.GetBytes(string.Join('\0', names) + '\0');
            var block = meta.DataBlocks.Data.FirstOrDefault(b => b.StructureNameHash == (MetaName)MetaTypeName.STRING);
            if (block is null)
                meta.DataBlocks.Add(new MetaDataBlock { StructureNameHash = (MetaName)MetaTypeName.STRING, Data = data });
            else
                block.Data = block.Data.Concat(data).ToArray();
        }
        var bytes = ResourceBuilder.Build(meta, 2);
        var roundtrip = new YtypFile();
        roundtrip.Load(bytes);
        var rebuilt = ParseXml(MetaXml.GetXml(roundtrip, out _));
        Compare(Root, rebuilt.Root!, "/CMapTypes");
        return bytes;
    }
    // Check every source field after compilation; prevent silent loss of unsupported data.
    private static void Compare(XElement a, XElement b, string path)
    {
        if (a.Name != b.Name)
            throw new InvalidDataException($"Build changed {path}.");
        foreach (var at in a.Attributes())
        {
            if (path == "/CMapTypes" && at.Name == "name")
                continue; // Meta resource label, not the map name.
            var bt = b.Attribute(at.Name)?.Value;
            if (bt is null || !Equivalent(at.Value, bt))
                throw new InvalidDataException($"Build cannot preserve {path}/@{at.Name}. Export XML instead.");
        }
        if (!a.HasElements)
        {
            if (!Equivalent(a.Value.Trim(), b.Value.Trim()))
                throw new InvalidDataException($"Build cannot preserve {path}. Export XML instead.");
            return;
        }
        foreach (var group in a.Elements().GroupBy(x => x.Name))
        {
            var left = group.ToArray();
            var right = b.Elements(group.Key).ToArray();
            if (left.Length != right.Length)
                throw new InvalidDataException($"Build cannot preserve {path}/{group.Key}. Export XML instead.");
            for (int i = 0; i < left.Length; i++)
                Compare(left[i], right[i], $"{path}/{group.Key}[{i}]");
        }
    }
    private static bool Equivalent(string a, string b)
    {
        if (a == b)
            return true;
        var aa = a.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var bb = b.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (aa.Length != bb.Length)
            return false;
        for (int i = 0; i < aa.Length; i++)
        {
            if (aa[i] == bb[i])
                continue;
            if (long.TryParse(aa[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ai) && long.TryParse(bb[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bi))
            {
                if (ai != bi)
                    return false;
            }
            else if (double.TryParse(aa[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var av) && double.TryParse(bb[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var bv))
            {
                if (!double.IsFinite(av) || !double.IsFinite(bv) || Math.Abs(av - bv) > Math.Max(1e-7, Math.Abs(av) * 1e-6))
                    return false;
            }
            else if (!(aa[i].StartsWith("hash_") || bb[i].StartsWith("hash_")) || (uint)XmlMeta.GetHash(aa[i]) != (uint)XmlMeta.GetHash(bb[i]))
                return false;
        }
        return true;
    }
    public static void AtomicWrite(string path, byte[] bytes)
    {
        var full = Path.GetFullPath(path);
        var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            if (File.Exists(full))
                File.Replace(temp, full, full + ".bak", true);
            else
                File.Move(temp, full);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void SaveXml(string path) => AtomicWrite(path, Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + Xml));
    public static string Text(XElement e, string field) => e.Element(field)?.Value ?? "";
    public static float Float(XElement e, string field) => float.Parse(e.Element(field)?.Attribute("value")?.Value ?? "0", CultureInfo.InvariantCulture);
    public static uint UInt(XElement e, string field) => ParseUInt(e.Element(field)?.Attribute("value")?.Value ?? "0");
    public static uint ParseUInt(string s) => s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? uint.Parse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) : uint.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
    public static float[] Vector(XElement e, string field) => Axes.Select(a => float.Parse(e.Element(field)?.Attribute(a)?.Value ?? "0", CultureInfo.InvariantCulture)).ToArray();
    private static XElement Field(XElement e, string field)
    {
        var x = e.Element(field);
        if (x is null)
        {
            x = new XElement(field);
            e.Add(x);
        }
        return x;
    }
    public static void SetText(XElement e, string field, string value) => Field(e, field).Value = value;
    public static void SetValue(XElement e, string field, object value) => Field(e, field).SetAttributeValue("value", Convert.ToString(value, CultureInfo.InvariantCulture));
    public static void SetVector(XElement e, string field, float[] v)
    {
        var element = Field(e, field);
        for (int i = 0; i < Axes.Length; i++)
            element.SetAttributeValue(Axes[i], v[i].ToString("R", CultureInfo.InvariantCulture));
    }
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static string Json(IEnumerable<XElement> items) => JsonSerializer.Serialize(items.Select(PropData.Read), JsonOptions);
    public static XElement[] ReadJson(string text)
    {
        if (text.Length > MaxFileSize)
            throw new InvalidDataException("JSON exceeds 64 MB.");
        using var j = JsonDocument.Parse(text);
        var props = j.RootElement.ValueKind == JsonValueKind.Array ? JsonSerializer.Deserialize<PropData[]>(text, JsonOptions)! : [JsonSerializer.Deserialize<PropData>(text, JsonOptions)!];
        if (props.Any(p => p is null))
            throw new InvalidDataException("JSON contains an empty prop.");
        return props.Select(p => p.ToElement()).ToArray();
    }
}
