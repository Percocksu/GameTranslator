using System.Text;
using System.Text.RegularExpressions;
using GameTranslator.Model;
using GameTranslator.Utils;
using Newtonsoft.Json;

namespace GameTranslator.Translator;

public interface ITranslationStorage
{
    Task<Translations> ReadTranslations();
    Task WriteTranslations(FileDefinition fileDefinition, TextExtract[] textExtracts);
    Task WriteUnsafeJson(FileDefinition fileDefinition, TextExtract[] textExtracts);
    Task ApplyReplace();
    Task FixInconsistencies();
    Task FixInconsistenciesInNames();
    Task ClearJapanese();
    Task ClearUnsafeJs();
    Task ClearUnsafeTranslations();
    Task UpdateFile();
    Task MergeTranslations();
}

public class JsonTranslationStorage : ITranslationStorage
{
    private readonly AppSettings _appSettings;
    private readonly ILogModule _logModule;
    private readonly ITranslationAnalyser _translationAnalyser;
    private readonly IInputProvider _inputProvider;
    private Translations? _cachedTranslations;
    private static SemaphoreSlim  _saveLock = new(1, 1);
    private static SemaphoreSlim  _writeLock = new(1, 1);

    public JsonTranslationStorage(AppSettings appSettings, ILogModule logModule, ITranslationAnalyser translationAnalyser, IInputProvider inputProvider)
    {
        _appSettings = appSettings;
        _logModule = logModule;
        _translationAnalyser = translationAnalyser;
        _inputProvider = inputProvider;
    }

    
    private string StorageFile => $"{new DirectoryInfo(_appSettings.GameConfig.DirectoryPath).Name}_TsStorage.json";

    private async Task SaveTranslations(Translations translations)
    {
        await _saveLock.WaitAsync();
        await using var writer = File.CreateText(StorageFile);
        await writer.WriteAsync(JsonConvert.SerializeObject(translations, Formatting.Indented));
        _saveLock.Release();
    }

    public async Task<Translations> ReadTranslations()
    {
        if (!File.Exists(StorageFile))
        {
            return new Translations();
        }

        if (_cachedTranslations == null)
        {
            _cachedTranslations =
                JsonConvert.DeserializeObject<Translations>(await File.ReadAllTextAsync(StorageFile, Encoding.UTF8))
                ?? new Translations();
        }

        return _cachedTranslations;
    }
    
    public async Task WriteTranslations(FileDefinition fileDefinition, TextExtract[] textExtracts)
    {
        await _writeLock.WaitAsync();
        if (!File.Exists(StorageFile))
        {
            await SaveTranslations(new Translations());
        }

        var gameKey = _appSettings.GameConfig.DirectoryPath;
        var translations = await ReadTranslations();
        if (!translations.GameTranslations.ContainsKey(gameKey))
        {
            translations.GameTranslations.Add(gameKey,
                new GameTranslation());
        }

        var fileKey = fileDefinition.PathToTranslate;
        if (fileDefinition.Type == FileType.Json)
        {
            if (!translations.GameTranslations[gameKey].JsonFiles.ContainsKey(fileKey))
            {
                translations.GameTranslations[gameKey].JsonFiles[fileKey] = new Dictionary<string, JsonExtract>();
            }
        }
        if (fileDefinition.Type == FileType.Js)
        {
            if (!translations.GameTranslations[gameKey].JsFiles.ContainsKey(fileKey))
            {
                translations.GameTranslations[gameKey].JsFiles[fileKey] = new Dictionary<string, JsExtract>();
            }
        }
        
        foreach (var jsonExtract in textExtracts.OfType<JsonExtract>())
        {
            if(translations.GameTranslations[gameKey].JsonFiles[fileKey].TryGetValue(jsonExtract.Path, out var existing))
            {
                existing.Translated = jsonExtract.Translated;
            }
            else
            {
                translations.GameTranslations[gameKey].JsonFiles[fileKey].Add(jsonExtract.Path, jsonExtract);
            }
        }
        
        foreach (var jsExtract in textExtracts.OfType<JsExtract>())
        {
            if(translations.GameTranslations[gameKey].JsFiles[fileKey].TryGetValue(jsExtract.Path, out var existing))
            {
                existing.Translated = jsExtract.Translated;
            }
            else
            {
                translations.GameTranslations[gameKey].JsFiles[fileKey].Add(jsExtract.Path, jsExtract);
            }
        }
        
        await SaveTranslations(translations);
        _writeLock.Release();
    }

    public async Task WriteUnsafeJson(FileDefinition fileDefinition, TextExtract[] textExtracts)
    {
        if (!textExtracts.Any())
            return;
            
        if (!File.Exists(StorageFile))
        {
            await SaveTranslations(new Translations());
        }
        var gameKey = _appSettings.GameConfig.DirectoryPath;
        var translations = await ReadTranslations();
        if (!translations.GameTranslations.ContainsKey(gameKey))
        {
            translations.GameTranslations.Add(gameKey,
                new GameTranslation());
        }

        foreach (var textExtract in textExtracts.DistinctBy(x => x.Value))
        {
            if (!translations.GameTranslations[gameKey].JsonUnsafe.Contains(textExtract.Value))
            {
                translations.GameTranslations[gameKey].JsonUnsafe.Add(textExtract.Value);
            }
        }
        
        await SaveTranslations(translations);
    }

    public async Task ApplyReplace()
    {
        var translations = await ReadTranslations();
        if (!translations.GameTranslations.ContainsKey(_appSettings.GameConfig.DirectoryPath))
            return;
            
        var gameTranslation = translations.GameTranslations[_appSettings.GameConfig.DirectoryPath];
        var extracts = gameTranslation.JsonFiles.SelectMany(x => x.Value.Select(y => y.Value))
            .Concat(gameTranslation.JsFiles.SelectMany(x => x.Value.Select(y => y.Value)).Cast<TextExtract>())
            .ToArray();
        foreach (var extract in extracts)
        {
            extract.Translated = _translationAnalyser.ReplaceTranslation(extract);
        }

        await SaveTranslations(translations);
    }

    public async Task FixInconsistencies()
    {
        var translations = await ReadTranslations();
        if (!translations.GameTranslations.ContainsKey(_appSettings.GameConfig.DirectoryPath))
            return;
        
        var inconsistencies = await _translationAnalyser.FindInconsistencies(
            translations.GameTranslations[_appSettings.GameConfig.DirectoryPath]);
        foreach (var inconsistency in inconsistencies)
        {
            var grouped = inconsistency.Value.Select(x => new { x.Value, x.Translated }).Distinct().ToArray();
            for (var i = 0; i < grouped.Length; i++)
            {
                await _logModule.WriteLog($"[{i}] {grouped[i].Value} => {grouped[i].Translated}");
            }
            await _logModule.WriteLog($"Please select the number you want to keep (-1 to ignore)");
            var selectedIndex = -1;
            while (!int.TryParse(await _inputProvider.ProvideUserInput(), out selectedIndex))
            {
                await _logModule.WriteLog($"Failed to parse answer to integer");
            }

            var selected = grouped[selectedIndex];
            await _logModule.WriteLog($"Updating all '{inconsistency.Key}' to '{selected.Translated}'");
            foreach (var extract in inconsistency.Value)
            {
                if (extract is JsExtract)
                {
                    extract.Translated = selected.Translated.Trim('"').PadLeft('"').PadRight('"');
                }
                else
                {
                    extract.Translated = selected.Translated;
                }
            }
        }

        if (inconsistencies.Any())
        {
            await _logModule.WriteLog($"Saving file");
            await SaveTranslations(translations);
        }
        else
        {
            await _logModule.WriteLog($"No inconsistencies found");
        }
    }

    public async Task FixInconsistenciesInNames()
    {
        var translations = await ReadTranslations();
        if (!translations.GameTranslations.ContainsKey(_appSettings.GameConfig.DirectoryPath))
            return;
        
        var names = await _translationAnalyser.FindNames(
            translations.GameTranslations[_appSettings.GameConfig.DirectoryPath]);

        var regex = new Regex(@"[\d\u3000-\u303f\u3040-\u309f\u30a0-\u30ff\uff00-\uff9f\u4e00-\u9faf\u3400-\u4dbfa-zA-Z]+");
        foreach (var nameExtracts in names)
        {
            var clearName = regex.Matches(nameExtracts.Key).LastOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(clearName))
            {
                await _logModule.WriteLog($"Failed to get clear name from {nameExtracts.Key}");
                continue;
            }

            var knownName = translations.GameTranslations[_appSettings.GameConfig.DirectoryPath]
                .JsonFiles.SelectMany(x => x.Value.Select(y => y.Value))
                .FirstOrDefault(x => x.Value == clearName);
            if (knownName == null)
            {
                await _logModule.WriteLog($"Failed to find direct name for {nameExtracts.Key}");
                continue;
            }

            var nameRegex = _translationAnalyser.NameRegex;
            foreach (var nameExtract in nameExtracts.Value)
            {
                var nameMatches = nameRegex.Matches(nameExtract.Value);
                if (nameMatches.Count > 1 && nameMatches.Any(x => x.Value != nameExtracts.Key))
                {
                    await _logModule.WriteLog($"Multiple names, skipping {nameExtract.Path} => {nameExtract.Value}");
                    continue;
                }
                
                nameMatches = nameRegex.Matches(nameExtract.Translated);
                foreach (var nameMatch in nameMatches)
                {
                    var clearNameToReplace = regex.Matches(nameMatch.ToString()).LastOrDefault()?.Value;
                    nameExtract.Translated = nameExtract.Translated.Replace(clearNameToReplace, knownName.Translated);
                }
            }
        }

        await SaveTranslations(translations);
    }

    public async Task ClearJapanese()
    {
        await _logModule.WriteLog($"Clearing japanese in translated values, chatgpt might do that sometimes");
        var nb = 0;
        var translations = await ReadTranslations();
        if (!translations.GameTranslations.TryGetValue(_appSettings.GameConfig.DirectoryPath, out var gameTranslation))
            return;

        foreach (var fileExtract in gameTranslation.JsonFiles)
        {
            foreach (var extract in fileExtract.Value.ToArray())
            {
                if (extract.Value.Translated?.All(x => x.IsJapanese()) ?? true)
                {
                    fileExtract.Value.Remove(extract.Key);
                    nb++;
                }
            }
        }
        
        foreach (var fileExtract in gameTranslation.JsFiles)
        {
            foreach (var extract in fileExtract.Value.ToArray())
            {
                if (extract.Value.Translated?.All(x => x.IsJapanese()) ?? true)
                {
                    fileExtract.Value.Remove(extract.Key);
                    nb++;
                }
            }
        }

        await SaveTranslations(translations);
        await _logModule.WriteLog($"{nb} translations cleared");
    }

    public async Task ClearUnsafeJs()
    {
        await _logModule.WriteLog($"Clearing unsafe Javascript");
        var nb = 0;
        
        var translations = await ReadTranslations();
        if (!translations.GameTranslations.TryGetValue(_appSettings.GameConfig.DirectoryPath, out var gameTranslation))
            return;

        foreach (var fileExtract in gameTranslation.JsonFiles)
        {
            foreach (var extract in fileExtract.Value.ToArray())
            {
                if (await _translationAnalyser.IsJsCode(extract.Value.Value))
                {
                    fileExtract.Value.Remove(extract.Key);
                    nb++;
                }
            }
        }
        
        foreach (var fileExtract in gameTranslation.JsFiles)
        {
            foreach (var extract in fileExtract.Value.ToArray())
            {
                if (await _translationAnalyser.IsJsCode(extract.Value.Value))
                {
                    fileExtract.Value.Remove(extract.Key);
                    nb++;
                }
            }
        }

        await SaveTranslations(translations);
        
        await _logModule.WriteLog($"{nb} translations cleared");
    }

    public async Task ClearUnsafeTranslations()
    {
        await _logModule.WriteLog($"Clearing unsafe translations from setting");
        var nb = 0;
        
        var translations = await ReadTranslations();
        if (!translations.GameTranslations.TryGetValue(_appSettings.GameConfig.DirectoryPath, out var gameTranslation))
            return;

        foreach (var fileExtract in gameTranslation.JsonFiles)
        {
            foreach (var extract in fileExtract.Value.ToArray())
            {
                if (await _translationAnalyser.IsUnsafe(fileExtract.Key, extract.Value))
                {
                    fileExtract.Value.Remove(extract.Key);
                    nb++;
                }
            }
        }
        
        foreach (var fileExtract in gameTranslation.JsFiles)
        {
            foreach (var extract in fileExtract.Value.ToArray())
            {
                if (await _translationAnalyser.IsUnsafe(fileExtract.Key, extract.Value))
                {
                    fileExtract.Value.Remove(extract.Key);
                    nb++;
                }

                if (!extract.Value.Translated.StartsWith('"')
                    || !extract.Value.Translated.EndsWith('"'))
                {
                    extract.Value.Translated = extract.Value.Translated
                        .Trim('"').PadLeft('"').PadRight('"');
                }

                if (translations.GameTranslations[_appSettings.GameConfig.DirectoryPath]
                    .JsonUnsafe.Contains(extract.Value.Value.Trim('"')))
                {
                    fileExtract.Value.Remove(extract.Key);
                    nb++;
                }
            }
        }

        await SaveTranslations(translations);
        
        await _logModule.WriteLog($"{nb} translations cleared");
    }

    //Only used for dev migration of storage
    public async Task UpdateFile()
    {
        var oldT = JsonConvert.DeserializeObject<OldTranslations>(await File.ReadAllTextAsync(StorageFile, Encoding.UTF8));
        var newT = new Translations();

        newT.GameTranslations[_appSettings.GameConfig.DirectoryPath] = new GameTranslation();
        foreach (var fileExtracts in oldT.GameTranslations[_appSettings.GameConfig.DirectoryPath])
        {
            newT.GameTranslations[_appSettings.GameConfig.DirectoryPath].JsonFiles[fileExtracts.Key] =
                new Dictionary<string, JsonExtract>();
            foreach (var extract in fileExtracts.Value)
            {
                newT.GameTranslations[_appSettings.GameConfig.DirectoryPath].JsonFiles[fileExtracts.Key].Add(extract.Key, extract.Value);
            }
        }

        await SaveTranslations(newT);
    }

    public async Task MergeTranslations()
    {
        var oldTranslation = JsonConvert.DeserializeObject<OldTranslations>(
            await File.ReadAllTextAsync($"{StorageFile}.merge", Encoding.UTF8));

        var translations = await ReadTranslations();
        if (!translations.GameTranslations.TryGetValue(_appSettings.GameConfig.DirectoryPath, out var gameTranslation))
            return;

        foreach (var jsonFile in gameTranslation.JsonFiles)
        {
            if (oldTranslation.GameTranslations[_appSettings.GameConfig.DirectoryPath]
                .TryGetValue(jsonFile.Key, out var mergeJsonFile))
            {
                foreach (var jsonExtract in jsonFile.Value)
                {
                    if (mergeJsonFile.ContainsKey(jsonExtract.Key)
                        && jsonExtract.Value.Translated != mergeJsonFile[jsonExtract.Key].Translated)
                    {
                        jsonExtract.Value.Translated = mergeJsonFile[jsonExtract.Key].Translated;
                        await _logModule.WriteLog($"Merged '{jsonExtract.Key}': {jsonExtract.Value.Translated} => {mergeJsonFile[jsonExtract.Key].Translated}");
                    }
                }
            }
        }

        await SaveTranslations(translations);
    }
}