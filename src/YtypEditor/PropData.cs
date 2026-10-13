using System.Xml.Linq;
using CodeWalker.GameFiles;

namespace YtypEditor;

public sealed class PropData
{
    public string Name { get; set; } = "";
    public string AssetName { get; set; } = "";
    public string TextureMode { get; set; } = "Embedded";
    public string TextureDictionary { get; set; } = "";
    public string PhysicsDictionary { get; set; } = "";
    public string DrawableDictionary { get; set; } = "";
    public string ClipDictionary { get; set; } = "";
    public string AssetType { get; set; } = "ASSET_TYPE_DRAWABLE";
    public float LodDistance { get; set; } = 500;
    public float HdTextureDistance { get; set; } = 250;
    public uint Flags { get; set; } = 32;
    public uint SpecialAttribute
    {
        get; set;
    }
    public float[] BoundsMin { get; set; } = [-0.5f, -0.5f, -0.5f];
    public float[] BoundsMax { get; set; } = [0.5f, 0.5f, 0.5f];
    public float[] SphereCentre { get; set; } = [0, 0, 0];
    public float SphereRadius { get; set; } = 0.8660254f;
    public string? ArchetypeXml
    {
        get; set;
    }

    public static PropData Read(XElement e) => new()
    {
        Name = Document.Text(e, "name"),
        AssetName = Document.Text(e, "assetName"),
        TextureMode = TextureHint(e),
        TextureDictionary = Document.Text(e, "textureDictionary"),
        PhysicsDictionary = Document.Text(e, "physicsDictionary"),
        DrawableDictionary = Document.Text(e, "drawableDictionary"),
        ClipDictionary = Document.Text(e, "clipDictionary"),
        AssetType = Document.Text(e, "assetType"),
        LodDistance = Document.Float(e, "lodDist"),
        HdTextureDistance = Document.Float(e, "hdTextureDist"),
        Flags = Document.UInt(e, "flags"),
        SpecialAttribute = Document.UInt(e, "specialAttribute"),
        BoundsMin = Document.Vector(e, "bbMin"),
        BoundsMax = Document.Vector(e, "bbMax"),
        SphereCentre = Document.Vector(e, "bsCentre"),
        SphereRadius = Document.Float(e, "bsRadius"),
        ArchetypeXml = e.ToString()
    };

    public static string TextureHint(XElement e)
    {
        var dict = Document.Text(e, "textureDictionary");
        var asset = Document.Text(e, "assetName");
        return dict.Length == 0 || (uint)XmlMeta.GetHash(dict) == (uint)XmlMeta.GetHash(asset) ? "Embedded" : "External";
    }

    public void Validate()
    {
        Document.CheckName(Name, "Prop name");
        Document.CheckName(AssetName, "Asset name");
        foreach (var name in new[] { TextureDictionary, PhysicsDictionary, DrawableDictionary, ClipDictionary })
        {
            if (name is null)
                throw new InvalidDataException("Dictionary fields must be strings, not null.");
            if (name.Length > 0)
                Document.CheckName(name, "Dictionary name");
        }
        if (TextureMode is not ("External" or "Embedded"))
            throw new InvalidDataException("Textures must be External or Embedded.");
        if (TextureMode == "External" && TextureDictionary.Length == 0)
            throw new InvalidDataException("External textures require a texture dictionary name (without .ytd).");
        if (!Document.AssetTypes.Contains(AssetType))
            throw new InvalidDataException("Choose a valid asset type.");
        foreach (var val in new[] { LodDistance, HdTextureDistance, SphereRadius })
            if (!float.IsFinite(val) || val < 0)
                throw new InvalidDataException("Distances and sphere radius must be finite, non-negative numbers.");
        foreach (var v in new[] { BoundsMin, BoundsMax, SphereCentre })
            if (v is null || v.Length != 3 || v.Any(x => !float.IsFinite(x)))
                throw new InvalidDataException("Each bound/centre requires three finite coordinates.");
        for (int i = 0; i < 3; i++)
            if (BoundsMin[i] > BoundsMax[i])
                throw new InvalidDataException("Bounds minimum must not exceed maximum.");
    }

    public XElement ToElement(XElement? original = null)
    {
        Validate();
        XElement e;
        if (original is not null)
            e = new XElement(original);
        else if (ArchetypeXml is not null)
            e = Document.ParseXml(ArchetypeXml).Root!;
        else
            e = new XElement("Item", new XAttribute("type", "CBaseArchetypeDef"));
        if (e.Name != "Item" || !Document.ArchetypeTypes.Contains((string?)e.Attribute("type") ?? ""))
            throw new InvalidDataException("Unsupported archetype XML.");
        Document.SetValue(e, "lodDist", LodDistance);
        Document.SetValue(e, "flags", Flags);
        Document.SetValue(e, "specialAttribute", SpecialAttribute);
        Document.SetVector(e, "bbMin", BoundsMin);
        Document.SetVector(e, "bbMax", BoundsMax);
        Document.SetVector(e, "bsCentre", SphereCentre);
        Document.SetValue(e, "bsRadius", SphereRadius);
        Document.SetValue(e, "hdTextureDist", HdTextureDistance);
        Document.SetText(e, "name", Name);
        Document.SetText(e, "textureDictionary", TextureDictionary);
        Document.SetText(e, "clipDictionary", ClipDictionary);
        Document.SetText(e, "drawableDictionary", DrawableDictionary);
        Document.SetText(e, "physicsDictionary", PhysicsDictionary);
        Document.SetText(e, "assetType", AssetType);
        Document.SetText(e, "assetName", AssetName);
        if (e.Element("extensions") is null)
            e.Add(new XElement("extensions"));
        return e;
    }

    public static PropData FromYdr(string path)
    {
        if (new FileInfo(path).Length > 64 * 1024 * 1024)
            throw new InvalidDataException("YDR exceeds the editor's 64 MB limit.");
        var p = new PropData();
        p.Name = Path.GetFileName(path).Replace(".xml", "", StringComparison.OrdinalIgnoreCase).Replace(".ydr", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        p.AssetName = p.Name;
        if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            var x = Document.ParseXml(File.ReadAllText(path)).Root!;
            if (x.Name != "Drawable")
                throw new InvalidDataException("Expected CodeWalker Drawable XML.");
            p.BoundsMin = Document.Vector(x, "BoundingBoxMin");
            p.BoundsMax = Document.Vector(x, "BoundingBoxMax");
            p.SphereCentre = Document.Vector(x, "BoundingSphereCenter");
            p.SphereRadius = Document.Float(x, "BoundingSphereRadius");
            p.TextureMode = x.Element("ShaderGroup")?.Element("TextureDictionary")?.Elements().Any() == true ? "Embedded" : "External";
            p.PhysicsDictionary = x.Element("Bounds")?.Elements().Any() == true ? p.Name : "";
        }
        else
        {
            var modelBytes = File.ReadAllBytes(path);
            if (modelBytes.Length < 16 || !modelBytes.AsSpan(0, 4).SequenceEqual("RSC7"u8))
                throw new InvalidDataException("Expected a native RSC7 YDR or CodeWalker Drawable XML.");
            if (RpfResourceFileEntry.GetVersionFromFlags(BitConverter.ToUInt32(modelBytes, 8), BitConverter.ToUInt32(modelBytes, 12)) != 165)
                throw new InvalidDataException("Native YDR import supports GTA V Legacy drawables. Export Enhanced/other drawables as .ydr.xml in CodeWalker and import that XML.");
            var ydr = new YdrFile();
            ydr.Load(modelBytes);
            var d = ydr.Drawable ?? throw new InvalidDataException("Could not read this YDR. Export it as XML from CodeWalker and retry.");
            p.BoundsMin = [d.BoundingBoxMin.X, d.BoundingBoxMin.Y, d.BoundingBoxMin.Z];
            p.BoundsMax = [d.BoundingBoxMax.X, d.BoundingBoxMax.Y, d.BoundingBoxMax.Z];
            p.SphereCentre = [d.BoundingCenter.X, d.BoundingCenter.Y, d.BoundingCenter.Z];
            p.SphereRadius = d.BoundingSphereRadius;
            p.TextureMode = d.ShaderGroup?.TextureDictionary is not null ? "Embedded" : "External";
            p.PhysicsDictionary = d.Bound is not null ? p.Name : "";
        }
        p.TextureDictionary = p.Name;
        return p;
    }
}
