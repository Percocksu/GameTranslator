using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GameTranslator.FileManager;
using GameTranslator.Model;
using GameTranslator.Translator;
using GameTranslator.Utils;
using Newtonsoft.Json;

namespace GameTranslator.Rpgm;

public class RpgmFileManager : IFileManager
{
    private readonly ILogModule _logModule;
    private readonly AppSettings _appSettings;
    private readonly TranslationSettings _translationSettings;
    private readonly LzStringConvert _lzStringConvert;
    private readonly RpgmvpConvert _rpgmvpConvert;

    public RpgmFileManager(ILogModule logModule, AppSettings appSettings, TranslationSettings translationSettings, LzStringConvert lzStringConvert, RpgmvpConvert rpgmvpConvert)
    {
        _logModule = logModule;
        _appSettings = appSettings;
        _translationSettings = translationSettings;
        _lzStringConvert = lzStringConvert;
        _rpgmvpConvert = rpgmvpConvert;
    }
    
    public async Task<FileDefinition[]> FindFilesToTranslate()
    {
        var jsonFiles = Directory.EnumerateFiles(_appSettings.GameConfig.DirectoryPath, "*.json", SearchOption.AllDirectories)
            .ToArray();
        await _logModule.WriteLog($"{jsonFiles.Length} json files found");
        var jsonToTranslate = new List<string>();
        foreach (var jsonFile in jsonFiles)
        {
            var jsonContent = await File.ReadAllTextAsync(jsonFile, Encoding.UTF8);
            if (jsonContent.Any(c => c.IsJapanese()))
            {
                jsonToTranslate.Add(jsonFile);
            }
        }
        await _logModule.WriteLog($"{jsonToTranslate.Count} json files to translate");
        
        var jsFiles = Directory.EnumerateFiles(Path.Combine(_appSettings.GameConfig.DirectoryPath, _appSettings.GameConfig.Version == "mv" 
                    ? @"www\js" 
                    : @"data\www\js")
                , "*.js", SearchOption.AllDirectories)
            .Where(x => (_translationSettings.JsFilesRegex ?? Array.Empty<string>()).Any(y => Regex.IsMatch(x, y)))
            .ToArray();
        var jsToTranslate = new List<string>();
        foreach (var jsFile in jsFiles)
        {
            var jsContent = await File.ReadAllTextAsync(jsFile, Encoding.UTF8);
            if (jsContent.Any(c => c.IsJapanese()))
            {
                jsToTranslate.Add(jsFile);
            }
        }
        await _logModule.WriteLog($"{jsToTranslate.Count} js files to translate");
        
        var imagesFile = Directory.EnumerateFiles(_appSettings.GameConfig.DirectoryPath, "*.rpgmvp", SearchOption.AllDirectories)
            .ToArray();
        await _logModule.WriteLog($"{imagesFile.Length} image files found");

        var saveFiles = Directory.EnumerateFiles(_appSettings.GameConfig.DirectoryPath, "*.rpgsave", SearchOption.AllDirectories)
            .ToArray();
        await _logModule.WriteLog($"{saveFiles.Length} rpgsave files found");
        
        return jsonToTranslate.Select(x => new FileDefinition
        {
            FilePath = x,
            Type = FileType.Json
        }).Concat(jsToTranslate.Select(x => new FileDefinition
            {
                FilePath = x,
                Type = FileType.Js
            })).Concat(imagesFile.Select(x => new FileDefinition
        {
            FilePath = x,
            Type = FileType.Rpgmvp
        }).Concat(saveFiles.Select(x => new FileDefinition
            {
                FilePath = x,
                Type = FileType.LzString
            })))
            .ToArray();
    }

    public string GameTempDirectory => Path.Combine(Path.Combine(_appSettings.TempDir,
        new DirectoryInfo(_appSettings.GameConfig.DirectoryPath).Name));
    
    private string GetTempPath(FileDefinition fileDefinition)
    {
        var relativePath = fileDefinition.FilePath.Replace(_appSettings.GameConfig.DirectoryPath + @"\", "");
        return Path.Combine(GameTempDirectory
            , fileDefinition.Type == FileType.Rpgmvp 
                ? relativePath.Replace(Path.GetFileName(relativePath), $"{Path.GetFileNameWithoutExtension(relativePath)}.png")
                : relativePath);
    }
    
    public async Task StoreFiles(FileDefinition[] filesDefinition)
    {
        await _logModule.WriteLog($"Copying files to temp directory {GameTempDirectory}");

        var filesToConvert = filesDefinition.Where(x => x.Type == FileType.Rpgmvp).ToArray();
        if (_appSettings.ExtractImages)
        {
            await _logModule.WriteLog($"Extracting {filesToConvert.Length} Rpgmvp");
            foreach (var fileDefinition in filesToConvert)
            {
                var extractPath = GetTempPath(fileDefinition);
                //We keep image due to extract and copy time required
                if (File.Exists(extractPath))
                {
                    continue;
                }
            
                if (!Directory.Exists(Path.GetDirectoryName(extractPath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(extractPath));
                }
                await _rpgmvpConvert.Extract(fileDefinition.FilePath, extractPath);
                fileDefinition.StoredPath = extractPath;
                fileDefinition.StoredType = FileType.Image;
            }
        }
        
        var filesToCopy = filesDefinition.Where(x => x.Type == FileType.Js
            || x.Type == FileType.Json).ToArray();
        foreach (var fileDefinition in filesToCopy)
        {
            var copyPath = GetTempPath(fileDefinition);
            if (!Directory.Exists(Path.GetDirectoryName(copyPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(copyPath));
            }
            File.Copy(fileDefinition.FilePath, copyPath);
            fileDefinition.StoredPath = copyPath;
            fileDefinition.StoredType = Path.GetExtension(copyPath) == ".json" 
                ? FileType.Json
                : FileType.Js;
        }
        
        filesToConvert = filesDefinition.Where(x => x.Type == FileType.LzString).ToArray();
        await _logModule.WriteLog($"Extracting {filesToConvert.Length} rpgsave");
        foreach (var fileDefinition in filesToConvert)
        {
            var extractPath = GetTempPath(fileDefinition);
            var jsonExtractPath = extractPath.Replace(Path.GetFileName(extractPath)
                , $"{Path.GetFileNameWithoutExtension(extractPath)}.json");
            if (!Directory.Exists(Path.GetDirectoryName(extractPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(extractPath));
            }
            var content = await _lzStringConvert.ExtractLzString(fileDefinition.FilePath);
            try
            {
                var obj = JsonConvert.DeserializeObject(content);
                await File.WriteAllTextAsync(jsonExtractPath
                    , JsonConvert.SerializeObject(obj, Formatting.Indented));
                File.Copy(fileDefinition.FilePath, $"{extractPath}.bk");
            }
            catch (Exception e)
            {
                await _logModule.WriteLog($"Failed to deserialize save file {fileDefinition.FilePath}");
            }
            fileDefinition.StoredPath = jsonExtractPath;
            fileDefinition.StoredType = FileType.Json;
        }
    }

    public Task CleanStoredFiles()
    {
        if (!Directory.Exists(GameTempDirectory))
            return Task.CompletedTask;
        
        var allFiles = Directory.EnumerateFiles(GameTempDirectory, "*", SearchOption.AllDirectories)
            .ToArray();
        var blackList = new[] { ".bak" }.Concat(_appSettings.KeepTempImages 
            ? new []{ ".png", ".jpg" } 
            : Array.Empty<string>());
        foreach (var file in allFiles.Where(x => blackList.All(y => y != Path.GetExtension(x))))
        {
            File.Delete(file);
        }
        
        return Task.CompletedTask;
    }
}