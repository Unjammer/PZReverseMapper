using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace PZ_Mapper_Converter;

internal sealed class MapImageWriter
{
    private readonly string _outputDirectory;
    private readonly int _cellSize;

    public MapImageWriter(string outputDirectory, int cellSize)
    {
        _outputDirectory = outputDirectory;
        _cellSize = cellSize;
    }

    public int Write(IReadOnlyCollection<TargetCell> targetCells, IReadOnlyCollection<LotHeaderData> headers)
    {
        if (targetCells.Count == 0)
        {
            return 0;
        }

        var imageDirectory = Path.Combine(_outputDirectory, "maps_img");
        Directory.CreateDirectory(imageDirectory);

        var orderedCells = targetCells.OrderBy(cell => cell.Coord.X).ThenBy(cell => cell.Coord.Y).ToArray();
        var written = 0;

        foreach (var cell in orderedCells)
        {
            var baseImage = RenderCell(cell, MapImageKind.Base);
            var fullImage = RenderCell(cell, MapImageKind.Full);
            var vegImage = RenderCell(cell, MapImageKind.Vegetation);

            baseImage.Save(Path.Combine(imageDirectory, $"{cell.Coord.X}_{cell.Coord.Y}_base.png"));
            fullImage.Save(Path.Combine(imageDirectory, $"{cell.Coord.X}_{cell.Coord.Y}_full.png"));
            vegImage.Save(Path.Combine(imageDirectory, $"{cell.Coord.X}_{cell.Coord.Y}_veg.png"));
            written += 3;
        }

        WriteMerged(orderedCells, MapImageKind.Base, "Map.png");
        WriteMerged(orderedCells, MapImageKind.Vegetation, "Map_veg.png");
        WriteMerged(orderedCells, MapImageKind.Full, "world.png");
        written += 3;

        if (WriteZombieMap(headers))
        {
            written++;
        }

        return written;
    }

    public static int WriteBiomeMapIfPresent(string inputDirectory, string outputDirectory)
    {
        var mapsDirectory = Path.Combine(inputDirectory, "maps");
        if (!Directory.Exists(mapsDirectory))
        {
            return 0;
        }

        var namePattern = new Regex(@"^biomemap_(-?\d+)_(-?\d+)\.png$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var tiles = Directory.EnumerateFiles(mapsDirectory, "biomemap_*_*.png", SearchOption.TopDirectoryOnly)
            .Select(file => new { File = file, Match = namePattern.Match(Path.GetFileName(file)) })
            .Where(item => item.Match.Success)
            .Select(item => new BiomeTile(
                int.Parse(item.Match.Groups[1].Value),
                int.Parse(item.Match.Groups[2].Value),
                item.File))
            .OrderBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .ToArray();

        if (tiles.Length == 0)
        {
            return 0;
        }

        var first = PngImage.Load(tiles[0].File);
        var tileWidth = first.Width;
        var tileHeight = first.Height;
        if (tileWidth <= 0 || tileHeight <= 0)
        {
            return 0;
        }

        var byCoord = tiles.ToDictionary(tile => (tile.X, tile.Y), tile => tile.File);
        var minX = tiles.Min(tile => tile.X);
        var minY = tiles.Min(tile => tile.Y);
        var maxX = tiles.Max(tile => tile.X);
        var maxY = tiles.Max(tile => tile.Y);
        var outputWidth = (maxX - minX + 1) * tileWidth;
        var outputHeight = (maxY - minY + 1) * tileHeight;
        var cachedTileY = int.MinValue;
        PngImage?[] cachedRow = Array.Empty<PngImage?>();

        PngImage.SaveRows(Path.Combine(outputDirectory, "biomemap.png"), outputWidth, outputHeight, (rowIndex, row) =>
        {
            FillOpaqueBlack(row);

            var gridY = minY + rowIndex / tileHeight;
            var localY = rowIndex % tileHeight;
            if (gridY != cachedTileY)
            {
                cachedRow = LoadBiomeRow(byCoord, minX, maxX, gridY);
                cachedTileY = gridY;
            }

            for (var x = 0; x < cachedRow.Length; x++)
            {
                var tile = cachedRow[x];
                if (tile is null || localY >= tile.Height)
                {
                    continue;
                }

                tile.CopyRowTo(localY, row.AsSpan(x * tileWidth * 4));
            }
        });

        return 1;
    }

    private static PngImage?[] LoadBiomeRow(IReadOnlyDictionary<(int X, int Y), string> byCoord, int minX, int maxX, int y)
    {
        var row = new PngImage?[maxX - minX + 1];
        for (var x = minX; x <= maxX; x++)
        {
            if (byCoord.TryGetValue((x, y), out var file))
            {
                row[x - minX] = PngImage.Load(file);
            }
        }

        return row;
    }

    private static void FillOpaqueBlack(Span<byte> row)
    {
        row.Clear();
        for (var i = 3; i < row.Length; i += 4)
        {
            row[i] = 255;
        }
    }

    private readonly record struct BiomeTile(int X, int Y, string File);

    private PngImage RenderCell(TargetCell cell, MapImageKind kind)
    {
        var image = new PngImage(_cellSize, _cellSize);
        image.Clear(kind == MapImageKind.Vegetation ? Rgba.Black : new Rgba(145, 135, 60));

        foreach (var (key, tiles) in cell.TilesByLayer.OrderBy(pair => pair.Key.Floor).ThenBy(pair => pair.Key.Layer))
        {
            foreach (var (index, tileName) in tiles)
            {
                var x = index % _cellSize;
                var y = index / _cellSize;
                if (x < 0 || y < 0 || x >= _cellSize || y >= _cellSize)
                {
                    continue;
                }

                switch (kind)
                {
                    case MapImageKind.Base:
                        if (key.Layer <= 1)
                        {
                            image.SetPixel(x, y, GetBaseColor(tileName));
                        }
                        break;
                    case MapImageKind.Full:
                        DrawTile(image, x, y, GetFullColor(tileName), IsLargeVegetation(tileName));
                        break;
                    case MapImageKind.Vegetation:
                        image.SetPixel(x, y, GetVegetationColor(tileName));
                        break;
                }
            }
        }

        return image;
    }

    private void DrawRooms(PngImage image, TargetCell cell)
    {
        foreach (var room in cell.Rooms.Where(room => room.Floor == 0))
        {
            var x = Math.Clamp(room.X, 0, _cellSize - 1);
            var y = Math.Clamp(room.Y, 0, _cellSize - 1);
            var width = Math.Min(Math.Clamp(room.Width, 1, _cellSize), _cellSize - x);
            var height = Math.Min(Math.Clamp(room.Height, 1, _cellSize), _cellSize - y);
            if (width <= 0 || height <= 0)
            {
                continue;
            }

            image.FillRect(x, y, width, height, GetRoomColor(room.Name));
            image.DrawRect(x, y, width, height, new Rgba(0, 0, 0, 100));
        }
    }

    private void WriteMerged(IReadOnlyCollection<TargetCell> cells, MapImageKind kind, string fileName)
    {
        var minX = cells.Min(cell => cell.Coord.X);
        var minY = cells.Min(cell => cell.Coord.Y);
        var maxX = cells.Max(cell => cell.Coord.X);
        var maxY = cells.Max(cell => cell.Coord.Y);
        var width = (maxX - minX + 1) * _cellSize;
        var height = (maxY - minY + 1) * _cellSize;
        var merged = new PngImage(width, height);
        merged.Clear(Rgba.Transparent);

        foreach (var cell in cells)
        {
            var image = RenderCell(cell, kind);
            merged.DrawImage(image, (cell.Coord.X - minX) * _cellSize, (cell.Coord.Y - minY) * _cellSize);
        }

        merged.Save(Path.Combine(_outputDirectory, fileName));
    }

    private bool WriteZombieMap(IReadOnlyCollection<LotHeaderData> headers)
    {
        var withDensity = headers.Where(header => header.ZombieDensity.Length > 0).ToArray();
        if (withDensity.Length == 0)
        {
            return false;
        }

        var minX = withDensity.Min(header => header.CellX);
        var minY = withDensity.Min(header => header.CellY);
        var maxX = withDensity.Max(header => header.CellX);
        var maxY = withDensity.Max(header => header.CellY);
        var maxChunks = withDensity.Max(header => header.ChunksPerCell);
        var image = new PngImage((maxX - minX + 1) * maxChunks, (maxY - minY + 1) * maxChunks);
        image.Clear(Rgba.Black);

        foreach (var header in withDensity)
        {
            for (var x = 0; x < header.ChunksPerCell; x++)
            {
                for (var y = 0; y < header.ChunksPerCell; y++)
                {
                    var index = x * header.ChunksPerCell + y;
                    if (index >= header.ZombieDensity.Length)
                    {
                        continue;
                    }

                    var value = header.ZombieDensity[index];
                    image.SetPixel((header.CellX - minX) * maxChunks + x, (header.CellY - minY) * maxChunks + y, new Rgba(value, value, value));
                }
            }
        }

        image.Save(Path.Combine(_outputDirectory, "Map_ZombieSpawnMap.png"));
        return true;
    }

    private void DrawTile(PngImage image, int x, int y, Rgba color, bool large)
    {
        if (!large)
        {
            image.SetPixel(x, y, color);
            return;
        }

        image.FillRect(Math.Max(0, x - 2), Math.Max(0, y - 2), Math.Min(5, _cellSize - Math.Max(0, x - 2)), Math.Min(5, _cellSize - Math.Max(0, y - 2)), color);
    }

    private static Rgba GetBaseColor(string tileName)
    {
        var color = GetTerrainColor(tileName);
        if (IsInteriorOrStructure(tileName))
        {
            color = new Rgba(100, 100, 100);
        }

        return ApplyCommonOverlays(tileName, color);
    }

    private static Rgba GetFullColor(string tileName)
    {
        var color = GetTerrainColor(tileName);
        if (tileName.Contains("roofs_", StringComparison.OrdinalIgnoreCase)
            || tileName.Contains("floors_interior_", StringComparison.OrdinalIgnoreCase))
        {
            color = Rgba.FromHex("#47484a");
        }
        else if (tileName.Contains("electricity_", StringComparison.OrdinalIgnoreCase)
                 || IsInteriorOrStructure(tileName))
        {
            color = Rgba.FromHex("#2c2d2e");
        }

        if (tileName.Contains("grassoverlay", StringComparison.OrdinalIgnoreCase))
        {
            color = Rgba.FromHex("#75752f");
        }

        return ApplyCommonOverlays(tileName, color);
    }

    private static Rgba GetTerrainColor(string tileName)
    {
        return tileName switch
        {
            "blends_natural_01_16" or "blends_natural_01_21" or "blends_natural_01_22" or "blends_natural_01_23" => new Rgba(90, 100, 35),
            "blends_natural_01_32" or "blends_natural_01_37" or "blends_natural_01_38" or "blends_natural_01_39" => new Rgba(117, 117, 47),
            "blends_natural_01_48" or "blends_natural_01_53" or "blends_natural_01_54" or "blends_natural_01_55" => new Rgba(145, 135, 60),
            "blends_natural_01_0" or "blends_natural_01_5" or "blends_natural_01_6" or "blends_natural_01_7"
                or "floors_exterior_natural_01_24" or "floors_exterior_natural_01_32" or "floors_exterior_natural_01_35" => new Rgba(210, 200, 160),
            "blends_street_01_48" or "blends_street_01_53" or "blends_street_01_54" or "blends_street_01_55" => new Rgba(165, 160, 140),
            "blends_street_01_80" or "blends_street_01_85" or "blends_street_01_86" or "blends_street_01_87"
                or "floors_exterior_street_01_0" or "floors_exterior_street_01_8" or "floors_exterior_street_01_14"
                or "floors_exterior_street_01_16" or "floors_exterior_street_01_17" => new Rgba(100, 100, 100),
            "blends_street_01_96" or "blends_street_01_101" or "blends_street_01_102" or "blends_street_01_103" => new Rgba(120, 120, 120),
            "floors_exterior_natural_01_12" or "floors_exterior_natural_01_13" => new Rgba(140, 70, 15),
            "blends_natural_01_64" or "blends_natural_01_69" or "blends_natural_01_70" or "blends_natural_01_71" => new Rgba(120, 70, 20),
            "blends_natural_01_80" or "blends_natural_01_85" or "blends_natural_01_86" or "blends_natural_01_87" => new Rgba(80, 55, 20),
            "blends_street_01_0" => new Rgba(110, 100, 100),
            "blends_street_01_16" or "blends_street_01_21" => new Rgba(130, 120, 120),
            "blends_natural_02_0" or "blends_natural_02_5" or "blends_natural_02_6" or "blends_natural_02_7" => new Rgba(0, 138, 255),
            "carpentry_02_12" or "carpentry_02_13" or "carpentry_02_14" or "carpentry_02_15" or "carpentry_02_51" or "carpentry_02_56" => new Rgba(152, 126, 102),
            _ => new Rgba(145, 135, 60)
        };
    }

    private static Rgba ApplyCommonOverlays(string tileName, Rgba color)
    {
        if (tileName.Contains("trees_", StringComparison.OrdinalIgnoreCase)
            || tileName.Contains("jumbo", StringComparison.OrdinalIgnoreCase))
        {
            return Rgba.FromHex("#263516");
        }

        if (tileName.Contains("vegetation_farm", StringComparison.OrdinalIgnoreCase))
        {
            return Rgba.FromHex("#daa520");
        }

        if (tileName.Contains("_railroad", StringComparison.OrdinalIgnoreCase))
        {
            return Rgba.FromHex("#493a2b");
        }

        if (tileName.Contains("fencing", StringComparison.OrdinalIgnoreCase))
        {
            return Rgba.FromHex("#525a53");
        }

        if (tileName.Contains("traffic", StringComparison.OrdinalIgnoreCase))
        {
            return Rgba.FromHex("#cccccc");
        }

        if (tileName.Contains("street_decoration_", StringComparison.OrdinalIgnoreCase)
            || tileName.Contains("street_curbs_", StringComparison.OrdinalIgnoreCase))
        {
            return Rgba.FromHex("#3b3b3b");
        }

        if (tileName.Contains("floors_exterior_tilesandstone_01_", StringComparison.OrdinalIgnoreCase))
        {
            return new Rgba(210, 200, 160);
        }

        if (tileName.Contains("floors_exterior_natural_01_0", StringComparison.OrdinalIgnoreCase))
        {
            return Rgba.FromHex("#75752f");
        }

        return color;
    }

    private static Rgba GetVegetationColor(string tileName)
    {
        return tileName switch
        {
            "jumbo_tree_01_0" => Rgba.Red,
            "blends_natural_02_0" or "blends_natural_02_5" or "blends_natural_02_6" or "blends_natural_02_7" => Rgba.Black,
            "vegetation_trees_01_8" or "vegetation_trees_01_9" or "vegetation_trees_01_10" or "vegetation_trees_01_11" => Rgba.Red,
            _ when IsDarkGrassOverlay(tileName) => new Rgba(127, 0, 0),
            _ when IsMediumGrassOverlay(tileName) => new Rgba(64, 0, 0),
            _ when IsShortGrassOverlay(tileName) => new Rgba(0, 255, 0),
            _ when tileName.StartsWith("vegetation_foliage_01_", StringComparison.OrdinalIgnoreCase) => Rgba.Magenta,
            _ => Rgba.Black
        };
    }

    private static bool IsDarkGrassOverlay(string tileName)
    {
        return TryGetSuffix(tileName, "blends_grassoverlays_01_", out var id) && id is >= 0 and <= 21;
    }

    private static bool IsMediumGrassOverlay(string tileName)
    {
        return TryGetSuffix(tileName, "blends_grassoverlays_01_", out var id) && id is >= 24 and <= 45;
    }

    private static bool IsShortGrassOverlay(string tileName)
    {
        return (TryGetSuffix(tileName, "blends_grassoverlays_01_", out var id) && id is >= 48 and <= 69)
            || (TryGetSuffix(tileName, "vegetation_groundcover_01_", out var groundId) && groundId is >= 18 and <= 23);
    }

    private static bool TryGetSuffix(string tileName, string prefix, out int id)
    {
        id = 0;
        return tileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(tileName[prefix.Length..], out id);
    }

    private static bool IsInteriorOrStructure(string tileName)
    {
        return tileName.Contains("wall", StringComparison.OrdinalIgnoreCase)
            || tileName.Contains("fixture", StringComparison.OrdinalIgnoreCase)
            || tileName.Contains("appliance", StringComparison.OrdinalIgnoreCase)
            || tileName.Contains("furniture", StringComparison.OrdinalIgnoreCase)
            || tileName.Contains("location", StringComparison.OrdinalIgnoreCase)
            || tileName.Contains("lighting_indoor", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLargeVegetation(string tileName)
    {
        return tileName.Contains("jumbo", StringComparison.OrdinalIgnoreCase)
            || tileName.Contains("_tree", StringComparison.OrdinalIgnoreCase);
    }

    private static Rgba GetRoomColor(string roomName)
    {
        if (ContainsAny(roomName, "restaurant", "kitchen", "spiffo", "bakery", "diner", "cafe", "sushi", "butcher", "taco", "pizza", "burger", "mexican", "icecream", "donut", "bowling", "gym", "fitness", "italian", "western"))
        {
            return Rgba.FromHex("#e7d646");
        }

        if (ContainsAny(roomName, "store", "storage", "gigamart", "grocer", "fossoil", "library", "liquor", "changeroom"))
        {
            return Rgba.FromHex("#b2c45b");
        }

        if (ContainsAny(roomName, "medical", "clinic", "pharmacy", "optometrist", "laboratory", "hospital", "dentist"))
        {
            return Rgba.FromHex("#d77e92");
        }

        if (ContainsAny(roomName, "police", "security", "church", "army", "gunstore", "post", "theatre", "school", "bank"))
        {
            return Rgba.FromHex("#8976dd");
        }

        if (ContainsAny(roomName, "motel"))
        {
            return Rgba.FromHex("#7dc2d3");
        }

        if (ContainsAny(roomName, "shed", "garage", "mechanic", "foundry", "barn", "construction", "railroad"))
        {
            return Rgba.FromHex("#3d3b3b");
        }

        return Rgba.FromHex("#ca9d6f");
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private enum MapImageKind
    {
        Base,
        Full,
        Vegetation
    }

}

internal readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
    {
        public static readonly Rgba Transparent = new(0, 0, 0, 0);
        public static readonly Rgba Black = new(0, 0, 0);
        public static readonly Rgba Red = new(255, 0, 0);
        public static readonly Rgba Magenta = new(255, 0, 255);

        public static Rgba FromHex(string value)
        {
            var text = value.TrimStart('#');
            return new Rgba(
                Convert.ToByte(text[..2], 16),
                Convert.ToByte(text.Substring(2, 2), 16),
                Convert.ToByte(text.Substring(4, 2), 16));
        }
    }

internal sealed class PngImage
    {
        private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        private readonly byte[] _pixels;

        public PngImage(int width, int height)
        {
            Width = width;
            Height = height;
            _pixels = new byte[width * height * 4];
        }

        public int Width { get; }
        public int Height { get; }

        public static PngImage Load(string file)
        {
            using var stream = File.OpenRead(file);
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
            var signature = reader.ReadBytes(PngSignature.Length);
            if (!signature.SequenceEqual(PngSignature))
            {
                throw new InvalidDataException($"Not a PNG file: {file}");
            }

            var width = 0;
            var height = 0;
            var bitDepth = 0;
            var colorType = 0;
            byte[]? palette = null;
            byte[]? transparency = null;
            using var idat = new MemoryStream();

            while (stream.Position < stream.Length)
            {
                var length = ReadBigEndianInt(reader);
                var type = Encoding.ASCII.GetString(reader.ReadBytes(4));
                var data = reader.ReadBytes(length);
                reader.ReadBytes(4);

                switch (type)
                {
                    case "IHDR":
                        width = ReadBigEndianInt(data, 0);
                        height = ReadBigEndianInt(data, 4);
                        bitDepth = data[8];
                        colorType = data[9];
                        if (data[10] != 0 || data[11] != 0 || data[12] != 0)
                        {
                            throw new NotSupportedException($"Unsupported PNG compression/filter/interlace in {file}");
                        }
                        break;
                    case "PLTE":
                        palette = data;
                        break;
                    case "tRNS":
                        transparency = data;
                        break;
                    case "IDAT":
                        idat.Write(data);
                        break;
                    case "IEND":
                        return Decode(width, height, bitDepth, colorType, palette, transparency, idat.ToArray(), file);
                }
            }

            throw new InvalidDataException($"PNG is missing IEND: {file}");
        }

        public void Clear(Rgba color)
        {
            for (var i = 0; i < _pixels.Length; i += 4)
            {
                _pixels[i] = color.R;
                _pixels[i + 1] = color.G;
                _pixels[i + 2] = color.B;
                _pixels[i + 3] = color.A;
            }
        }

        public void SetPixel(int x, int y, Rgba color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return;
            }

            var offset = (x + y * Width) * 4;
            _pixels[offset] = color.R;
            _pixels[offset + 1] = color.G;
            _pixels[offset + 2] = color.B;
            _pixels[offset + 3] = color.A;
        }

        public void FillRect(int x, int y, int width, int height, Rgba color)
        {
            for (var yy = y; yy < y + height; yy++)
            {
                for (var xx = x; xx < x + width; xx++)
                {
                    SetPixel(xx, yy, color);
                }
            }
        }

        public void DrawRect(int x, int y, int width, int height, Rgba color)
        {
            for (var xx = x; xx < x + width; xx++)
            {
                SetPixel(xx, y, color);
                SetPixel(xx, y + height - 1, color);
            }

            for (var yy = y; yy < y + height; yy++)
            {
                SetPixel(x, yy, color);
                SetPixel(x + width - 1, yy, color);
            }
        }

        public void DrawImage(PngImage image, int left, int top)
        {
            for (var y = 0; y < image.Height; y++)
            {
                var targetY = top + y;
                if (targetY < 0 || targetY >= Height || left < 0 || left + image.Width > Width)
                {
                    for (var x = 0; x < image.Width; x++)
                    {
                        var source = (x + y * image.Width) * 4;
                        SetPixel(left + x, targetY, new Rgba(image._pixels[source], image._pixels[source + 1], image._pixels[source + 2], image._pixels[source + 3]));
                    }

                    continue;
                }

                Buffer.BlockCopy(image._pixels, y * image.Width * 4, _pixels, (left + targetY * Width) * 4, image.Width * 4);
            }
        }

        public void DrawImage(PngImage image, int sourceX, int sourceY, int width, int height, int left, int top)
        {
            for (var y = 0; y < height; y++)
            {
                var sy = sourceY + y;
                var dy = top + y;
                if (sy < 0 || sy >= image.Height || dy < 0 || dy >= Height)
                {
                    continue;
                }

                for (var x = 0; x < width; x++)
                {
                    var sx = sourceX + x;
                    var dx = left + x;
                    if (sx < 0 || sx >= image.Width || dx < 0 || dx >= Width)
                    {
                        continue;
                    }

                    var source = (sx + sy * image.Width) * 4;
                    var target = (dx + dy * Width) * 4;
                    _pixels[target] = image._pixels[source];
                    _pixels[target + 1] = image._pixels[source + 1];
                    _pixels[target + 2] = image._pixels[source + 2];
                    _pixels[target + 3] = image._pixels[source + 3];
                }
            }
        }

        public void CopyRowTo(int y, Span<byte> destination)
        {
            if (y < 0 || y >= Height)
            {
                return;
            }

            _pixels.AsSpan(y * Width * 4, Math.Min(destination.Length, Width * 4)).CopyTo(destination);
        }

        public void Save(string file)
        {
            SaveRows(file, Width, Height, (y, row) => _pixels.AsSpan(y * Width * 4, Width * 4).CopyTo(row));
        }

        public static void SaveRows(string file, int width, int height, Action<int, byte[]> writeRow)
        {
            using var stream = File.Create(file);
            stream.Write(PngSignature);
            WriteChunk(stream, "IHDR", BuildHeader(width, height));
            WriteRowsImageData(stream, width, height, writeRow);
            WriteChunk(stream, "IEND", Array.Empty<byte>());
        }

        private static byte[] BuildHeader(int width, int height)
        {
            using var stream = new MemoryStream();
            WriteBigEndian(stream, width);
            WriteBigEndian(stream, height);
            stream.WriteByte(8);
            stream.WriteByte(6);
            stream.WriteByte(0);
            stream.WriteByte(0);
            stream.WriteByte(0);
            return stream.ToArray();
        }

        private static void WriteRowsImageData(Stream stream, int width, int height, Action<int, byte[]> writeRow)
        {
            using var idat = new PngChunkStream(stream, "IDAT");
            var row = new byte[width * 4];
            using (var zlib = new ZLibStream(idat, CompressionLevel.Fastest, leaveOpen: true))
            {
                for (var y = 0; y < height; y++)
                {
                    row.AsSpan().Clear();
                    writeRow(y, row);
                    zlib.WriteByte(0);
                    zlib.Write(row);
                }
            }
        }

        private static PngImage Decode(int width, int height, int bitDepth, int colorType, byte[]? palette, byte[]? transparency, byte[] compressedData, string file)
        {
            if (width <= 0 || height <= 0)
            {
                throw new InvalidDataException($"Invalid PNG dimensions in {file}");
            }

            if (bitDepth != 8)
            {
                throw new NotSupportedException($"Only 8-bit PNG files are supported: {file}");
            }

            var bytesPerPixel = colorType switch
            {
                0 => 1,
                2 => 3,
                3 => 1,
                4 => 2,
                6 => 4,
                _ => throw new NotSupportedException($"Unsupported PNG color type {colorType} in {file}")
            };

            if (colorType == 3 && palette is null)
            {
                throw new InvalidDataException($"Indexed PNG is missing PLTE: {file}");
            }

            using var compressed = new MemoryStream(compressedData);
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
            using var rawStream = new MemoryStream();
            zlib.CopyTo(rawStream);
            var raw = rawStream.ToArray();
            var stride = width * bytesPerPixel;
            var expected = checked((stride + 1) * height);
            if (raw.Length < expected)
            {
                throw new InvalidDataException($"PNG data is shorter than expected: {file}");
            }

            var image = new PngImage(width, height);
            var previous = new byte[stride];
            var current = new byte[stride];
            var source = 0;

            for (var y = 0; y < height; y++)
            {
                var filter = raw[source++];
                Array.Copy(raw, source, current, 0, stride);
                source += stride;
                Unfilter(current, previous, bytesPerPixel, filter);
                WriteDecodedRow(image, y, current, colorType, palette, transparency);

                var swap = previous;
                previous = current;
                current = swap;
            }

            return image;
        }

        private static void Unfilter(byte[] current, byte[] previous, int bytesPerPixel, int filter)
        {
            for (var i = 0; i < current.Length; i++)
            {
                var left = i >= bytesPerPixel ? current[i - bytesPerPixel] : 0;
                var up = previous[i];
                var upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
                var predictor = filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException($"Unsupported PNG filter type {filter}")
                };

                current[i] = unchecked((byte)(current[i] + predictor));
            }
        }

        private static int Paeth(int left, int up, int upLeft)
        {
            var p = left + up - upLeft;
            var pa = Math.Abs(p - left);
            var pb = Math.Abs(p - up);
            var pc = Math.Abs(p - upLeft);
            if (pa <= pb && pa <= pc)
            {
                return left;
            }

            return pb <= pc ? up : upLeft;
        }

        private static void WriteDecodedRow(PngImage image, int y, byte[] row, int colorType, byte[]? palette, byte[]? transparency)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var dest = (x + y * image.Width) * 4;
                switch (colorType)
                {
                    case 0:
                    {
                        var gray = row[x];
                        image._pixels[dest] = gray;
                        image._pixels[dest + 1] = gray;
                        image._pixels[dest + 2] = gray;
                        image._pixels[dest + 3] = 255;
                        break;
                    }
                    case 2:
                    {
                        var source = x * 3;
                        image._pixels[dest] = row[source];
                        image._pixels[dest + 1] = row[source + 1];
                        image._pixels[dest + 2] = row[source + 2];
                        image._pixels[dest + 3] = 255;
                        break;
                    }
                    case 3:
                    {
                        var index = row[x];
                        var paletteOffset = index * 3;
                        if (palette is null || paletteOffset + 2 >= palette.Length)
                        {
                            image._pixels[dest + 3] = 255;
                            break;
                        }

                        image._pixels[dest] = palette[paletteOffset];
                        image._pixels[dest + 1] = palette[paletteOffset + 1];
                        image._pixels[dest + 2] = palette[paletteOffset + 2];
                        image._pixels[dest + 3] = transparency is not null && index < transparency.Length ? transparency[index] : (byte)255;
                        break;
                    }
                    case 4:
                    {
                        var source = x * 2;
                        var gray = row[source];
                        image._pixels[dest] = gray;
                        image._pixels[dest + 1] = gray;
                        image._pixels[dest + 2] = gray;
                        image._pixels[dest + 3] = row[source + 1];
                        break;
                    }
                    case 6:
                    {
                        var source = x * 4;
                        image._pixels[dest] = row[source];
                        image._pixels[dest + 1] = row[source + 1];
                        image._pixels[dest + 2] = row[source + 2];
                        image._pixels[dest + 3] = row[source + 3];
                        break;
                    }
                }
            }
        }

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            var typeBytes = Encoding.ASCII.GetBytes(type);
            WriteBigEndian(stream, data.Length);
            stream.Write(typeBytes);
            stream.Write(data);

            var crc = new Crc32();
            crc.Update(typeBytes);
            crc.Update(data);
            WriteBigEndian(stream, unchecked((int)crc.Value));
        }

        private static void WriteBigEndian(Stream stream, int value)
        {
            stream.WriteByte((byte)((value >> 24) & 0xff));
            stream.WriteByte((byte)((value >> 16) & 0xff));
            stream.WriteByte((byte)((value >> 8) & 0xff));
            stream.WriteByte((byte)(value & 0xff));
        }

        private static int ReadBigEndianInt(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(4);
            if (bytes.Length != 4)
            {
                throw new EndOfStreamException();
            }

            return ReadBigEndianInt(bytes, 0);
        }

        private static int ReadBigEndianInt(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24)
                | (bytes[offset + 1] << 16)
                | (bytes[offset + 2] << 8)
                | bytes[offset + 3];
        }

        private sealed class PngChunkStream : Stream
        {
            private const int BufferSize = 64 * 1024;
            private readonly Stream _output;
            private readonly string _chunkType;
            private readonly byte[] _buffer = new byte[BufferSize];
            private int _count;

            public PngChunkStream(Stream output, string chunkType)
            {
                _output = output;
                _chunkType = chunkType;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
                FlushBuffer();
            }

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count)
            {
                while (count > 0)
                {
                    var copy = Math.Min(count, _buffer.Length - _count);
                    Buffer.BlockCopy(buffer, offset, _buffer, _count, copy);
                    _count += copy;
                    offset += copy;
                    count -= copy;

                    if (_count == _buffer.Length)
                    {
                        FlushBuffer();
                    }
                }
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                while (!buffer.IsEmpty)
                {
                    var copy = Math.Min(buffer.Length, _buffer.Length - _count);
                    buffer[..copy].CopyTo(_buffer.AsSpan(_count));
                    _count += copy;
                    buffer = buffer[copy..];

                    if (_count == _buffer.Length)
                    {
                        FlushBuffer();
                    }
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    FlushBuffer();
                }

                base.Dispose(disposing);
            }

            private void FlushBuffer()
            {
                if (_count == 0)
                {
                    return;
                }

                WriteChunk(_output, _chunkType, _buffer.AsSpan(0, _count).ToArray());
                _count = 0;
            }
        }
    }

internal sealed class Crc32
    {
        private static readonly uint[] Table = BuildTable();
        private uint _value = 0xffffffff;

        public uint Value => _value ^ 0xffffffff;

        public void Update(byte[] bytes)
        {
            foreach (var b in bytes)
            {
                _value = Table[(_value ^ b) & 0xff] ^ (_value >> 8);
            }
        }

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                var c = i;
                for (var k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
                }

                table[i] = c;
            }

            return table;
        }
    }
