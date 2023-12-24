namespace GameTranslator.Model;

public class FileDefinition
{
    public string FilePath { get; set; }

    public FileType Type { get; set; }

    public string? StoredPath { get; set; }
    public FileType? StoredType { get; set; }

    public FileType TypeToTranslate => StoredType ?? Type;
    public string PathToTranslate => StoredPath ?? FilePath;

    public string TranslatedPath { get; set; }
}

public enum FileType
{
    Json,
    Rpgmvp,
    Image,
    Js,
    LzString
}