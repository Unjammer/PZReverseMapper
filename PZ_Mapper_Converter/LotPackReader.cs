namespace PZ_Mapper_Converter;

internal sealed class LotPackReader
{
    private readonly LotHeaderData _header;
    private readonly string _file;

    public LotPackReader(LotHeaderData header, string inputDirectory)
    {
        _header = header;
        _file = Path.Combine(inputDirectory, $"world_{header.CellX}_{header.CellY}.lotpack");
    }

    public bool Exists => File.Exists(_file);

    public void Read(Action<int, int, int, int, string> onTile)
    {
        if (!Exists)
        {
            return;
        }

        using var reader = new BinaryReader(File.OpenRead(_file));
        var hasMagic = BinaryHelpers.TryReadMagic(reader, "LOTP");
        var version = 0;
        var tableStart = 4;

        if (hasMagic)
        {
            version = reader.ReadInt32();
            if (version is < 0 or > 1)
            {
                throw new InvalidDataException($"{Path.GetFileName(_file)} has unsupported lotpack version {version}");
            }

            if (version >= 1)
            {
                var headerValue = reader.ReadInt32();
                var expectedChunkCount = _header.ChunksPerCell * _header.ChunksPerCell;
                if (headerValue != _header.ChunkDim && headerValue != expectedChunkCount)
                {
                    Console.WriteLine($"Warning: {_file} lotpack header value is {headerValue}, expected chunk size {_header.ChunkDim} or chunk count {expectedChunkCount}");
                }

                tableStart = 12;
            }
        }
        else
        {
            reader.BaseStream.Position = 0;
        }

        for (var chunkX = 0; chunkX < _header.ChunksPerCell; chunkX++)
        {
            for (var chunkY = 0; chunkY < _header.ChunksPerCell; chunkY++)
            {
                ReadChunk(reader, version, tableStart, chunkX, chunkY, onTile);
            }
        }
    }

    private void ReadChunk(
        BinaryReader reader,
        int version,
        int tableStart,
        int chunkX,
        int chunkY,
        Action<int, int, int, int, string> onTile)
    {
        var index = chunkX * _header.ChunksPerCell + chunkY;
        reader.BaseStream.Position = tableStart + index * 8L;
        var chunkOffset = version >= 1 ? reader.ReadInt64() : reader.ReadInt32();
        if (chunkOffset <= 0 || chunkOffset >= reader.BaseStream.Length)
        {
            return;
        }

        reader.BaseStream.Position = chunkOffset;
        var skip = 0;

        for (var z = _header.MinLevel; z <= _header.MaxLevel; z++)
        {
            for (var x = 0; x < _header.ChunkDim; x++)
            {
                for (var y = 0; y < _header.ChunkDim; y++)
                {
                    var localX = chunkX * _header.ChunkDim + x;
                    var localY = chunkY * _header.ChunkDim + y;

                    if (skip > 0)
                    {
                        skip--;
                        continue;
                    }

                    var count = reader.ReadInt32();
                    if (count == -1)
                    {
                        skip = reader.ReadInt32();
                        if (skip > 0)
                        {
                            skip--;
                            continue;
                        }
                    }

                    if (count <= 1)
                    {
                        continue;
                    }

                    reader.ReadInt32();
                    for (var layer = 1; layer < count; layer++)
                    {
                        var tileIndex = reader.ReadInt32();
                        if ((uint)tileIndex >= (uint)_header.TilesUsed.Count)
                        {
                            Console.WriteLine($"Warning: tile index {tileIndex} is outside {_header.CellX}_{_header.CellY}.lotheader");
                            continue;
                        }

                        onTile(localX, localY, z, layer, _header.TilesUsed[tileIndex]);
                    }
                }
            }
        }
    }
}
