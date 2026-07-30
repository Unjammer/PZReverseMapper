using System.Text;

namespace PZ_Mapper_Converter;

internal static class BinaryHelpers
{
    public static string ReadLineString(BinaryReader reader)
    {
        var bytes = new List<byte>(64);
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var b = reader.ReadByte();
            if (b == 10)
            {
                break;
            }

            if (b != 13)
            {
                bytes.Add(b);
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public static bool TryReadMagic(BinaryReader reader, string expected)
    {
        if (reader.BaseStream.Length < 4)
        {
            return false;
        }

        var start = reader.BaseStream.Position;
        var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (magic == expected)
        {
            return true;
        }

        reader.BaseStream.Position = start;
        return false;
    }

    public static int FloorDiv(int value, int divisor)
    {
        var result = value / divisor;
        var remainder = value % divisor;
        return remainder != 0 && ((value < 0) ^ (divisor < 0)) ? result - 1 : result;
    }

    public static int PositiveMod(int value, int divisor)
    {
        var result = value % divisor;
        return result < 0 ? result + Math.Abs(divisor) : result;
    }
}
