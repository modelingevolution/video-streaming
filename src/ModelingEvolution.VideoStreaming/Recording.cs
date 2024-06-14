namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014
public record Recording(string FileName, string FullPath, string Name, DateTime Started, TimeSpan Duration, Bytes Size)
{
    private string? _displayName;
    private bool _displayNameLoaded;
    public string DisplayName
    {
        get
        {
            if (_displayNameLoaded)
                return _displayName;
            string metadata = FullPath + ".name";
            _displayName = File.Exists(metadata) ? File.ReadAllText(metadata) : Name;
            _displayNameLoaded = true;
            return _displayName;
        }
        set
        {
            string metadata = FullPath + ".name";
            File.WriteAllText(metadata, value);
            _displayName = value;
        }
    }
    public void Delete()
    {
        File.Delete(FullPath);
    }
}