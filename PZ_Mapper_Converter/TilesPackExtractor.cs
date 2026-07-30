using System.Text;

namespace PZ_Mapper_Converter;

internal static class TilesPackExtractor
{
    private const int NewPackMagic = 1263557200;
    private const int TilesPerRow = 8;

    public static TilesPackExtractionResult Extract(string? tilesPath, string outputDirectory)
    {
        return Extract(new[] { tilesPath }, outputDirectory);
    }

    public static TilesPackExtractionResult Extract(IEnumerable<string?> tilesPaths, string outputDirectory)
    {
        var paths = tilesPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paths.Length == 0)
        {
            return TilesPackExtractionResult.Empty;
        }

        var packFiles = paths
            .SelectMany(path => ResolveFiles(path, "*.pack", "texturepacks", "texturespack", "texturepack"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (packFiles.Length == 0)
        {
            return TilesPackExtractionResult.Empty;
        }

        var rawRoot = Path.Combine(outputDirectory, "TilesRaw");
        var singleRoot = Path.Combine(outputDirectory, "TilesSingle");
        var tilesRoot = Path.Combine(outputDirectory, "Tiles");
        Directory.CreateDirectory(rawRoot);
        Directory.CreateDirectory(singleRoot);
        Directory.CreateDirectory(tilesRoot);

        var packedTiles = new List<PackedTile>();
        var usedPackDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedRawPackDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var logicalPackDirectories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rawImageCount = 0;
        foreach (var packFile in packFiles)
        {
            var logicalPackIdentity = GetLogicalPackIdentity(packFile);
            if (!logicalPackDirectories.TryGetValue(logicalPackIdentity, out var packDirectoryName))
            {
                packDirectoryName = ReserveUniqueDirectoryName(
                    GetLogicalPackFileName(packFile),
                    usedPackDirectoryNames);
                logicalPackDirectories.Add(logicalPackIdentity, packDirectoryName);
            }

            var rawPackDirectoryName = ReserveUniqueDirectoryName(
                Path.GetFileName(packFile),
                usedRawPackDirectoryNames);
            rawImageCount += ExtractPack(
                packFile,
                rawPackDirectoryName,
                packDirectoryName,
                rawRoot,
                packedTiles);
        }

        var parsedTiles = ParsePackedTiles(packedTiles);
        var individualTileCount = ExtractIndividualTiles(parsedTiles, rawRoot, singleRoot);
        var tileSheetCount = ReconstructTileSheets(parsedTiles, singleRoot, tilesRoot);

        return new TilesPackExtractionResult(rawImageCount, individualTileCount, tileSheetCount);
    }

    private static IEnumerable<string> ResolveFiles(string path, string pattern, params string[] preferredChildren)
    {
        if (File.Exists(path))
        {
            if (string.Equals(Path.GetExtension(path), pattern.TrimStart('*'), StringComparison.OrdinalIgnoreCase))
            {
                yield return Path.GetFullPath(path);
            }

            yield break;
        }

        if (!Directory.Exists(path))
        {
            yield break;
        }

        var childRoots = preferredChildren
            .Where(child => !string.IsNullOrWhiteSpace(child))
            .Select(child => Path.Combine(path, child))
            .Where(Directory.Exists)
            .ToArray();
        var searchRoots = childRoots.Length > 0 ? childRoots : new[] { path };

        foreach (var file in searchRoots
            .SelectMany(root => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
        {
            yield return file;
        }
    }

    private static int ExtractPack(
        string packFile,
        string rawPackDirectoryName,
        string packDirectoryName,
        string rawRoot,
        ICollection<PackedTile> packedTiles)
    {
        var written = 0;
        using var reader = new BinaryReader(File.OpenRead(packFile), Encoding.UTF8, leaveOpen: false);
        var sheetCount = reader.ReadInt32();
        var newFormat = false;
        if (sheetCount == NewPackMagic)
        {
            reader.ReadInt32();
            sheetCount = reader.ReadInt32();
            newFormat = true;
        }

        var packOutputDirectory = Path.Combine(rawRoot, rawPackDirectoryName);
        Directory.CreateDirectory(packOutputDirectory);

        for (var sheet = 0; sheet < sheetCount && reader.BaseStream.Position < reader.BaseStream.Length; sheet++)
        {
            var atlasName = ReadPackString(reader);
            var tileCount = reader.ReadInt32();
            reader.ReadInt32();

            for (var tile = 0; tile < tileCount && reader.BaseStream.Position < reader.BaseStream.Length; tile++)
            {
                var tileNameLength = reader.ReadInt32();
                if (tileNameLength == 0)
                {
                    break;
                }

                var tileName = ReadPackString(reader, tileNameLength);
                packedTiles.Add(new PackedTile(
                    tileName,
                    atlasName,
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    rawPackDirectoryName,
                    packDirectoryName));
            }

            if (TryExtractImage(reader, newFormat, out var extension, out var imageBytes))
            {
                var rawFile = Path.Combine(packOutputDirectory, $"{SanitizeFileName(atlasName)}{extension}");
                File.WriteAllBytes(rawFile, imageBytes);
                written++;
            }
        }

        return written;
    }

    private static string GetLogicalPackIdentity(string packFile)
    {
        return Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(packFile)) ?? string.Empty,
            GetLogicalPackFileName(packFile));
    }

    private static string GetLogicalPackFileName(string packFile)
    {
        const string floorSuffix = ".floor.pack";
        var fileName = Path.GetFileName(packFile);
        return fileName.EndsWith(floorSuffix, StringComparison.OrdinalIgnoreCase)
            ? $"{fileName[..^floorSuffix.Length]}.pack"
            : fileName;
    }

    private static bool TryExtractImage(BinaryReader reader, bool newFormat, out string extension, out byte[] imageBytes)
    {
        extension = string.Empty;
        imageBytes = Array.Empty<byte>();

        if (newFormat)
        {
            if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
            {
                return false;
            }

            var dataLength = reader.ReadInt32();
            if (dataLength <= 0 || reader.BaseStream.Position + dataLength > reader.BaseStream.Length)
            {
                return false;
            }

            var start = reader.BaseStream.Position;
            var kind = PeekImageKind(reader);
            reader.BaseStream.Position = start;
            imageBytes = reader.ReadBytes(dataLength);
            extension = kind == PackImageKind.Dds ? ".dds" : ".png";
            return kind != PackImageKind.Unknown;
        }

        var imageKind = PeekImageKind(reader);
        if (imageKind == PackImageKind.Png)
        {
            imageBytes = ReadPngBytes(reader);
            SkipUntilSentinel(reader);
            extension = ".png";
            return true;
        }

        if (imageKind == PackImageKind.Dds)
        {
            imageBytes = ReadDdsBytes(reader);
            extension = ".dds";
            return true;
        }

        return false;
    }

    private static ParsedPackedTile[] ParsePackedTiles(IReadOnlyCollection<PackedTile> packedTiles)
    {
        return packedTiles
            .Select((tile, order) => TryParseTileName(tile.TileName, out var sheetName, out var tileId)
                ? new ParsedPackedTile(tile, sheetName, tileId, order)
                : (ParsedPackedTile?)null)
            .Where(item => item.HasValue
                && item.Value.TileId >= 0
                && item.Value.Tile.TileWidth > 0
                && item.Value.Tile.TileHeight > 0)
            .Select(item => item!.Value)
            .ToArray();
    }

    private static int ExtractIndividualTiles(
        IReadOnlyCollection<ParsedPackedTile> parsedTiles,
        string rawRoot,
        string singleRoot)
    {
        var written = 0;
        var createdDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var atlasGroup in parsedTiles
            .OrderBy(item => item.Order)
            .GroupBy(item => (item.Tile.RawPackDirectoryName, item.Tile.AtlasName)))
        {
            var atlasPath = Path.Combine(
                rawRoot,
                atlasGroup.Key.RawPackDirectoryName,
                $"{SanitizeFileName(atlasGroup.Key.AtlasName)}.png");
            if (!File.Exists(atlasPath))
            {
                continue;
            }

            PngImage atlas;
            try
            {
                atlas = PngImage.Load(atlasPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: failed to load atlas {atlasPath}: {ex.Message}");
                continue;
            }

            foreach (var item in atlasGroup.OrderBy(item => item.Order))
            {
                var tile = item.Tile;
                var singleTile = new PngImage(tile.TileWidth, tile.TileHeight);
                singleTile.Clear(Rgba.Transparent);
                singleTile.DrawImage(
                    atlas,
                    tile.X,
                    tile.Y,
                    tile.Width,
                    tile.Height,
                    tile.OffsetX,
                    tile.OffsetY);

                var tileDirectory = Path.Combine(
                    singleRoot,
                    tile.PackDirectoryName,
                    SanitizeFileName(item.SheetName));
                if (createdDirectories.Add(tileDirectory))
                {
                    Directory.CreateDirectory(tileDirectory);
                }

                singleTile.Save(Path.Combine(
                    tileDirectory,
                    $"{SanitizeFileName(tile.TileName)}.png"));
                written++;
            }
        }

        return written;
    }

    private static int ReconstructTileSheets(
        IReadOnlyCollection<ParsedPackedTile> parsedTiles,
        string singleRoot,
        string tilesRoot)
    {
        var byPackAndSheet = parsedTiles
            .GroupBy(
                item => (item.Tile.PackDirectoryName, item.SheetName),
                PackSheetKeyComparer.Instance)
            .Select(group => new
            {
                group.Key.PackDirectoryName,
                group.Key.SheetName,
                Tiles = group.OrderBy(item => item.Order).ToArray()
            })
            .OrderBy(group => group.PackDirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.SheetName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var written = 0;
        var sheetsWithMultipleSizes = 0;
        foreach (var group in byPackAndSheet)
        {
            var candidates = group.Tiles;
            var selectedCellSize = new SheetCellSize(
                candidates[^1].Tile.TileWidth,
                candidates[^1].Tile.TileHeight);
            var matchingCandidates = candidates
                .Where(item => item.Tile.TileWidth == selectedCellSize.Width
                    && item.Tile.TileHeight == selectedCellSize.Height)
                .ToArray();
            if (matchingCandidates.Length != candidates.Length)
            {
                sheetsWithMultipleSizes++;
            }

            var sourceTiles = matchingCandidates
                .GroupBy(item => item.TileId)
                .Select(group => group.MaxBy(item => item.Order))
                .OrderBy(item => item.Order)
                .ToArray();
            if (sourceTiles.Length == 0)
            {
                continue;
            }

            var maxTileId = sourceTiles.Max(item => item.TileId);
            var rows = (maxTileId / TilesPerRow) + 1;
            var output = new PngImage(
                TilesPerRow * selectedCellSize.Width,
                rows * selectedCellSize.Height);
            output.Clear(Rgba.Transparent);

            foreach (var item in sourceTiles)
            {
                var singleTilePath = Path.Combine(
                    singleRoot,
                    group.PackDirectoryName,
                    SanitizeFileName(group.SheetName),
                    $"{SanitizeFileName(item.Tile.TileName)}.png");
                if (!File.Exists(singleTilePath))
                {
                    continue;
                }

                PngImage singleTile;
                try
                {
                    singleTile = PngImage.Load(singleTilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: failed to load tile {singleTilePath}: {ex.Message}");
                    continue;
                }

                if (singleTile.Width != selectedCellSize.Width
                    || singleTile.Height != selectedCellSize.Height)
                {
                    Console.WriteLine(
                        $"Warning: skipped {item.Tile.TileName}: expected {selectedCellSize.Width}x{selectedCellSize.Height}, " +
                        $"found {singleTile.Width}x{singleTile.Height}");
                    continue;
                }

                var left = (item.TileId % TilesPerRow) * selectedCellSize.Width;
                var top = (item.TileId / TilesPerRow) * selectedCellSize.Height;
                output.DrawImage(singleTile, left, top);
            }

            var packTilesDirectory = Path.Combine(tilesRoot, group.PackDirectoryName);
            Directory.CreateDirectory(packTilesDirectory);
            output.Save(Path.Combine(
                packTilesDirectory,
                $"{SanitizeFileName(group.SheetName)}.png"));
            written++;
        }

        if (sheetsWithMultipleSizes > 0)
        {
            Console.WriteLine(
                $"Tile packs: selected one consistent tile size for {sheetsWithMultipleSizes} pack tileset(s) containing multiple resolutions.");
        }

        return written;
    }

    private static bool TryParseTileName(string tileName, out string sheetName, out int tileId)
    {
        sheetName = string.Empty;
        tileId = 0;
        var index = tileName.LastIndexOf('_');
        if (index <= 0 || index >= tileName.Length - 1)
        {
            return false;
        }

        if (!int.TryParse(tileName[(index + 1)..], out tileId))
        {
            return false;
        }

        sheetName = tileName[..index];
        return true;
    }

    private static string ReadPackString(BinaryReader reader)
    {
        return ReadPackString(reader, reader.ReadInt32());
    }

    private static string ReadPackString(BinaryReader reader, int length)
    {
        return new string(reader.ReadChars(length));
    }

    private static PackImageKind PeekImageKind(BinaryReader reader)
    {
        if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
        {
            return PackImageKind.Unknown;
        }

        var start = reader.BaseStream.Position;
        var bytes = reader.ReadBytes(4);
        reader.BaseStream.Position = start;

        if (bytes is [0x89, 0x50, 0x4e, 0x47])
        {
            return PackImageKind.Png;
        }

        return bytes is [0x44, 0x44, 0x53, 0x20] ? PackImageKind.Dds : PackImageKind.Unknown;
    }

    private static byte[] ReadPngBytes(BinaryReader reader)
    {
        var start = reader.BaseStream.Position;
        reader.BaseStream.Seek(8, SeekOrigin.Current);
        while (reader.BaseStream.Position + 12 <= reader.BaseStream.Length)
        {
            var length = ReadBigEndianInt(reader);
            var type = Encoding.ASCII.GetString(reader.ReadBytes(4));
            reader.BaseStream.Seek(length + 4L, SeekOrigin.Current);
            if (type == "IEND")
            {
                break;
            }
        }

        var end = reader.BaseStream.Position;
        reader.BaseStream.Position = start;
        return reader.ReadBytes(checked((int)(end - start)));
    }

    private static byte[] ReadDdsBytes(BinaryReader reader)
    {
        var start = reader.BaseStream.Position;
        SkipUntilSentinel(reader);
        var end = reader.BaseStream.Position;
        reader.BaseStream.Position = start;
        return reader.ReadBytes(checked((int)(end - start)));
    }

    private static void SkipUntilSentinel(BinaryReader reader)
    {
        while (reader.BaseStream.Position + 4 <= reader.BaseStream.Length)
        {
            var value = reader.ReadUInt32();
            if (value is 0xDEADBEEF or 0xAE444E45)
            {
                break;
            }
        }
    }

    private static int ReadBigEndianInt(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (bytes.Length != 4)
        {
            throw new EndOfStreamException();
        }

        return (bytes[0] << 24)
            | (bytes[1] << 16)
            | (bytes[2] << 8)
            | bytes[3];
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return value;
    }

    private static string ReserveUniqueDirectoryName(string value, ISet<string> usedNames)
    {
        var sanitized = SanitizeFileName(value);
        if (usedNames.Add(sanitized))
        {
            return sanitized;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{sanitized}_{suffix}";
            if (usedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private enum PackImageKind
    {
        Unknown,
        Png,
        Dds
    }

    private readonly record struct SheetCellSize(int Width, int Height);

    private readonly record struct ParsedPackedTile(
        PackedTile Tile,
        string SheetName,
        int TileId,
        int Order);

    private sealed class PackSheetKeyComparer : IEqualityComparer<(string PackDirectoryName, string SheetName)>
    {
        public static readonly PackSheetKeyComparer Instance = new();

        public bool Equals(
            (string PackDirectoryName, string SheetName) x,
            (string PackDirectoryName, string SheetName) y)
        {
            return string.Equals(x.PackDirectoryName, y.PackDirectoryName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.SheetName, y.SheetName, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string PackDirectoryName, string SheetName) value)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.PackDirectoryName),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.SheetName));
        }
    }

    private readonly record struct PackedTile(
        string TileName,
        string AtlasName,
        int X,
        int Y,
        int Width,
        int Height,
        int OffsetX,
        int OffsetY,
        int TileWidth,
        int TileHeight,
        string RawPackDirectoryName,
        string PackDirectoryName);
}

internal readonly record struct TilesPackExtractionResult(
    int RawImageCount,
    int IndividualTileCount,
    int TileSheetCount)
{
    public static readonly TilesPackExtractionResult Empty = new(0, 0, 0);
    public int TotalImageCount => RawImageCount + IndividualTileCount + TileSheetCount;
}
