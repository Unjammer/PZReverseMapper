using System.Text.RegularExpressions;

namespace PZ_Mapper_Converter;

internal static partial class LotHeaderReader
{
    public static IReadOnlyDictionary<CellCoord, LotHeaderData> ReadAll(string inputDirectory)
    {
        var headers = new Dictionary<CellCoord, LotHeaderData>();
        foreach (var file in Directory.EnumerateFiles(inputDirectory, "*.lotheader").OrderBy(f => f))
        {
            var name = Path.GetFileName(file);
            var match = LotHeaderFileNameRegex().Match(name);
            if (!match.Success)
            {
                continue;
            }

            var cellX = int.Parse(match.Groups[1].Value);
            var cellY = int.Parse(match.Groups[2].Value);
            var header = Read(file, cellX, cellY);
            headers[new CellCoord(cellX, cellY)] = header;
        }

        return headers;
    }

    public static LotHeaderData Read(string file, int cellX, int cellY)
    {
        using var reader = new BinaryReader(File.OpenRead(file));
        var hasMagic = BinaryHelpers.TryReadMagic(reader, "LOTH");

        var version = reader.ReadInt32();
        if (version is < 0 or > 1)
        {
            throw new InvalidDataException($"{Path.GetFileName(file)} has unsupported lotheader version {version}");
        }

        var tileCount = reader.ReadInt32();
        var tilesUsed = new List<string>(tileCount);
        for (var i = 0; i < tileCount; i++)
        {
            tilesUsed.Add(BinaryHelpers.ReadLineString(reader).Trim());
        }

        if (version == 0)
        {
            reader.ReadByte();
        }

        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        var minLevel = version == 0 ? 0 : reader.ReadInt32();
        var maxLevel = reader.ReadInt32();

        var chunkDim = width > 0 ? width : (version >= 1 ? 8 : 10);
        var chunksPerCell = chunkDim == 8 ? 32 : 30;
        var cellDim = chunkDim * chunksPerCell;

        var rooms = ReadRooms(reader, cellX, cellY, cellDim);
        var buildings = ReadBuildings(reader, cellX, cellY);
        var zombieDensity = ReadZombieDensity(reader, chunksPerCell);

        return new LotHeaderData
        {
            CellX = cellX,
            CellY = cellY,
            Version = version,
            ChunkDim = chunkDim,
            ChunksPerCell = chunksPerCell,
            CellDim = cellDim,
            MinLevel = minLevel,
            MaxLevel = version == 0 ? maxLevel - 1 : maxLevel,
            TilesUsed = tilesUsed,
            Rooms = rooms,
            Buildings = buildings,
            ZombieDensity = zombieDensity
        };
    }

    private static IReadOnlyList<RoomRect> ReadRooms(BinaryReader reader, int cellX, int cellY, int cellDim)
    {
        var roomCount = reader.ReadInt32();
        var rooms = new List<RoomRect>();

        for (var roomIndex = 0; roomIndex < roomCount; roomIndex++)
        {
            var name = BinaryHelpers.ReadLineString(reader);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "empty";
            }

            var floor = reader.ReadInt32();
            var rectCount = reader.ReadInt32();
            for (var rectIndex = 0; rectIndex < rectCount; rectIndex++)
            {
                var x = reader.ReadInt32();
                var y = reader.ReadInt32();
                var width = reader.ReadInt32();
                var height = reader.ReadInt32();

                rooms.Add(new RoomRect
                {
                    SourceRoomId = roomIndex,
                    Name = SanitizeRoomName(name),
                    Floor = floor,
                    X = x + cellX * cellDim,
                    Y = y + cellY * cellDim,
                    Width = width,
                    Height = height
                });
            }

            var objectCount = reader.ReadInt32();
            for (var i = 0; i < objectCount; i++)
            {
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
            }
        }

        return rooms;
    }

    private static IReadOnlyList<BuildingDef> ReadBuildings(BinaryReader reader, int cellX, int cellY)
    {
        var buildingCount = reader.ReadInt32();
        var buildings = new List<BuildingDef>(buildingCount);
        for (var buildingIndex = 0; buildingIndex < buildingCount; buildingIndex++)
        {
            var roomCount = reader.ReadInt32();
            var roomIds = new List<int>(roomCount);
            for (var roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                roomIds.Add(reader.ReadInt32());
            }

            buildings.Add(new BuildingDef
            {
                CellX = cellX,
                CellY = cellY,
                SourceBuildingId = buildingIndex,
                RoomIds = roomIds
            });
        }

        return buildings;
    }

    private static byte[] ReadZombieDensity(BinaryReader reader, int chunksPerCell)
    {
        var length = chunksPerCell * chunksPerCell;
        var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        return remaining >= length ? reader.ReadBytes(length) : Array.Empty<byte>();
    }

    private static string SanitizeRoomName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return value;
    }

    [GeneratedRegex(@"^(-?\d+)_(-?\d+)\.lotheader$", RegexOptions.IgnoreCase)]
    private static partial Regex LotHeaderFileNameRegex();
}
