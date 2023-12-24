using System.Text.RegularExpressions;
using GameTranslator.Model;
using GameTranslator.Utils;

namespace GameTranslator.Translator;

public interface ITranslationAnalyser
{
    Task<bool> IsJsCode(string value);
    Task<bool> IsUnsafe(string path, TextExtract extract);
    Task<Dictionary<string, List<TextExtract>>> FindInconsistencies(GameTranslation gameTranslation);
    Regex NameRegex { get; }
    Task<Dictionary<string, TextExtract[]>> FindNames(GameTranslation gameTranslation);
    string ReplaceTranslation(TextExtract textExtract);
    string FormatTranslation(string filePath, TextExtract textExtract);
}

public class BasicTranslationAnalyser : ITranslationAnalyser
{
    private readonly TranslationSettings _translationSettings;
    private readonly ILogModule _logModule;

    public Regex NameRegex =>
        new (@"\\N<[\d\u3000-\u303f\u3040-\u309f\u30a0-\u30ff\uff00-\uff9f\u4e00-\u9faf\u3400-\u4dbfa-zA-Z]*>");
    
    public BasicTranslationAnalyser(TranslationSettings translationSettings, ILogModule logModule)
    {
        _translationSettings = translationSettings;
        _logModule = logModule;
    }

    public Task<bool> IsJsCode(string value)
    {
        var toEval = value.Split("//").First().Trim();
        var isJs = false;

        if (toEval.EndsWith(";"))
            isJs = true;

        if (toEval.Contains("()"))
            isJs = true;

        if (toEval.Contains(" == "))
            isJs = true;

        if (toEval.Contains(" = "))
            isJs = true;

        if (toEval.Contains(" != "))
            isJs = true;

        if (Regex.IsMatch(toEval, "[!]+[a-zA-Z]+"))
            isJs = true;

        if (toEval.ToLower().EndsWith("flg"))
            isJs = true;

        if (toEval.Contains(" && "))
            isJs = true;

        if (toEval.Contains(" || "))
            isJs = true;

        if (toEval.Contains("this."))
            isJs = true;

        var quotes = toEval.Select((x, xi) => new { x, xi })
            .Where(x => x.x == '"')
            .Select(x => x.xi)
            .Select((x, xi) => new { Qi = xi, Qio = x })
            .ToList();
        var isSafe = (_translationSettings.UnsafeJs ?? new string[] {})
                        .Any(x => toEval.Contains(x))
                     && toEval.Select((x, xi) => new { x, xi })
                         .Where(x => x.x.IsJapanese())
                         .All(x =>
                         {
                             var startingQuote = quotes.FirstOrDefault(y => y.Qio < x.xi && quotes.IndexOf(y) % 2 == 0);
                             if (startingQuote == null)
                                 return false;

                             return quotes.FirstOrDefault(y => x.xi < y.Qio)?.Qi == startingQuote.Qi + 1;
                         });

        return Task.FromResult(isJs && !isSafe);
    }

    public Task<bool> IsUnsafe(string path, TextExtract extract)
    {
        if (extract is JsonExtract)
        {
            return Task.FromResult(_translationSettings.UnsafePathRegex
                    .FirstOrDefault(x => Regex.IsMatch(Path.GetFileName(path), x.Key))
                    .Value?.Any(x => Regex.IsMatch(extract.Path, x)) ?? false
            );
        }
        
        return Task.FromResult(_translationSettings.UnsafeJsRegex
                .Any(x => Regex.IsMatch(path, x.Key)
                          && x.Value.Any(y => Regex.IsMatch(extract.Value, y)))
        );
    }

    public async Task<Dictionary<string, List<TextExtract>>> FindInconsistencies(GameTranslation gameTranslation)
    {
        var inconsistencies = new Dictionary<string, List<TextExtract>>();
        var groupedExtracts = gameTranslation.JsonFiles.SelectMany(x => x.Value.Select(y => y.Value))
            .Concat(gameTranslation.JsFiles.SelectMany(x => x.Value.Select(y => y.Value)).Cast<TextExtract>())
            .GroupBy(x => x.Value)
            .ToArray();
        
        await _logModule.WriteLog($"Detecting inconsistencies in translation file");
        foreach (var extractsByValue in groupedExtracts)
        {
            var groupedTranslated = extractsByValue.GroupBy(x => x.TranlatedValue).ToArray();
            if (groupedTranslated.Length != 1)
            {
                // await _logModule.WriteLog($"Inconsistency detected for '{extractsByValue.Key}'");
                // foreach (var extractsByTranslated in groupedTranslated)
                // {
                //     await _logModule.WriteLog($"{extractsByValue.Key} => {extractsByTranslated.Key}");
                // }
                inconsistencies.Add(extractsByValue.Key, extractsByValue.ToList());
            }
        }

        return inconsistencies;
    }

    public async Task<Dictionary<string, TextExtract[]>> FindNames(GameTranslation gameTranslation)
    {
        var regex = NameRegex;
        await _logModule.WriteLog($"Detecting inconsistent names in translation file");
        return gameTranslation.JsonFiles.SelectMany(x => x.Value.Select(y => y.Value))
            .Concat(gameTranslation.JsFiles.SelectMany(x => x.Value.Select(y => y.Value)).Cast<TextExtract>())
            .Where(x => regex.IsMatch(x.Value))
            .SelectMany(x => regex.Matches(x.Value).Select(y => new { Name=y.Value, X=x }))
            .GroupBy(x => x.Name)
            .ToDictionary(x => x.Key, xs => xs.Select(x => x.X).ToArray());
    }

    public string ReplaceTranslation(TextExtract textExtract)
    {
        if (!string.IsNullOrWhiteSpace(textExtract.Translated))
        {
            foreach (var replace in _translationSettings.TranslatedReplace)
            {
                return textExtract.Translated.Replace(replace.Key, replace.Value);
            }
        }

        return textExtract.Translated ?? "";
    }

    public string FormatTranslation(string filePath, TextExtract textExtract)
    {
        var clearedTranslation = textExtract.TranlatedValue
            .Replace(" #OBS:", "")
            .Replace(" #OBS", "")
            .Replace("#OBS", "");

        if (_translationSettings.NoFormatRegex.Any(x => Regex.IsMatch(filePath, x.Key)
                                                        && x.Value.Any(y => Regex.IsMatch(textExtract.Path, y))))
        {
            return clearedTranslation;
        }
        
        return TextUtils.SplicePhrase(
            clearedTranslation
            , _translationSettings.PhraseMaxLength);
    }
}