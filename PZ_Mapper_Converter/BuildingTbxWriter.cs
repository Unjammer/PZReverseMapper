using System.Text;

namespace PZ_Mapper_Converter;

internal sealed class BuildingTbxWriter
{
    private readonly string _outputDirectory;
    private readonly int _cellSize;
    private readonly IReadOnlyDictionary<CellCoord, TargetCell> _targetCells;

    public BuildingTbxWriter(
        string outputDirectory,
        int cellSize,
        IReadOnlyDictionary<CellCoord, TargetCell> targetCells)
    {
        _outputDirectory = outputDirectory;
        _cellSize = cellSize;
        _targetCells = targetCells;
    }

    public int Write(IEnumerable<LotHeaderData> headers)
    {
        var count = 0;
        foreach (var header in headers.OrderBy(h => h.CellX).ThenBy(h => h.CellY))
        {
            foreach (var building in header.Buildings.OrderBy(b => b.SourceBuildingId))
            {
                var roomIds = building.RoomIds.ToHashSet();
                var rooms = header.Rooms
                    .Where(room => roomIds.Contains(room.SourceRoomId))
                    .Where(room => room.Width > 0 && room.Height > 0)
                    .ToArray();

                if (rooms.Length == 0)
                {
                    continue;
                }

                WriteBuilding(header, building, rooms);
                count++;
            }
        }

        return count;
    }

    private void WriteBuilding(LotHeaderData header, BuildingDef building, IReadOnlyList<RoomRect> rooms)
    {
        var bounds = GetBounds(rooms);
        var roomGroups = rooms
            .GroupBy(room => room.SourceRoomId)
            .OrderBy(group => group.Min(room => room.Floor))
            .ThenBy(group => group.Key)
            .ToArray();

        var roomIndexById = roomGroups
            .Select((group, index) => new { group.Key, Index = index + 1 })
            .ToDictionary(item => item.Key, item => item.Index);

        var userTiles = CollectUserTiles(bounds);
        var userTileIds = userTiles
            .Select((tile, index) => new { tile, id = index + 1 })
            .ToDictionary(item => item.tile, item => item.id, StringComparer.Ordinal);

        var floors = rooms.Select(room => room.Floor)
            .Concat(GetFloorsWithTiles(bounds))
            .Distinct()
            .OrderBy(floor => floor)
            .ToArray();

        var sourceCellName = $"{header.CellX}_{header.CellY}";
        var buildingName = roomGroups.First().First().Name;
        var safeName = SanitizeFileName(buildingName);
        var directory = Path.Combine(_outputDirectory, "tbx_buildings", sourceCellName);
        Directory.CreateDirectory(directory);

        var fileName = $"{sourceCellName}_b{building.SourceBuildingId:D4}_{safeName}_{bounds.MinX}_{bounds.MinY}.tbx";
        var file = Path.Combine(directory, fileName);

        using var writer = new StreamWriter(file, false, Encoding.UTF8);
        writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        writer.WriteLine($"<building version=\"4\" width=\"{bounds.Width}\" height=\"{bounds.Height}\" ExteriorWall=\"0\" ExteriorWallTrim=\"0\" Door=\"0\" DoorFrame=\"0\" Window=\"0\" Curtains=\"0\" Shutters=\"0\" Stairs=\"0\" RoofCap=\"0\" RoofSlope=\"0\" RoofTop=\"0\" GrimeWall=\"0\">");
        writer.WriteLine("<properties>");
        writer.WriteLine("<property name=\"Legend\" value=\"Residential\"/>");
        writer.WriteLine($"<property name=\"WorldX\" value=\"{bounds.MinX}\"/>");
        writer.WriteLine($"<property name=\"WorldY\" value=\"{bounds.MinY}\"/>");
        writer.WriteLine($"<property name=\"SourceCell\" value=\"{sourceCellName}\"/>");
        writer.WriteLine($"<property name=\"SourceBuilding\" value=\"{building.SourceBuildingId}\"/>");
        writer.WriteLine("</properties>");
        writer.WriteLine(" <user_tiles>");
        foreach (var tile in userTiles)
        {
            writer.WriteLine($"  <tile tile=\"{XmlEscape(tile)}\"/>");
        }
        writer.WriteLine(" </user_tiles>");
        writer.WriteLine(" <used_tiles></used_tiles>");
        writer.WriteLine(" <used_furniture></used_furniture>");

        foreach (var group in roomGroups)
        {
            var room = group.First();
            writer.WriteLine($" <room Name=\"{XmlEscape(room.Name)}\" InternalName=\"{XmlEscape(room.Name)}\" Color=\"{GetRoomColor(room)}\" InteriorWall=\"0\" InteriorWallTrim=\"0\" Floor=\"0\" GrimeFloor=\"0\" GrimeWall=\"0\"/>");
        }

        foreach (var floor in floors)
        {
            writer.WriteLine(" <floor>");
            writer.WriteLine("  <rooms>");
            writer.WriteLine(BuildRoomCsv(bounds, rooms.Where(room => room.Floor == floor).ToArray(), roomIndexById));
            writer.WriteLine("  </rooms>");
            WriteTileLayers(writer, bounds, floor, userTileIds);
            writer.WriteLine(" </floor>");
        }

        writer.WriteLine("</building>");
    }

    private BuildingBounds GetBounds(IReadOnlyList<RoomRect> rooms)
    {
        var minX = rooms.Min(room => room.X) - 1;
        var minY = rooms.Min(room => room.Y) - 1;
        var maxX = rooms.Max(room => room.Right) + 1;
        var maxY = rooms.Max(room => room.Bottom) + 1;
        return new BuildingBounds(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
    }

    private List<string> CollectUserTiles(BuildingBounds bounds)
    {
        var userTiles = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var key in GetAllLayerKeys())
        {
            for (var y = 0; y <= bounds.Height; y++)
            {
                for (var x = 0; x <= bounds.Width; x++)
                {
                    if (TryGetTile(bounds.MinX + x, bounds.MinY + y, key.Floor, key.Layer, out var tileName) && seen.Add(tileName))
                    {
                        userTiles.Add(tileName);
                    }
                }
            }
        }

        return userTiles;
    }

    private IEnumerable<int> GetFloorsWithTiles(BuildingBounds bounds)
    {
        foreach (var floor in GetAllLayerKeys().Select(key => key.Floor).Distinct())
        {
            foreach (var layer in GetLayers(floor))
            {
                if (HasAnyTile(bounds, floor, layer))
                {
                    yield return floor;
                    break;
                }
            }
        }
    }

    private void WriteTileLayers(
        StreamWriter writer,
        BuildingBounds bounds,
        int floor,
        IReadOnlyDictionary<string, int> userTileIds)
    {
        foreach (var layer in GetLayers(floor))
        {
            if (!HasAnyTile(bounds, floor, layer))
            {
                continue;
            }

            writer.WriteLine($"  <tiles layer=\"{GetTbxLayerName(layer - 1)}\">");
            writer.WriteLine(BuildTileCsv(bounds, floor, layer, userTileIds));
            writer.WriteLine("  </tiles>");
        }
    }

    private string BuildRoomCsv(
        BuildingBounds bounds,
        IReadOnlyList<RoomRect> floorRooms,
        IReadOnlyDictionary<int, int> roomIndexById)
    {
        return BuildCsv(bounds.Width, bounds.Height, (x, y) =>
        {
            var worldX = bounds.MinX + x;
            var worldY = bounds.MinY + y;
            foreach (var room in floorRooms)
            {
                if (worldX >= room.X && worldX < room.Right && worldY >= room.Y && worldY < room.Bottom)
                {
                    return roomIndexById[room.SourceRoomId];
                }
            }

            return 0;
        });
    }

    private string BuildTileCsv(
        BuildingBounds bounds,
        int floor,
        int layer,
        IReadOnlyDictionary<string, int> userTileIds)
    {
        return BuildCsv(bounds.Width + 1, bounds.Height + 1, (x, y) =>
        {
            return TryGetTile(bounds.MinX + x, bounds.MinY + y, floor, layer, out var tileName)
                && userTileIds.TryGetValue(tileName, out var id)
                    ? id
                    : 0;
        });
    }

    private bool HasAnyTile(BuildingBounds bounds, int floor, int layer)
    {
        for (var y = 0; y <= bounds.Height; y++)
        {
            for (var x = 0; x <= bounds.Width; x++)
            {
                if (TryGetTile(bounds.MinX + x, bounds.MinY + y, floor, layer, out _))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryGetTile(int worldX, int worldY, int floor, int layer, out string tileName)
    {
        tileName = string.Empty;
        var cellCoord = new CellCoord(
            BinaryHelpers.FloorDiv(worldX, _cellSize),
            BinaryHelpers.FloorDiv(worldY, _cellSize));

        if (!_targetCells.TryGetValue(cellCoord, out var cell))
        {
            return false;
        }

        var key = new LayerKey(floor, layer);
        if (!cell.TilesByLayer.TryGetValue(key, out var tiles))
        {
            return false;
        }

        var localX = BinaryHelpers.PositiveMod(worldX, _cellSize);
        var localY = BinaryHelpers.PositiveMod(worldY, _cellSize);
        return tiles.TryGetValue(localX + localY * _cellSize, out tileName!)
            && TbxTileFilter.ShouldInclude(tileName);
    }

    private IEnumerable<LayerKey> GetAllLayerKeys()
    {
        return _targetCells.Values
            .SelectMany(cell => cell.TilesByLayer.Keys)
            .Distinct()
            .OrderBy(key => key.Floor)
            .ThenBy(key => key.Layer);
    }

    private IEnumerable<int> GetLayers(int floor)
    {
        return GetAllLayerKeys()
            .Where(key => key.Floor == floor)
            .Select(key => key.Layer)
            .Distinct()
            .OrderBy(layer => layer);
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
        var hash = HashCode.Combine(room.Name, room.SourceRoomId, room.Floor);
        var r = 64 + Math.Abs(hash & 0x7f);
        var g = 64 + Math.Abs((hash >> 8) & 0x7f);
        var b = 64 + Math.Abs((hash >> 16) & 0x7f);
        return $"{r} {g} {b}";
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(value) ? "building" : value;
    }

    private static string XmlEscape(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private readonly record struct BuildingBounds(int MinX, int MinY, int Width, int Height);
}
