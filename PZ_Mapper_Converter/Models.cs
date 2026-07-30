namespace PZ_Mapper_Converter;

internal readonly record struct CellCoord(int X, int Y)
{
    public override string ToString() => $"{X}_{Y}";
}

internal readonly record struct LayerKey(int Floor, int Layer);

internal readonly record struct ObjectPoint(int X, int Y);

internal sealed class LotHeaderData
{
    public required int CellX { get; init; }
    public required int CellY { get; init; }
    public required int Version { get; init; }
    public required int ChunkDim { get; init; }
    public required int ChunksPerCell { get; init; }
    public required int CellDim { get; init; }
    public required int MinLevel { get; init; }
    public required int MaxLevel { get; init; }
    public required IReadOnlyList<string> TilesUsed { get; init; }
    public required IReadOnlyList<RoomRect> Rooms { get; init; }
    public required IReadOnlyList<BuildingDef> Buildings { get; init; }
    public required byte[] ZombieDensity { get; init; }

    public int MinSquareX => CellX * CellDim;
    public int MinSquareY => CellY * CellDim;
    public int MaxSquareX => (CellX + 1) * CellDim - 1;
    public int MaxSquareY => (CellY + 1) * CellDim - 1;
}

internal sealed class RoomRect
{
    public required int SourceRoomId { get; init; }
    public required string Name { get; init; }
    public required int Floor { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }

    public int Right => X + Width;
    public int Bottom => Y + Height;
}

internal sealed class BuildingDef
{
    public required int CellX { get; init; }
    public required int CellY { get; init; }
    public required int SourceBuildingId { get; init; }
    public required IReadOnlyList<int> RoomIds { get; init; }
}

internal sealed class TargetCell
{
    public TargetCell(CellCoord coord)
    {
        Coord = coord;
    }

    public CellCoord Coord { get; }
    public Dictionary<LayerKey, Dictionary<int, string>> TilesByLayer { get; } = new();
    public List<RoomRect> Rooms { get; } = new();

    public void AddTile(int localX, int localY, int floor, int layer, string tileName, int targetCellSize)
    {
        var key = new LayerKey(floor, layer);
        if (!TilesByLayer.TryGetValue(key, out var tiles))
        {
            tiles = new Dictionary<int, string>();
            TilesByLayer.Add(key, tiles);
        }

        tiles[localX + localY * targetCellSize] = tileName;
    }
}

internal sealed class MapObject
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int? X { get; init; }
    public int? Y { get; init; }
    public int? Z { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public string? Geometry { get; init; }
    public int? LineWidth { get; init; }
    public IReadOnlyList<ObjectPoint> Points { get; init; } = Array.Empty<ObjectPoint>();
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();

    public bool IsGeometry => !string.IsNullOrWhiteSpace(Geometry) && Points.Count > 0;
    public bool CanWrite => IsGeometry || (X.HasValue && Y.HasValue);
    public int Level => Z ?? 0;

    public CellCoord GetCell(int targetCellSize)
    {
        var anchorX = IsGeometry ? Points[0].X : X ?? 0;
        var anchorY = IsGeometry ? Points[0].Y : Y ?? 0;

        return new CellCoord(
            BinaryHelpers.FloorDiv(anchorX, targetCellSize),
            BinaryHelpers.FloorDiv(anchorY, targetCellSize));
    }
}

internal sealed class ConversionResult
{
    public required int SourceCellCount { get; init; }
    public required int TargetCellCount { get; init; }
    public required int ObjectCount { get; init; }
    public required int ImageCount { get; init; }
    public required int TileImageCount { get; init; }
    public required int TbxCount { get; init; }
    public required int BuildingTbxCount { get; init; }
    public required int TileSetCount { get; init; }
    public required TimeSpan Elapsed { get; init; }
    public required string OutputDirectory { get; init; }
    public required string ProjectFile { get; init; }
}

internal sealed class ConversionProgress
{
    public required string Stage { get; init; }
    public required string Message { get; init; }
    public int? Completed { get; init; }
    public int? Total { get; init; }

    public int? Percent
    {
        get
        {
            if (Completed is null || Total is null || Total <= 0)
            {
                return null;
            }

            return Math.Clamp((int)Math.Round(Completed.Value * 100d / Total.Value), 0, 100);
        }
    }
}
