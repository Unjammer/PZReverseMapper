using System.Diagnostics;

namespace PZ_Mapper_Converter;

internal sealed class MapConverter
{
    private readonly ConverterOptions _options;
    private readonly IProgress<ConversionProgress>? _progress;
    private readonly Dictionary<CellCoord, TargetCell> _targetCells = new();

    public MapConverter(ConverterOptions options, IProgress<ConversionProgress>? progress = null)
    {
        _options = options;
        _progress = progress;
    }

    public ConversionResult Run()
    {
        var stopwatch = Stopwatch.StartNew();
        if (_options.TilesOnly)
        {
            return RunTilesOnly(stopwatch);
        }

        if (!Directory.Exists(_options.InputDirectory))
        {
            throw new DirectoryNotFoundException($"Input folder not found: {_options.InputDirectory}");
        }

        ValidateOutputSelection();
        Report("Prepare", "Preparing output folder");
        PrepareOutput();

        Report("Headers", "Reading lotheaders");
        var headers = LotHeaderReader.ReadAll(_options.InputDirectory);
        if (_options.IncludeCells is not null)
        {
            headers = headers
                .Where(pair => _options.IncludeCells.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        if (headers.Count == 0)
        {
            throw new InvalidDataException($"No matching .lotheader files found in {_options.InputDirectory}");
        }

        ValidateSourceCellSize(headers.Values);
        Report("Headers", $"Loaded {headers.Count} source cell(s)");

        var tileSets = new TileSetCatalog();
        Report("Tiles", "Loading tile definitions");
        tileSets.LoadTilesPath(_options.TilesPath);
        if (!string.IsNullOrWhiteSpace(_options.ModTilesPath))
        {
            Report("Tiles", "Loading modded tile definitions");
            tileSets.LoadTilesPath(_options.ModTilesPath, overrideExisting: true);
        }

        Report("Tile images", _options.ExportTilePacks ? "Extracting tilespack images" : "Skipping tilespack extraction");
        var tileImageCount = _options.ExportTilePacks
            ? TilesPackExtractor.Extract(_options.EnumerateTilesAssetPaths(), _options.OutputDirectory).TotalImageCount
            : 0;

        Report("Grid", "Building target cell grid");
        CreateTargetCellGrid(headers.Values);
        ReadLotPacks(headers.Values, tileSets);
        Report("Rooms", "Reprojecting RoomDefs");
        ReprojectRooms(headers.Values);
        Report("Objects", _options.ExportObjects && !_options.TbxOnly ? "Reading objects.lua" : "Skipping objects.lua");
        var objects = _options.ExportObjects && !_options.TbxOnly ? ReadObjects() : Array.Empty<MapObject>();
        var buildingTbxWriter = new BuildingTbxWriter(_options.OutputDirectory, _options.TargetCellSize, _targetCells);
        Report("Building TBX", _options.ExportBuildingTbx ? "Writing building TBX pack" : "Skipping building TBX pack");
        var buildingTbxCount = _options.ExportBuildingTbx ? buildingTbxWriter.Write(headers.Values) : 0;
        Report("Tiles", "Finalizing tilesets");
        tileSets.Build();
        Report("Images", _options.ExportImages && !_options.TbxOnly ? "Writing preview images" : "Skipping preview images");
        var imageCount = 0;
        if (_options.ExportImages && !_options.TbxOnly)
        {
            imageCount += new MapImageWriter(_options.OutputDirectory, _options.TargetCellSize).Write(_targetCells.Values.ToArray(), headers.Values.ToArray());
            if (_options.IncludeCells is null)
            {
                Report("Images", "Checking B42 biomemap tiles");
                imageCount += MapImageWriter.WriteBiomeMapIfPresent(_options.InputDirectory, _options.OutputDirectory);
            }
            else
            {
                Report("Images", "Skipping full biomemap for filtered source-cell export");
            }
        }

        var tmxWriter = new TmxWriter(
            _options.OutputDirectory,
            _options.TargetCellSize,
            tileSets,
            _options.ExportRoomTbx,
            ShouldWriteTmxLevelAttributes(headers.Values));
        var targetCells = _targetCells.Values.OrderBy(c => c.Coord.X).ThenBy(c => c.Coord.Y).ToArray();
        for (var i = 0; i < targetCells.Length; i++)
        {
            var cell = targetCells[i];
            if (_options.TbxOnly)
            {
                Report("RoomDef TBX", $"Writing RoomDef TBX {cell.Coord}", i + 1, targetCells.Length);
                tmxWriter.WriteRoomTbxOnly(cell);
            }
            else
            {
                Report("TMX", $"Writing TMX {cell.Coord}", i + 1, targetCells.Length);
                tmxWriter.Write(cell);
            }
        }

        var pzwFile = Path.Combine(_options.OutputDirectory, $"{_options.ProjectName}.pzw");
        if (_options.TbxOnly)
        {
            Report("PZW", "Skipping WorldEd project");
            pzwFile = string.Empty;
        }
        else
        {
            Report("PZW", "Writing WorldEd project");
            PzwWriter.Write(pzwFile, _options.ProjectName, _targetCells.Keys, objects, _options.TargetCellSize);
        }

        stopwatch.Stop();
        Report("Done", $"Export finished in {FormatElapsed(stopwatch.Elapsed)}");

        return new ConversionResult
        {
            SourceCellCount = headers.Count,
            TargetCellCount = _targetCells.Count,
            ObjectCount = objects.Count,
            ImageCount = imageCount,
            TileImageCount = tileImageCount,
            TbxCount = tmxWriter.TbxCount,
            BuildingTbxCount = buildingTbxCount,
            TileSetCount = tileSets.TileSets.Count,
            Elapsed = stopwatch.Elapsed,
            OutputDirectory = _options.OutputDirectory,
            ProjectFile = pzwFile
        };
    }

    private ConversionResult RunTilesOnly(Stopwatch stopwatch)
    {
        ValidateOutputSelection();
        Report("Prepare", "Preparing tiles-only output folder");
        PrepareOutput();
        Report("Tile images", "Extracting atlases, individual tiles and tilesets");
        var extraction = TilesPackExtractor.Extract(
            _options.EnumerateTilesAssetPaths(),
            _options.OutputDirectory);

        stopwatch.Stop();
        Report("Done", $"Tiles-only export finished in {FormatElapsed(stopwatch.Elapsed)}");
        return new ConversionResult
        {
            SourceCellCount = 0,
            TargetCellCount = 0,
            ObjectCount = 0,
            ImageCount = 0,
            TileImageCount = extraction.TotalImageCount,
            TbxCount = 0,
            BuildingTbxCount = 0,
            TileSetCount = extraction.TileSheetCount,
            Elapsed = stopwatch.Elapsed,
            OutputDirectory = _options.OutputDirectory,
            ProjectFile = string.Empty
        };
    }

    private void ValidateOutputSelection()
    {
        if (!string.IsNullOrWhiteSpace(_options.InputDirectory)
            && string.Equals(TrimPath(_options.InputDirectory), TrimPath(_options.OutputDirectory), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Input and output folders must be different.");
        }

        if (_options.TilesOnly)
        {
            if (!_options.ExportTilePacks)
            {
                throw new InvalidOperationException("Tiles-only mode requires tilespack extraction.");
            }

            if (!_options.EnumerateTilesAssetPaths().Any())
            {
                throw new InvalidOperationException("Tiles-only mode requires a tiles or mod-assets path.");
            }
        }

        if (_options.TbxOnly && !_options.ExportRoomTbx && !_options.ExportBuildingTbx)
        {
            throw new InvalidOperationException("TBX only requires RoomDef TBX and/or Building TBX to be enabled.");
        }
    }

    private void Report(string stage, string message, int? completed = null, int? total = null)
    {
        _progress?.Report(new ConversionProgress
        {
            Stage = stage,
            Message = message,
            Completed = completed,
            Total = total
        });
    }

    internal static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalHours >= 1
            ? elapsed.ToString(@"h\:mm\:ss")
            : elapsed.ToString(@"m\:ss");
    }

    private bool ShouldWriteTmxLevelAttributes(IEnumerable<LotHeaderData> headers)
    {
        return _options.ExpectedSourceCellSize == 256
            || headers.Any(header => header.CellDim == 256 || header.MinLevel < 0);
    }

    private void ValidateSourceCellSize(IEnumerable<LotHeaderData> headers)
    {
        if (_options.ExpectedSourceCellSize is null)
        {
            return;
        }

        var unexpected = headers
            .Where(header => header.CellDim != _options.ExpectedSourceCellSize.Value)
            .Select(header => $"{header.CellX}_{header.CellY}={header.CellDim}")
            .ToArray();

        if (unexpected.Length == 0)
        {
            return;
        }

        throw new InvalidDataException(
            $"Source profile expected {_options.ExpectedSourceCellSize.Value}x{_options.ExpectedSourceCellSize.Value} cells, " +
            $"but these headers differ: {string.Join(", ", unexpected.Take(12))}");
    }

    private void PrepareOutput()
    {
        if (_options.CleanOutput && Directory.Exists(_options.OutputDirectory))
        {
            OutputCleaner.EnsureSafeCleanTarget(_options.OutputDirectory);
            if (OutputCleaner.HasContent(_options.OutputDirectory) && !_options.CleanOutputConfirmed)
            {
                throw new InvalidOperationException(
                    "Clean output requires explicit confirmation before moving existing output files to the Recycle Bin.");
            }

            Report("Prepare", "Moving existing output contents to Recycle Bin");
            var movedCount = OutputCleaner.CleanToRecycleBin(_options.OutputDirectory);
            Report("Prepare", movedCount == 0
                ? "Output folder was already empty"
                : $"Moved {movedCount} existing output item(s) to Recycle Bin");
        }

        Directory.CreateDirectory(_options.OutputDirectory);
        if (!_options.TbxOnly && !_options.TilesOnly)
        {
            Directory.CreateDirectory(Path.Combine(_options.OutputDirectory, "tmx"));
        }
    }

    private void CreateTargetCellGrid(IEnumerable<LotHeaderData> headers)
    {
        var list = headers.ToArray();
        var minWorldX = list.Min(h => h.MinSquareX);
        var minWorldY = list.Min(h => h.MinSquareY);
        var maxWorldX = list.Max(h => h.MaxSquareX);
        var maxWorldY = list.Max(h => h.MaxSquareY);

        var minTargetX = BinaryHelpers.FloorDiv(minWorldX, _options.TargetCellSize);
        var minTargetY = BinaryHelpers.FloorDiv(minWorldY, _options.TargetCellSize);
        var maxTargetX = BinaryHelpers.FloorDiv(maxWorldX, _options.TargetCellSize);
        var maxTargetY = BinaryHelpers.FloorDiv(maxWorldY, _options.TargetCellSize);

        for (var x = minTargetX; x <= maxTargetX; x++)
        {
            for (var y = minTargetY; y <= maxTargetY; y++)
            {
                GetTargetCell(new CellCoord(x, y));
            }
        }
    }

    private void ReadLotPacks(IEnumerable<LotHeaderData> headers, TileSetCatalog tileSets)
    {
        var orderedHeaders = headers.OrderBy(h => h.CellX).ThenBy(h => h.CellY).ToArray();
        for (var i = 0; i < orderedHeaders.Length; i++)
        {
            var header = orderedHeaders[i];
            Report("Lotpacks", $"Reading world_{header.CellX}_{header.CellY}.lotpack", i + 1, orderedHeaders.Length);
            var reader = new LotPackReader(header, _options.InputDirectory);
            if (!reader.Exists)
            {
                Console.WriteLine($"Warning: missing world_{header.CellX}_{header.CellY}.lotpack");
                continue;
            }

            reader.Read((localX, localY, floor, layer, tileName) =>
            {
                var worldX = header.MinSquareX + localX;
                var worldY = header.MinSquareY + localY;
                var targetX = BinaryHelpers.FloorDiv(worldX, _options.TargetCellSize);
                var targetY = BinaryHelpers.FloorDiv(worldY, _options.TargetCellSize);
                var targetLocalX = BinaryHelpers.PositiveMod(worldX, _options.TargetCellSize);
                var targetLocalY = BinaryHelpers.PositiveMod(worldY, _options.TargetCellSize);

                tileSets.Observe(tileName);
                GetTargetCell(new CellCoord(targetX, targetY))
                    .AddTile(targetLocalX, targetLocalY, floor, layer, tileName, _options.TargetCellSize);
            });
        }
    }

    private void ReprojectRooms(IEnumerable<LotHeaderData> headers)
    {
        foreach (var room in headers.SelectMany(h => h.Rooms))
        {
            if (room.Width <= 0 || room.Height <= 0)
            {
                continue;
            }

            var minCellX = BinaryHelpers.FloorDiv(room.X, _options.TargetCellSize);
            var minCellY = BinaryHelpers.FloorDiv(room.Y, _options.TargetCellSize);
            var maxCellX = BinaryHelpers.FloorDiv(room.Right - 1, _options.TargetCellSize);
            var maxCellY = BinaryHelpers.FloorDiv(room.Bottom - 1, _options.TargetCellSize);

            for (var cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                for (var cellY = minCellY; cellY <= maxCellY; cellY++)
                {
                    var cellMinX = cellX * _options.TargetCellSize;
                    var cellMinY = cellY * _options.TargetCellSize;
                    var clipLeft = Math.Max(room.X, cellMinX);
                    var clipTop = Math.Max(room.Y, cellMinY);
                    var clipRight = Math.Min(room.Right, cellMinX + _options.TargetCellSize);
                    var clipBottom = Math.Min(room.Bottom, cellMinY + _options.TargetCellSize);

                    if (clipRight <= clipLeft || clipBottom <= clipTop)
                    {
                        continue;
                    }

                    GetTargetCell(new CellCoord(cellX, cellY)).Rooms.Add(new RoomRect
                    {
                        SourceRoomId = room.SourceRoomId,
                        Name = room.Name,
                        Floor = room.Floor,
                        X = clipLeft - cellMinX,
                        Y = clipTop - cellMinY,
                        Width = clipRight - clipLeft,
                        Height = clipBottom - clipTop
                    });
                }
            }
        }
    }

    private IReadOnlyList<MapObject> ReadObjects()
    {
        var objectsFile = Path.Combine(_options.InputDirectory, "objects.lua");
        if (!File.Exists(objectsFile))
        {
            return Array.Empty<MapObject>();
        }

        var outputCells = _targetCells.Keys.ToHashSet();
        return ObjectsLuaReader.Read(objectsFile)
            .Where(obj => obj.CanWrite)
            .Where(obj => outputCells.Contains(obj.GetCell(_options.TargetCellSize)))
            .ToArray();
    }

    private TargetCell GetTargetCell(CellCoord coord)
    {
        if (_targetCells.TryGetValue(coord, out var cell))
        {
            return cell;
        }

        cell = new TargetCell(coord);
        _targetCells.Add(coord, cell);
        return cell;
    }

    private static string TrimPath(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
