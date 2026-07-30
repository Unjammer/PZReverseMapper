using PZ_Mapper_Converter;

try
{
    if (args.Any(a => a is "-h" or "--help" or "/?"))
    {
        ConverterOptions.PrintUsage();
        return 0;
    }

    var options = ConverterOptions.Parse(args);
    if (options is null)
    {
        ConverterOptions.PrintUsage();
        return 1;
    }

    if (!ConfirmCleanOutputIfNeeded(options))
    {
        Console.Error.WriteLine("Export canceled: output cleanup was not confirmed.");
        return 1;
    }

    var converter = new MapConverter(options);
    var result = converter.Run();

    Console.WriteLine();
    Console.WriteLine("Conversion finished");
    Console.WriteLine($"Input cells:  {result.SourceCellCount}");
    Console.WriteLine($"TMX cells:    {result.TargetCellCount}");
    Console.WriteLine($"Objects:      {result.ObjectCount}");
    Console.WriteLine($"Images:       {result.ImageCount}");
    Console.WriteLine($"Tile images:  {result.TileImageCount}");
    Console.WriteLine($"TBX files:    {result.TbxCount}");
    Console.WriteLine($"Buildings:    {result.BuildingTbxCount}");
    Console.WriteLine($"Tilesets:     {result.TileSetCount}");
    Console.WriteLine($"Elapsed:      {MapConverter.FormatElapsed(result.Elapsed)}");
    Console.WriteLine($"Output:       {result.OutputDirectory}");
    Console.WriteLine($"Project:      {(string.IsNullOrWhiteSpace(result.ProjectFile) ? "not written" : result.ProjectFile)}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

static bool ConfirmCleanOutputIfNeeded(ConverterOptions options)
{
    if (!options.CleanOutput || !OutputCleaner.HasContent(options.OutputDirectory))
    {
        return true;
    }

    OutputCleaner.EnsureSafeCleanTarget(options.OutputDirectory);

    Console.Error.WriteLine("Clean output is enabled.");
    Console.Error.WriteLine("Existing files and folders in this output directory will be moved to the Recycle Bin:");
    Console.Error.WriteLine(options.OutputDirectory);
    Console.Error.Write("Type CLEAN to continue, or anything else to cancel: ");

    var answer = Console.ReadLine();
    if (!string.Equals(answer, "CLEAN", StringComparison.Ordinal))
    {
        return false;
    }

    options.CleanOutputConfirmed = true;
    return true;
}
