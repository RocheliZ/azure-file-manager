namespace FileManager.Api.Options;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "files";
}
