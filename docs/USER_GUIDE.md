# YTYP Editor 1.1.1

Run **YtypEditor.exe** on Windows 10/11, x64. No installer or .NET installation is needed. Keep `README.md` and the `licenses` folder with the executable when sharing a package. The embedded .NET runtime uses its normal per-user temporary extraction cache on first launch.

## Workflow

1. Choose **File → New YTYP** and enter a name, or **File → Open** a native `.ytyp` / CodeWalker `.ytyp.xml`.
2. Click **Add prop**, or double-click an existing prop to edit it. All fields are on one form, grouped by category. Smaller windows scroll the fields; action buttons stay visible below them.
3. Enter prop/asset names without file extensions. For a new prop, the asset name fills automatically.
4. Choose **Embedded** or **External** textures. For an external YTD, enter its dictionary name without `.ytd`. The embedded option normally sets the dictionary to the asset name, matching CodeWalker's generator.
5. Review LOD distance, flags, special attribute, bounds, and collision references. **Import YDR** / **Load from YDR** reads model bounds, the sphere, and texture/collision hints before you add the prop.
6. Click **Build YTYP** or press **F6** to write a native game file. **Export XML** writes editable CodeWalker XML.

Manual entries start with a **1 × 1 × 1 placeholder bounding box**. Replace these values with your model's actual bounds. The Bounds section calculates a sphere from the box if needed.

## Editing and saving

- Ctrl+N / Ctrl+O: new/open. Ctrl+S: save. Ctrl+Shift+S: save as.
- Ctrl+A: add prop. Ctrl+I: import YDR. Ctrl+E: edit selected prop.
- Ctrl+D: duplicate selected prop. A duplicate keeps the original model/dictionary references.
- Delete: delete selected props, with confirmation.
- Ctrl+Z / Ctrl+Y: undo/redo. F1: help.
- Select multiple rows for deletion or JSON export. Export a prop to get a template for this editor's JSON format; compatibility with Addon Props JSON is not claimed.
- Saves use atomic replacement. An overwritten file's previous version is stored at `<filename>.bak`; only the latest backup is retained.
- Native builds reload their output and check source fields before writing. If a field cannot be preserved, export XML and use CodeWalker for that file.

Floats use a decimal dot. Flags accept unsigned decimal or `0x` hexadecimal. Duplicate hashes, invalid bounds, non-finite numbers, invalid uint values, and missing external texture dictionaries are rejected. Input files are limited to 64 MB.

## Existing files and limits

**Texture mode is inferred.** YTYP stores a dictionary reference rather than an Embedded/External flag. A matching asset/dictionary name, or no dictionary, is shown as Embedded; other references are shown as External. A model can still use an external YTD with a matching name, or mix embedded/external textures. Verify the actual assets. Editing other fields preserves the original reference.

**Hashed names:** existing files without readable names display `hash_XXXXXXXX`. Hashes are preserved. Associated `.ydr`, `.ytd`, `.ydd`, `.yft`, and `.ybn` filenames in the same folder help resolve names. Files built here retain readable names in the metadata string section.

**Time/MLO content:** common fields can be edited; specialised time flags, rooms, portals, entities, entity sets, extensions, dependencies, and composite definitions are retained where CodeWalker supports them. Specialised MLO/time-flag editing is not provided. Unknown XML fields survive XML export; unsupported fields prevent native compilation. XML formatting can change.

**YDR import:** native import supports Legacy version 165. Export Enhanced/other drawables to CodeWalker `.ydr.xml` first. Dictionary fields reference existing assets; the editor does not create, convert, or embed YTD/YBN/model files. No 3D preview, RPF editing, or game installation is included.

40 automated checks passed before packaging, including native round trips, prop field updates, and UI rendering. No in-game GTA V testing or user-supplied YTYP regression test has been performed.

## Credits and terms

The editor's original source is licensed under MIT; its licence is in `licenses/Editor-MIT.txt`. Uses CodeWalker Core by dexyfex, SharpDX, and the .NET runtime. These dependencies retain their own terms and notices in `licenses/`; the editor's MIT licence does not relicense them.
