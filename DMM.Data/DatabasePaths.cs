namespace DMM.Data;

public static class DatabasePaths
{
    public const string CompanyFolderName = "ZeeOgre";
    public const string ProductFolderName = "DevModManager";
    public const string DatabaseFileName = "DMM.db";

    public static string GetDatabaseDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, CompanyFolderName, ProductFolderName);
    }

    public static string GetDatabasePath() => Path.Combine(GetDatabaseDirectory(), DatabaseFileName);
}
