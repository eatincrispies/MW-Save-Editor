using System.Text;

namespace NFSMWSave.Core;

public sealed record GameProfile(string Name, string Path, uint Stamp);

public static class GameFolder
{
    private const int HeaderNameOffset = 8;
    private const int HeaderNameBytes = 256;

    public static string Root => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NFS Most Wanted");

    public static IReadOnlyList<GameProfile> FindProfiles()
    {
        var found = new List<GameProfile>();
        if (!Directory.Exists(Root))
        {
            return found;
        }
        foreach (var folder in Directory.EnumerateDirectories(Root))
        {
            var name = System.IO.Path.GetFileName(folder);
            var path = System.IO.Path.Combine(folder, name);
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length == SaveLayout.Pc.FileSize)
                {
                    found.Add(new GameProfile(name, path, SaveFile.Open(path).Career.StableStamp));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SaveFormatException)
            {
            }
        }
        return found.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static uint? InstalledStamp(string? excludePath)
    {
        var stamps = FindProfiles()
            .Where(p => excludePath is null || !string.Equals(System.IO.Path.GetFullPath(p.Path), System.IO.Path.GetFullPath(excludePath), StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.Stamp)
            .OrderByDescending(g => g.Count())
            .ToList();
        return stamps.Count == 0 ? null : stamps[0].Key;
    }

    public static string? RenameTarget(string path, string oldAlias, string alias)
    {
        var folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
        if (folder is null || oldAlias == alias)
        {
            return null;
        }
        var fileName = System.IO.Path.GetFileName(path);
        var folderName = System.IO.Path.GetFileName(folder);
        if (!string.Equals(fileName, oldAlias, StringComparison.OrdinalIgnoreCase) || !string.Equals(folderName, oldAlias, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var parent = System.IO.Path.GetDirectoryName(folder);
        return parent is null ? null : System.IO.Path.Combine(parent, alias, alias);
    }

    public static string MoveProfile(string path, string alias)
    {
        var folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!;
        var oldName = System.IO.Path.GetFileName(path);
        var parent = System.IO.Path.GetDirectoryName(folder)!;
        var newFolder = System.IO.Path.Combine(parent, alias);
        var caseOnly = string.Equals(folder, newFolder, StringComparison.OrdinalIgnoreCase);
        if (!caseOnly && Directory.Exists(newFolder))
        {
            throw new IOException($"A profile named {alias} already exists.");
        }
        if (caseOnly)
        {
            var step = newFolder + ".renaming";
            Directory.Move(folder, step);
            Directory.Move(step, newFolder);
        }
        else
        {
            Directory.Move(folder, newFolder);
        }
        var moved = System.IO.Path.Combine(newFolder, oldName);
        var finalPath = System.IO.Path.Combine(newFolder, alias);
        if (!string.Equals(oldName, alias, StringComparison.Ordinal))
        {
            var step = moved + ".renaming";
            File.Move(moved, step);
            File.Move(step, finalPath);
        }
        MoveXenia360Header(parent, oldName, alias);
        return finalPath;
    }

    private static void MoveXenia360Header(string contentFolder, string oldName, string alias)
    {
        var titleFolder = System.IO.Path.GetDirectoryName(contentFolder);
        if (titleFolder is null)
        {
            return;
        }
        var headers = System.IO.Path.Combine(titleFolder, "Headers", System.IO.Path.GetFileName(contentFolder));
        var header = System.IO.Path.Combine(headers, oldName + ".header");
        if (!File.Exists(header))
        {
            return;
        }
        var data = File.ReadAllBytes(header);
        if (data.Length >= HeaderNameOffset + HeaderNameBytes)
        {
            data.AsSpan(HeaderNameOffset, HeaderNameBytes).Clear();
            Encoding.BigEndianUnicode.GetBytes(alias).CopyTo(data, HeaderNameOffset);
        }
        File.WriteAllBytes(System.IO.Path.Combine(headers, alias + ".header"), data);
        if (!string.Equals(oldName, alias, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(header);
        }
    }
}
