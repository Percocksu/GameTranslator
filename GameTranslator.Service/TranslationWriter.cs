using System.Text;
using GameTranslator.FileManager;
using GameTranslator.Model;
using GameTranslator.Translator;
using GameTranslator.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameTranslator.Service;

public class TranslationWriter
{
    private readonly ITranslationStorage _translationStorage;
    private readonly AppSettings _appSettings;
    private readonly ITranslationAnalyser _translationAnalyser;
    private readonly ILogModule _logModule;
    private readonly TranslationSettings _translationSettings;
    private readonly IFileManager _fileManager;
    
    public TranslationWriter(ITranslationStorage translationStorage, AppSettings appSettings, ILogModule logModule, TranslationSettings translationSettings, ITranslationAnalyser translationAnalyser, IFileManager fileManager)
    {
        _translationStorage = translationStorage;
        _appSettings = appSettings;
        _logModule = logModule;
        _translationSettings = translationSettings;
        _translationAnalyser = translationAnalyser;
        _fileManager = fileManager;
    }
    
    public async Task WriteJsonTranslationsFromStorage()
    {
        await _logModule.WriteLog($"Writing json translations to temp files");
        var translations = await _translationStorage.ReadTranslations();

        foreach (var fileTranslation in translations.GameTranslations[_appSettings.GameConfig.DirectoryPath].JsonFiles)
        {
            await _logModule.WriteLog($"Processing file {fileTranslation.Key}");
            dynamic dynObj =
                JsonConvert.DeserializeObject(await File.ReadAllTextAsync(fileTranslation.Key, Encoding.UTF8));
            foreach (var jsonExtract in fileTranslation.Value.Where(x => !(x.Value.Unsafe ?? false)))
            {
                if (await _translationAnalyser.IsJsCode(jsonExtract.Value.Value))
                {
                    await _logModule.WriteLog($"Detected unsafe javascript: {jsonExtract.Value.Path}=>{jsonExtract.Value.Value}");
                    continue;
                }
                if (await _translationAnalyser.IsUnsafe(fileTranslation.Key, jsonExtract.Value))
                {
                    await _logModule.WriteLog($"Detected unsafe translation: {jsonExtract.Value.Path}=>{jsonExtract.Value.Value}");
                    continue;
                }
                
                var selectedPath = dynObj.SelectToken(jsonExtract.Value.Path);
                if (jsonExtract.Value.Translated.Contains("Floats and lives softly in a dark cave."))
                {
                    
                }
                if(selectedPath != null) 
                {
                    if (selectedPath.ToString() is string test
                        && test .Contains("Floats and lives softly in a dark cave."))
                    {
                        
                    }
                    
                    if (selectedPath is JValue jValue 
                        && jValue.Parent is JArray jArray)
                    {
                        var index = jArray.IndexOf(jValue);
                        jArray[index] = _translationAnalyser.FormatTranslation(fileTranslation.Key, jsonExtract.Value);
                    }
                    else
                    {
                        selectedPath.Replace(_translationAnalyser.FormatTranslation(fileTranslation.Key, jsonExtract.Value));
                    }
                }
            }

            await File.WriteAllTextAsync(fileTranslation.Key, JsonConvert.SerializeObject(dynObj, Formatting.Indented), Encoding.UTF8);
        }
        await _logModule.WriteLog($"Temp json files have been updated");
    }
    
    public async Task WriteJsTranslationsFromStorage()
    {
        await _logModule.WriteLog($"Writing js translations to temp files");
        var translations = await _translationStorage.ReadTranslations();
        foreach (var fileTranslation in translations.GameTranslations[_appSettings.GameConfig.DirectoryPath].JsFiles)
        {
            var jsContent = await File.ReadAllTextAsync(fileTranslation.Key, Encoding.UTF8);
            foreach (var jsExtract in fileTranslation.Value.Where(x => !(x.Value.Unsafe ?? false)))
            {
                if (await _translationAnalyser.IsJsCode(jsExtract.Value.Value))
                {
                    await _logModule.WriteLog($"Detected unsafe javascript: {jsExtract.Value.Path}=>{jsExtract.Value.Value}");
                    continue;
                }
                if (await _translationAnalyser.IsUnsafe(fileTranslation.Key, jsExtract.Value))
                {
                    await _logModule.WriteLog($"Detected unsafe translation: {jsExtract.Value.Path}=>{jsExtract.Value.Value}");
                    continue;
                }

                if (!jsExtract.Value.Translated.StartsWith('"')
                    || !jsExtract.Value.Translated.EndsWith('"'))
                {
                    await _logModule.WriteLog($"Wrong js string: {jsExtract.Value.Path} => {jsExtract.Value.Translated}");
                    continue;
                }
                
                jsContent = jsContent.Replace(jsExtract.Value.Value, jsExtract.Value.Translated);
            }
            await File.WriteAllTextAsync(fileTranslation.Key, jsContent, Encoding.UTF8);
        }

        await _logModule.WriteLog($"Temp js files have been updated");
    }
}