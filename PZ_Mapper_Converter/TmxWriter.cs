using System.Text;

namespace PZ_Mapper_Converter;

internal sealed class TmxWriter
{
    private readonly string _outputDirectory;
    private readonly int _cellSize;
    private readonly TileSetCatalog _tileSets;
    private readonly bool _exportRoomTbx;
    private readonly bool _useLevelAttributes;

    public TmxWriter(string outputDirectory, int cellSize, TileSetCatalog tileSets, bool exportRoomTbx = true, bool useLevelAttributes = false)
    {
        _outputDirectory = outputDirectory;
        _cellSize = cellSize;
        _tileSets = tileSets;
        _exportRoomTbx = exportRoomTbx;
        _useLevelAttributes = useLevelAttributes;
    }

    public int TbxCount { get; private set; }

    public void Write(TargetCell cell)
    {
        var tmxDirectory = Path.Combine(_outputDirectory, "tmx");
        Directory.CreateDirectory(tmxDirectory);

        var file = Path.Combine(tmxDirectory, $"{cell.Coord.X}_{cell.Coord.Y}.tmx");
        var useLevelAttributes = _useLevelAttributes || ShouldUseLevelAttributes(cell);
        using var writer = new StreamWriter(file, false, Encoding.UTF8);

        writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        writer.WriteLine($"<map version=\"{(useLevelAttributes ? "2.0" : "1.0")}\" orientation=\"levelisometric\" width=\"{_cellSize}\" height=\"{_cellSize}\" tilewidth=\"64\" tileheight=\"32\">");
        WriteTileSets(writer);
        WriteLayers(writer, cell, useLevelAttributes);
        WriteRoomDefs(writer, cell, useLevelAttributes);
        writer.WriteLine("</map>");
    }

    public void WriteRoomTbxOnly(TargetCell cell)
    {
        if (!_exportRoomTbx)
        {
            return;
        }

        foreach (var floorGroup in GetRoomsByFloor(cell))
        {
            var counter = 0;
            foreach (var room in floorGroup)
            {
                WriteTbx(cell, room, counter++);
            }
        }
    }

    private void WriteTileSets(StreamWriter writer)
    {
        foreach (var tileSet in _tileSets.TileSets)
        {
            var name = XmlEscape(tileSet.Name);
            writer.WriteLine($" <tileset firstgid=\"{tileSet.FirstGid}\" name=\"{name}\" tilewidth=\"64\" tileheight=\"128\">");
            writer.WriteLine($"  <image source=\"../../../Tiles/{name}.png\" width=\"{64 * tileSet.Width}\" height=\"{128 * tileSet.Height}\"/>");
            writer.WriteLine(" </tileset>");
        }
    }

    private void WriteLayers(StreamWriter writer, TargetCell cell, bool useLevelAttributes)
    {
        foreach (var (key, tiles) in cell.TilesByLayer.OrderBy(k => k.Key.Floor).ThenBy(k => k.Key.Layer))
        {
            var layerName = GetLayerName(key.Floor, key.Layer);
            writer.WriteLine($" <layer name=\"{GetTmxLayerName(key.Floor, layerName, useLevelAttributes)}\"{GetLevelAttribute(key.Floor, useLevelAttributes)} width=\"{_cellSize}\" height=\"{_cellSize}\">");
            writer.WriteLine("  <data encoding=\"csv\">");
            writer.WriteLine(BuildCsv(tiles));
            writer.WriteLine("  </data>");
            writer.WriteLine(" </layer>");
        }
    }

    private string BuildCsv(Dictionary<int, string> tiles)
    {
        var builder = new StringBuilder(_cellSize * _cellSize * 3);
        var first = true;

        for (var y = 0; y < _cellSize; y++)
        {
            for (var x = 0; x < _cellSize; x++)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                var index = x + y * _cellSize;
                builder.Append(tiles.TryGetValue(index, out var tileName) ? _tileSets.GetGid(tileName) : 0);
                first = false;
            }

            if (y < _cellSize - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private void WriteRoomDefs(StreamWriter writer, TargetCell cell, bool useLevelAttributes)
    {
        foreach (var floorGroup in GetRoomsByFloor(cell))
        {
            writer.WriteLine($" <objectgroup name=\"{GetRoomDefsGroupName(floorGroup.Key, useLevelAttributes)}\"{GetLevelAttribute(floorGroup.Key, useLevelAttributes)} width=\"{_cellSize}\" height=\"{_cellSize}\">");
            var counter = 0;
            foreach (var room in floorGroup)
            {
                var type = _exportRoomTbx ? XmlEscape(WriteTbx(cell, room, counter++)) : string.Empty;
                var name = XmlEscape(room.Name);
                writer.WriteLine($"  <object name=\"{name}\" type=\"{type}\" x=\"{room.X * 64}\" y=\"{room.Y * 32}\" width=\"{room.Width * 64}\" height=\"{room.Height * 32}\"/>");
            }
            writer.WriteLine(" </objectgroup>");
        }
    }

    private IEnumerable<IGrouping<int, RoomRect>> GetRoomsByFloor(TargetCell cell)
    {
        return cell.Rooms
            .Where(r => r.Width > 0 && r.Height > 0)
            .GroupBy(r => r.Floor)
            .OrderBy(g => g.Key);
    }

    private string WriteTbx(TargetCell cell, RoomRect room, int counter)
    {
        var cellName = $"{cell.Coord.X}_{cell.Coord.Y}";
        var tbxDirectory = Path.Combine(_outputDirectory, "tmx", "tbx", cellName);
        Directory.CreateDirectory(tbxDirectory);

        var safeRoomName = SanitizeFileName(room.Name);
        var fileName = $"{cellName}_{room.Floor}_{safeRoomName}_{room.X}_{room.Y}_{counter}.tbx";
        var fullPath = Path.Combine(tbxDirectory, fileName);
        var userTiles = CollectUserTiles(cell, room);
        var userTileIds = userTiles
            .Select((tile, index) => new { tile, id = index + 1 })
            .ToDictionary(item => item.tile, item => item.id, StringComparer.Ordinal);
        var floors = GetFloorsWithTiles(cell, room)
            .Distinct()
            .DefaultIfEmpty(room.Floor)
            .OrderBy(floor => floor)
            .ToArray();

        using var writer = new StreamWriter(fullPath, false, Encoding.UTF8);
        writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        writer.WriteLine($"<building version=\"4\" width=\"{room.Width}\" height=\"{room.Height}\" ExteriorWall=\"0\" ExteriorWallTrim=\"0\" Door=\"0\" DoorFrame=\"0\" Window=\"0\" Curtains=\"0\" Shutters=\"0\" Stairs=\"0\" RoofCap=\"0\" RoofSlope=\"0\" RoofTop=\"0\" GrimeWall=\"0\">");
        writer.WriteLine("<properties>");
        writer.WriteLine("<property name=\"Legend\" value=\"Residential\"/>");
        writer.WriteLine("</properties>");
        writer.WriteLine(" <user_tiles>");
        foreach (var tile in userTiles)
        {
            writer.WriteLine($"  <tile tile=\"{XmlEscape(tile)}\"/>");
        }
        writer.WriteLine(" </user_tiles>");
        writer.WriteLine(" <used_tiles></used_tiles>");
        writer.WriteLine(" <used_furniture></used_furniture>");
        writer.WriteLine($" <room Name=\"{room.Floor}_{XmlEscape(room.Name)}\" InternalName=\"{XmlEscape(room.Name)}\" Color=\"{GetRoomColor(room)}\" InteriorWall=\"0\" InteriorWallTrim=\"0\" Floor=\"0\" GrimeFloor=\"0\" GrimeWall=\"0\"/>");

        foreach (var floor in floors)
        {
            writer.WriteLine(" <floor>");
            writer.WriteLine("  <rooms>");
            writer.WriteLine(BuildRoomCsv(room.Width, room.Height, floor == room.Floor ? 1 : 0));
            writer.WriteLine("  </rooms>");
            WriteTbxTileLayers(writer, cell, room, floor, userTileIds);
            writer.WriteLine(" </floor>");
        }

        writer.WriteLine("</building>");

        TbxCount++;
        return $@".\tbx\{cellName}\{fileName}";
    }

    private List<string> CollectUserTiles(TargetCell cell, RoomRect room)
    {
        var userTiles = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (_, tiles) in cell.TilesByLayer.OrderBy(pair => pair.Key.Floor).ThenBy(pair => pair.Key.Layer))
        {
            for (var y = 0; y <= room.Height; y++)
            {
                for (var x = 0; x <= room.Width; x++)
                {
                    if (TryGetTile(tiles, room.X + x, room.Y + y, out var tileName) && seen.Add(tileName))
                    {
                        userTiles.Add(tileName);
                    }
                }
            }
        }

        return userTiles;
    }

    private IEnumerable<int> GetFloorsWithTiles(TargetCell cell, RoomRect room)
    {
        foreach (var (key, tiles) in cell.TilesByLayer)
        {
            if (HasAnyTileInTileArea(tiles, room))
            {
                yield return key.Floor;
            }
        }
    }

    private bool HasAnyTileInTileArea(Dictionary<int, string> tiles, RoomRect room)
    {
        for (var y = 0; y <= room.Height; y++)
        {
            for (var x = 0; x <= room.Width; x++)
            {
                if (TryGetTile(tiles, room.X + x, room.Y + y, out _))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void WriteTbxTileLayers(
        StreamWriter writer,
        TargetCell cell,
        RoomRect room,
        int floor,
        IReadOnlyDictionary<string, int> userTileIds)
    {
        foreach (var (key, tiles) in cell.TilesByLayer
                     .Where(pair => pair.Key.Floor == floor)
                     .OrderBy(pair => pair.Key.Layer))
        {
            if (!HasAnyTileInTileArea(tiles, room))
            {
                continue;
            }

            writer.WriteLine($"  <tiles layer=\"{GetTbxLayerName(key.Layer - 1)}\">");
            writer.WriteLine(BuildTileCsv(room, tiles, userTileIds));
            writer.WriteLine("  </tiles>");
        }
    }

    private string BuildTileCsv(RoomRect room, Dictionary<int, string> tiles, IReadOnlyDictionary<string, int> userTileIds)
    {
        return BuildCsv(room.Width + 1, room.Height + 1, (x, y) =>
        {
            return TryGetTile(tiles, room.X + x, room.Y + y, out var tileName) && userTileIds.TryGetValue(tileName, out var id)
                ? id
                : 0;
        });
    }

    private bool TryGetTile(Dictionary<int, string> tiles, int localX, int localY, out string tileName)
    {
        tileName = string.Empty;
        if (localX < 0 || localY < 0 || localX >= _cellSize || localY >= _cellSize)
        {
            return false;
        }

        return tiles.TryGetValue(localX + localY * _cellSize, out tileName!)
            && TbxTileFilter.ShouldInclude(tileName);
    }

    private static string BuildRoomCsv(int width, int height, int value)
    {
        return BuildCsv(width, height, (_, _) => value);
    }

    private static string BuildCsv(int width, int height, Func<int, int, int> valueAt)
    {
        var builder = new StringBuilder(width * height * 2);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                builder.Append(valueAt(x, y));
                if (x != width - 1 || y != height - 1)
                {
                    builder.Append(',');
                }
            }

            if (y < height - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private static string GetTbxLayerName(int layer)
    {
        return layer switch
        {
            0 => "Floor",
            1 => "FloorOverlay",
            2 => "FloorGrime",
            3 => "FloorGrime2",
            4 => "FloorFurniture",
            5 => "Vegetation",
            6 => "Walls",
            7 => "WallTrim",
            8 => "Walls2",
            9 => "WallTrim2",
            10 => "RoofCap",
            11 => "RoofCap2",
            12 => "WallOverlay",
            13 => "WallOverlay2",
            14 => "WallGrime",
            15 => "WallGrime2",
            16 => "WallFurniture",
            17 => "WallFurniture2",
            18 => "Frames",
            19 => "Doors",
            20 => "Windows",
            21 => "Curtains",
            22 => "Furniture",
            23 => "Furniture2",
            24 => "Furniture3",
            25 => "Furniture4",
            26 => "Curtains2",
            27 => "WallFurniture3",
            28 => "WallFurniture4",
            29 => "WallOverlay3",
            30 => "WallOverlay4",
            31 => "Roof",
            32 => "Roof2",
            33 => "RoofTop",
            _ => $"Custom_{layer}"
        };
    }

    private static string GetRoomColor(RoomRect room)
    {
        var hash = HashCode.Combine(room.Name, room.Floor, room.Width, room.Height);
        var r = 64 + Math.Abs(hash & 0x7f);
        var g = 64 + Math.Abs((hash >> 8) & 0x7f);
        var b = 64 + Math.Abs((hash >> 16) & 0x7f);
        return $"{r} {g} {b}";
    }

    private static string GetLayerName(int floor, int layer)
    {
        if (floor <= 0 && layer == 1)
        {
            return "Floor";
        }

        if (floor == 0 && layer == 6)
        {
            return "Vegetation";
        }

        if ((floor <= 0 && layer == 2) || (floor > 0 && layer == 1))
        {
            return "FloorOverlay";
        }

        return $"FloorOverlay{layer - 1}";
    }

    private static bool ShouldUseLevelAttributes(TargetCell cell) =>
        cell.TilesByLayer.Keys.Any(key => key.Floor < 0)
        || cell.Rooms.Any(room => room.Floor < 0);

    private static string GetTmxLayerName(int floor, string layerName, bool useLevelAttributes) =>
        useLevelAttributes ? layerName : $"{floor}_{layerName}";

    private static string GetRoomDefsGroupName(int floor, bool useLevelAttributes) =>
        useLevelAttributes ? "RoomDefs" : $"{floor}_RoomDefs";

    private static string GetLevelAttribute(int floor, bool useLevelAttributes) =>
        useLevelAttributes ? $" level=\"{floor}\"" : string.Empty;

    private static string SanitizeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return value;
    }

    private static string XmlEscape(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("\"", "&quot;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
