namespace PZ_Mapper_Converter;

internal sealed class ConverterOptions
{
    public required string InputDirectory { get; init; }
    public required string OutputDirectory { get; init; }
    public string ProjectName { get; init; } = "ConvertedMap";
    public string? TilesPath { get; init; }
    public string? ModTilesPath { get; init; }
    public int? ExpectedSourceCellSize { get; init; }
    public int TargetCellSize { get; init; } = 300;
    public bool CleanOutput { get; init; }
    public bool CleanOutputConfirmed { get; set; }
    public bool ExportImages { get; init; } = true;
    public bool ExportTilePacks { get; init; }
    public bool ExportObjects { get; init; } = true;
    public bool ExportRoomTbx { get; init; } = true;
    public bool ExportBuildingTbx { get; init; } = true;
    public bool TbxOnly { get; init; }
    public bool TilesOnly { get; init; }
    public IReadOnlySet<CellCoord>? IncludeCells { get; init; }

    public static ConverterOptions? Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return null;
        }

        string? input = null;
        string? output = null;
        string? name = null;
        string? tiles = null;
        string? modTiles = null;
        int? expectedSourceCellSize = null;
        int targetCellSize = 300;
        bool clean = false;
        bool exportImages = true;
        bool exportTilePacks = false;
        bool exportObjects = true;
        bool exportRoomTbx = true;
        bool exportBuildingTbx = true;
        bool tbxOnly = false;
        bool tilesOnly = false;
        HashSet<CellCoord>? includeCells = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string NextValue()
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {arg}");
                }
                return args[++i];
            }

            switch (arg.ToLowerInvariant())
            {
                case "--input":
                case "-i":
                    input = NextValue();
                    break;
                case "--output":
                case "-o":
                    output = NextValue();
                    break;
                case "--name":
                case "-n":
                    name = NextValue();
                    break;
                case "--tiles":
                case "-t":
                    tiles = NextValue();
                    break;
                case "--mod-tiles":
                case "--mod-media":
                    modTiles = NextValue();
                    break;
                case "--target-cell-size":
                    if (!int.TryParse(NextValue(), out targetCellSize) || targetCellSize <= 0)
                    {
                        throw new ArgumentException("--target-cell-size must be a positive integer");
                    }
                    break;
                case "--source-cell-size":
                    if (!int.TryParse(NextValue(), out var sourceCellSize) || sourceCellSize <= 0)
                    {
                        throw new ArgumentException("--source-cell-size must be a positive integer");
                    }
                    expectedSourceCellSize = sourceCellSize;
                    break;
                case "--cells":
                    includeCells = ParseCells(NextValue());
                    break;
                case "--clean":
                    clean = true;
                    break;
                case "--no-objects":
                    exportObjects = false;
                    break;
                case "--no-images":
                    exportImages = false;
                    break;
                case "--extract-tiles":
                    exportTilePacks = true;
                    break;
                case "--tiles-only":
                    tilesOnly = true;
                    exportTilePacks = true;
                    exportImages = false;
                    exportObjects = false;
                    exportRoomTbx = false;
                    exportBuildingTbx = false;
                    tbxOnly = false;
                    break;
                case "--no-room-tbx":
                    exportRoomTbx = false;
                    break;
                case "--no-building-tbx":
                    exportBuildingTbx = false;
                    break;
                case "--tbx-only":
                    tbxOnly = true;
                    exportImages = false;
                    exportObjects = false;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        if (string.IsNullOrWhiteSpace(output)
            || (!tilesOnly && string.IsNullOrWhiteSpace(input)))
        {
            return null;
        }

        if (tilesOnly && string.IsNullOrWhiteSpace(tiles) && string.IsNullOrWhiteSpace(modTiles))
        {
            throw new ArgumentException("--tiles-only requires --tiles and/or --mod-tiles");
        }

        return new ConverterOptions
        {
            InputDirectory = string.IsNullOrWhiteSpace(input) ? string.Empty : Path.GetFullPath(input),
            OutputDirectory = Path.GetFullPath(output),
            ProjectName = string.IsNullOrWhiteSpace(name) ? "ConvertedMap" : name,
            TilesPath = string.IsNullOrWhiteSpace(tiles) ? null : Path.GetFullPath(tiles),
            ModTilesPath = string.IsNullOrWhiteSpace(modTiles) ? null : Path.GetFullPath(modTiles),
            ExpectedSourceCellSize = expectedSourceCellSize,
            TargetCellSize = targetCellSize,
            CleanOutput = clean,
            ExportImages = exportImages,
            ExportTilePacks = exportTilePacks,
            ExportObjects = exportObjects,
            ExportRoomTbx = exportRoomTbx,
            ExportBuildingTbx = exportBuildingTbx,
            TbxOnly = tbxOnly,
            TilesOnly = tilesOnly,
            IncludeCells = includeCells
        };
    }

    public IEnumerable<string> EnumerateTilesAssetPaths()
    {
        if (!string.IsNullOrWhiteSpace(TilesPath))
        {
            yield return TilesPath;
        }

        if (!string.IsNullOrWhiteSpace(ModTilesPath))
        {
            yield return ModTilesPath;
        }
    }

    private static HashSet<CellCoord> ParseCells(string value)
    {
        var cells = new HashSet<CellCoord>();
        var tokens = value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var parts = token.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
            {
                throw new ArgumentException($"Invalid cell coordinate: {token}. Expected X_Y.");
            }

            cells.Add(new CellCoord(x, y));
        }

        if (cells.Count == 0)
        {
            throw new ArgumentException("--cells must contain at least one X_Y coordinate.");
        }

        return cells;
    }

    public static void PrintUsage()
    {
        Console.WriteLine("PZ Reverse Mapper CLI");
        Console.WriteLine("Project Zomboid compiled map reverse exporter");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  PZReverseMapper.Cli.exe --input <map-folder> --output <export-folder> [options]");
        Console.WriteLine("  PZReverseMapper.Cli.exe --output <export-folder> --tiles <media-folder> --tiles-only");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --tiles <file-or-folder>       Vanilla game media folder, .tiles file, or folder containing .tiles files");
        Console.WriteLine("  --mod-tiles <folder>           Optional mod asset root read after --tiles; alias: --mod-media");
        Console.WriteLine("  --name <project-name>          PZW filename without extension (default: ConvertedMap)");
        Console.WriteLine("  --source-cell-size <size>      Optional input validation, for example 300 or 256");
        Console.WriteLine("  --target-cell-size <size>      Output cell size (default: 300)");
        Console.WriteLine("  --cells X_Y[,X_Y...]           Optional source-cell filter, for example 46_26,46_27");
        Console.WriteLine("  --clean                        Move existing output contents to Recycle Bin after confirmation");
        Console.WriteLine("  --tbx-only                     Write TBX outputs only; skip TMX, PZW, and objects.lua");
        Console.WriteLine("  --no-images                    Do not write preview map images");
        Console.WriteLine("  --extract-tiles                Extract .pack atlases from --tiles and optional --mod-tiles");
        Console.WriteLine("  --tiles-only                   Extract tiles without reading map headers or lotpacks");
        Console.WriteLine("  --no-objects                   Do not import objects.lua into the PZW");
        Console.WriteLine("  --no-room-tbx                  Do not write RoomDef TBX files");
        Console.WriteLine("  --no-building-tbx              Do not write supplemental building TBX files");
    }
}
