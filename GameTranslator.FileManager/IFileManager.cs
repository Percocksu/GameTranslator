using GameTranslator.Model;

namespace GameTranslator.FileManager;

public interface IFileManager
{
    Task<FileDefinition[]> FindFilesToTranslate();
    Task StoreFiles(FileDefinition[] filesDefinition);
    Task CleanStoredFiles();
}