using System.Text;

namespace PZ_Mapper_Converter;

internal sealed class TileSetCatalog
{
    private readonly Dictionary<string, TileSetDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _observedMaxTile = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _definitionOrder = new();
    private bool _built;

    public IReadOnlyCollection<TileSetDefinition> TileSets => _definitions.Values
        .Where(d => d.FirstGid > 0)
        .OrderBy(d => d.FirstGid)
        .ToArray();

    public void LoadTilesPath(string? path, bool overrideExisting = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (File.Exists(path))
        {
            if (string.Equals(Path.GetExtension(path), ".tiles", StringComparison.OrdinalIgnoreCase))
            {
                LoadTilesFile(path, overrideExisting);
            }

            return;
        }

        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Tiles path not found: {path}");
        }

        foreach (var file in Directory.EnumerateFiles(path, "*.tiles", SearchOption.AllDirectories).OrderBy(f => f))
        {
            LoadTilesFile(file, overrideExisting);
        }
    }

    public void Observe(string tileName)
    {
        if (!TrySplitTileName(tileName, out var sheetName, out var localId))
        {
            return;
        }

        if (!_observedMaxTile.TryGetValue(sheetName, out var current) || localId > current)
        {
            _observedMaxTile[sheetName] = localId;
        }
    }

    public void Build()
    {
        var firstGid = 1;
        foreach (var sheetName in _definitionOrder.Where(_observedMaxTile.ContainsKey))
        {
            firstGid = AssignFirstGid(sheetName, firstGid);
        }

        foreach (var sheetName in _observedMaxTile.Keys.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            if (_definitions.TryGetValue(sheetName, out var existing) && existing.FirstGid > 0)
            {
                continue;
            }

            firstGid = AssignFirstGid(sheetName, firstGid);
        }

        _built = true;
    }

    public int GetGid(string tileName)
    {
        if (!_built)
        {
            throw new InvalidOperationException("Tileset catalog must be built before gid lookup.");
        }

        if (!TrySplitTileName(tileName, out var sheetName, out var localId))
        {
            return 0;
        }

        return _definitions.TryGetValue(sheetName, out var def) ? def.FirstGid + localId : 0;
    }

    private int AssignFirstGid(string sheetName, int firstGid)
    {
        if (!_definitions.TryGetValue(sheetName, out var def))
        {
            var maxTile = _observedMaxTile.GetValueOrDefault(sheetName);
            def = new TileSetDefinition
            {
                Name = sheetName,
                Width = 8,
                Height = Math.Max(1, (maxTile + 8) / 8)
            };
            _definitions.Add(sheetName, def);
        }

        var observedMax = _observedMaxTile.GetValueOrDefault(sheetName, def.Width * def.Height - 1);
        if (observedMax >= def.Width * def.Height)
        {
            def.Height = Math.Max(def.Height, (observedMax + def.Width) / def.Width);
        }

        def.FirstGid = firstGid;
        return firstGid + def.Width * def.Height;
    }

    private void LoadTilesFile(string file, bool overrideExisting)
    {
        try
        {
            using var reader = new BinaryReader(File.OpenRead(file));
            reader.ReadInt32();
            reader.ReadInt32();
            var sheetCount = reader.ReadInt32();

            for (var sheet = 0; sheet < sheetCount; sheet++)
            {
                _ = BinaryHelpers.ReadLineString(reader);
                var imageFile = BinaryHelpers.ReadLineString(reader);
                var width = reader.ReadInt32();
                var height = reader.ReadInt32();
                reader.ReadInt32();
                var tileCount = reader.ReadInt32();

                var name = Path.GetFileNameWithoutExtension(imageFile);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    if (_definitions.TryGetValue(name, out var existing))
                    {
                        if (overrideExisting)
                        {
                            existing.Width = width;
                            existing.Height = height;
                        }
                    }
                    else
                    {
                        _definitions.Add(name, new TileSetDefinition
                        {
                            Name = name,
                            Width = width,
                            Height = height
                        });
                    }

                    if (!_definitionOrder.Any(sheetName => string.Equals(sheetName, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        _definitionOrder.Add(name);
                    }
                }

                for (var tile = 0; tile < tileCount; tile++)
                {
                    var propertyCount = reader.ReadInt32();
                    for (var prop = 0; prop < propertyCount; prop++)
                    {
                        _ = BinaryHelpers.ReadLineString(reader);
                        _ = BinaryHelpers.ReadLineString(reader);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: failed to parse tiles file {file}: {ex.Message}");
        }
    }

    private static bool TrySplitTileName(string tileName, out string sheetName, out int localId)
    {
        sheetName = string.Empty;
        localId = 0;

        var index = tileName.LastIndexOf('_');
        if (index <= 0 || index == tileName.Length - 1)
        {
            return false;
        }

        if (!int.TryParse(tileName[(index + 1)..], out localId))
        {
            return false;
        }

        sheetName = tileName[..index];
        return true;
    }
}

internal sealed class TileSetDefinition
{
    public required string Name { get; init; }
    public required int Width { get; set; }
    public required int Height { get; set; }
    public int FirstGid { get; set; }
}
