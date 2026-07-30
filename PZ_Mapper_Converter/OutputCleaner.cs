using Microsoft.VisualBasic.FileIO;

namespace PZ_Mapper_Converter;

internal static class OutputCleaner
{
    public static bool HasContent(string directory)
    {
        return Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any();
    }

    public static int CleanToRecycleBin(string outputDirectory)
    {
        EnsureSafeCleanTarget(outputDirectory);
        if (!Directory.Exists(outputDirectory))
        {
            return 0;
        }

        var entries = Directory.EnumerateFileSystemEntries(outputDirectory).ToArray();
        foreach (var entry in entries)
        {
            MoveEntryToRecycleBin(entry);
        }

        return entries.Length;
    }

    public static void EnsureSafeCleanTarget(string outputDirectory)
    {
        var fullPath = NormalizePath(outputDirectory);
        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrWhiteSpace(root) &&
            string.Equals(TrimPath(fullPath), TrimPath(Path.GetFullPath(root)), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to clean drive root: {fullPath}");
        }

        foreach (var protectedPath in EnumerateProtectedPaths())
        {
            if (string.Equals(TrimPath(fullPath), TrimPath(protectedPath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Refusing to clean protected folder: {fullPath}. Select or create a dedicated export folder instead.");
            }
        }
    }

    private static void MoveEntryToRecycleBin(string entry)
    {
        var attributes = File.GetAttributes(entry);
        if (attributes.HasFlag(FileAttributes.Directory))
        {
            FileSystem.DeleteDirectory(entry, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            return;
        }

        FileSystem.DeleteFile(entry, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    }

    private static IEnumerable<string> EnumerateProtectedPaths()
    {
        var specialFolders = new[]
        {
            Environment.SpecialFolder.Desktop,
            Environment.SpecialFolder.DesktopDirectory,
            Environment.SpecialFolder.MyDocuments,
            Environment.SpecialFolder.MyPictures,
            Environment.SpecialFolder.MyMusic,
            Environment.SpecialFolder.MyVideos,
            Environment.SpecialFolder.UserProfile
        };

        foreach (var folder in specialFolders)
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return NormalizePath(path);
            }
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            yield return NormalizePath(Path.Combine(userProfile, "Downloads"));
        }
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path);
    }

    private static string TrimPath(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
