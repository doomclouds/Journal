namespace Journal.Infrastructure.Storage;

public sealed record JournalStorageOptions(string RootDirectory)
{
    public static JournalStorageOptions FromLocalAppData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new JournalStorageOptions(Path.Combine(localAppData, "Journal"));
    }
}
