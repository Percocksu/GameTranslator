using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using GameTranslator.FileManager;
using GameTranslator.Model;
using GameTranslator.Translator;
using GameTranslator.Utils;

namespace GameTranslator.Service;

public class GameTranslatorService
{
    private readonly ILogModule _logModule;
    private readonly IFileManager _fileManager;
    private readonly JsonExtractor _jsonExtractor;
    private readonly JsExtractor _jsExtractor;
    private readonly ITranslator _translator;
    private readonly AppSettings _appSettings;
    private readonly TranslationWriter _translationWriter;
    private readonly TranslationSettings _translationSettings;
    private readonly ITranslationStorage _translationStorage;
    
    public GameTranslatorService(ILogModule logModule, IFileManager fileManager, JsonExtractor jsonExtractor, ITranslator translator, AppSettings appSettings, TranslationWriter translationWriter, TranslationSettings translationSettings, ITranslationStorage translationStorage, JsExtractor jsExtractor)
    {
        _logModule = logModule;
        _fileManager = fileManager;
        _jsonExtractor = jsonExtractor;
        _translator = translator;
        _appSettings = appSettings;
        _translationWriter = translationWriter;
        _translationSettings = translationSettings;
        _translationStorage = translationStorage;
        _jsExtractor = jsExtractor;
    }

    public async Task CopyFilesToTemp()
    {
        await _fileManager.CleanStoredFiles();
        var files = await _fileManager.FindFilesToTranslate();
        await _fileManager.StoreFiles(files);
    }
    
    public async Task TranslateGame()
    {
        await _fileManager.CleanStoredFiles();
        var files = await _fileManager.FindFilesToTranslate();
        await _fileManager.StoreFiles(files);
        await _logModule.WriteLog("Collecting json chunks to translate");
        var jsonFiles = files.Where(x => x.StoredType == FileType.Json
            && x.Type != FileType.LzString).ToList();
        var textExtracts = new List<TextExtractFile>();
        foreach (var fileDefinition in jsonFiles)
        {
            await _logModule.WriteLog($"Json file {jsonFiles.IndexOf(fileDefinition)+1}/{jsonFiles.Count}");
            var extracts = await _jsonExtractor.ExtractToTranslate(fileDefinition);
            textExtracts.AddRange(extracts.Select(x => new TextExtractFile
            {
                TextExtract = x,
                FileDefinition = fileDefinition
            }));
        }
        await _translator.TranslateTextExtracts(textExtracts.Where(x => !(x.TextExtract.Unsafe ?? false)).ToList());
        foreach (var groupedTextExtracts in textExtracts
                     .GroupBy(x => x.FileDefinition))
        {
            await _translationStorage.WriteUnsafeJson(groupedTextExtracts.Key, 
                groupedTextExtracts
                    .Where(x => x.TextExtract.Unsafe ?? false)
                    .Select(x => x.TextExtract).ToArray());
        }

        if (_translationSettings.UpdateJsConst)
        {
            textExtracts.Clear();
            var unsafeJson = (await _translationStorage.ReadTranslations())
                .GameTranslations[_appSettings.GameConfig.DirectoryPath]
                .JsonUnsafe.Distinct().ToDictionary(x => x, x => x);
            await _logModule.WriteLog("Collecting js chunks to translate");
            var jsFiles = files.Where(x => x.StoredType == FileType.Js).ToList();
            foreach (var fileDefinition in jsFiles)
            {
                await _logModule.WriteLog($"Js file {jsFiles.IndexOf(fileDefinition)+1}/{jsFiles.Count}");
                var extracts = (await _jsExtractor.ExtractToTranslate(fileDefinition));
                extracts = extracts.Where(x => !unsafeJson.ContainsKey(x.Value.Trim('"'))).ToList();
                textExtracts.AddRange(extracts.Where(x => !(x.Unsafe ?? false)).Select(x => new TextExtractFile
                {
                    TextExtract = x,
                    FileDefinition = fileDefinition
                }));
            }
            await _translator.TranslateTextExtracts(textExtracts);
        }

        await _logModule.WriteLog($"All temp file updated, you can copy into the game folder");
    }

    public async Task WriteTranslationsToTemp()
    {
        await _translationWriter.WriteJsonTranslationsFromStorage();
        await _translationWriter.WriteJsTranslationsFromStorage();
    }
}