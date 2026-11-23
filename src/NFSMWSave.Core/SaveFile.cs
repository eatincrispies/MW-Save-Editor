namespace NFSMWSave.Core;

public sealed class SaveFormatException(string message) : Exception(message);

public sealed class SaveFile
{
    public const int MaxAliasLength = 7;

    private readonly SaveContainer _container;
    private readonly SaveBuffer _buffer;
    private readonly HashSet<string> _presetBackups = new(StringComparer.OrdinalIgnoreCase);

    private SaveFile(SaveContainer container, SaveBuffer buffer, string? path)
    {
        _container = container;
        _buffer = buffer;
        Path = path;
        OriginalAlias = Bytes.Text(buffer.Data, buffer.Map.ProfileName, Layout.ProfileNameSize);
        Career = new Career(buffer);
        Stable = new CarStable(buffer, Career);
        Markers = new MarkerInventory(buffer);
        Stats = new RapSheet(buffer);
        Options = new GameOptions(buffer);
        Races = new RaceTable(buffer);
        Unlocks = new UnlockTable(buffer);
        Progress = new CareerProgress(buffer, Career, Races);
    }

    public string? Path { get; private set; }
    public string OriginalAlias { get; private set; }
    public SaveLayout Map => _buffer.Map;
    public string PlatformName => _buffer.Map.PlatformName;
    public string FormatName => _container.FormatName;
    public string? ResignNotice => _container.ResignNotice;
    public string? OpenNotice => _container.OpenNotice;
    public Career Career { get; }
    public CarStable Stable { get; }
    public MarkerInventory Markers { get; }
    public RapSheet Stats { get; }
    public GameOptions Options { get; }
    public RaceTable Races { get; }
    public UnlockTable Unlocks { get; }
    public CareerProgress Progress { get; }

    public static SaveFile Open(string path) => Parse(File.ReadAllBytes(path), path);

    public static SaveFile Parse(byte[] source, string? path = null)
    {
        var container = SaveContainer.Open((byte[])source.Clone());
        var profile = container.ExtractProfile();
        var map = SaveLayout.Detect(profile);
        if (profile.Length != map.FileSize)
        {
            throw new SaveFormatException("This save has an unsupported layout.");
        }
        var buffer = new SaveBuffer(profile, map);
        if (buffer.U32(0) != Layout.Magic || buffer.U32(Layout.FileLengthOffset) != map.StoredFileLength ||buffer.U32(Layout.InfoSizeOffset) != Layout.InfoSize)
        {
            throw new SaveFormatException("This save has an unsupported layout.");
        }
        return new SaveFile(container, buffer, path);
    }

    public static string? AliasProblem(string alias)
    {
        if (alias.Length is 0 or > MaxAliasLength)
        {
            return $"The alias must be 1 to {MaxAliasLength} characters.";
        }
        return alias.All(char.IsAsciiLetterOrDigit) ? null : "The alias can only use letters and numbers.";
    }

    public void SetAlias(string alias)
    {
        var problem = AliasProblem(alias);
        if (problem is not null)
        {
            throw new ArgumentException(problem, nameof(alias));
        }
        _buffer.Text(_buffer.Map.ProfileName, Layout.ProfileNameSize, Map.UppercaseAlias ? alias.ToUpperInvariant() : alias);
    }

    public ImportReport Import(SaveImport source, ImportSections sections)
    {
        var snapshot = (byte[])_buffer.Data.Clone();
        var pending = Stable.PendingState();
        try
        {
            return source.Apply(this, sections);
        }
        catch
        {
            snapshot.CopyTo(_buffer.Data, 0);
            Stable.RestorePending(pending);
            throw;
        }
    }

    public IntegrityReport Verify() => Integrity.Check(_buffer);

    public byte[] ProfileBytes()
    {
        Integrity.Seal(_buffer);
        return (byte[])_buffer.Data.Clone();
    }

    public byte[] ToBytes()
    {
        Integrity.Seal(_buffer);
        var output = _container.Build(_buffer.Data, Career.ProfileName);
        var check = SaveContainer.Open(output).ExtractProfile();
        if (!check.AsSpan().SequenceEqual(_buffer.Data))
        {
            throw new IOException("The rebuilt save did not read back correctly, so nothing was written.");
        }
        return output;
    }

    public string? Save(string path, bool backup)
    {
        var bytes = ToBytes();
        string? backupPath = null;
        if (backup && File.Exists(path))
        {
            backupPath = $"{path}.{DateTime.Now:yyyyMMdd-HHmmss}.bak";
            File.Copy(path, backupPath, false);
        }
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, true);
        if (Map.Platform == SavePlatform.PC)
        {
            if (Path is not null && string.Equals(System.IO.Path.GetFullPath(Path), System.IO.Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            {
                UnlimiterPresets.Retire(path, Stable.TakeDeletedPresets());
            }
            UnlimiterPresets.Sync(path, Stable.RepaintedCars, Stable.PresetHashes(), _presetBackups);
        }
        Path = path;
        return backupPath;
    }

    public void MarkSavedAt(string path)
    {
        Path = path;
        OriginalAlias = Career.ProfileName;
    }
}
