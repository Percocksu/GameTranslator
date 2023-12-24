using System.Text;
using System.Text.RegularExpressions;
using GameTranslator.Model;
using GameTranslator.Translator;
using GameTranslator.Utils;
using Newtonsoft.Json;

namespace GameTranslator.FileManager;

public class JsExtractor
{
    private readonly ILogModule _logModule;
    private readonly ITranslationAnalyser _translationAnalyser;
    private readonly TranslationSettings _translationSettings;
    
    public JsExtractor(ILogModule logModule, ITranslationAnalyser translationAnalyser, TranslationSettings translationSettings)
    {
        _logModule = logModule;
        _translationAnalyser = translationAnalyser;
        _translationSettings = translationSettings;
    }

    public async Task<List<JsExtract>> ExtractToTranslate(FileDefinition fileDefinition)
    {
        var jsExtracts = new List<JsExtract>();
        var jsContent = await File.ReadAllTextAsync(fileDefinition.PathToTranslate, Encoding.UTF8);

        foreach (var match in FindPosFromMatch(
                     "\"[\\u3000-\\u303f\\u3040-\\u309f\\u30a0-\\u30ff\\uff00-\\uff9f\\u4e00-\\u9faf\\u3400-\\u4dbf]+\""
                     , jsContent))
        {
            var jsExtract = new JsExtract
            {
                Line = match.Line,
                Pos = match.Pos,
                Path = $"{Path.GetFileName(fileDefinition.PathToTranslate)}:{match.Line}:{match.Pos}",
                Value = match.Value
            };
            var status = await ShouldExtract(fileDefinition, jsExtract);
            jsExtract.Unsafe = status == ExtractStatus.Unsafe;
            if(status != ExtractStatus.Ignore)
                jsExtracts.Add(jsExtract);
        }

        return jsExtracts;
    }

    private async Task<ExtractStatus> ShouldExtract(FileDefinition fileDefinition, JsExtract jsExtract)
    {
        if (!string.IsNullOrWhiteSpace(jsExtract.Value) 
            && jsExtract.Value
                .Select((x, xi) => new { x, xi })
                .Any(x => x.x.IsJapanese() && !jsExtract.Value.Substring(0, x.xi).Contains("//")))
        {
            if (await _translationAnalyser.IsJsCode(jsExtract.Value))
                return ExtractStatus.Ignore;
            
            if(await _translationAnalyser.IsUnsafe(fileDefinition.PathToTranslate, jsExtract))
                return ExtractStatus.Unsafe;

            return ExtractStatus.Safe;
        }

        return ExtractStatus.Ignore;
    }
    
    private List<JsMatch> FindPosFromMatch(string pattern, string jsContent)
    {
        var matches = new List<JsMatch>();
        var lines = Regex.Split(jsContent, "\r\n|\r|\n").Where(s => s != string.Empty)
            .ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            foreach (Match m in Regex.Matches(lines[i], pattern))
            {
                matches.Add(new JsMatch
                {
                    Value = m.ToString(),
                    Line = i + 1,
                    Pos =  m.Index
                });
            }
        }

        return matches;
    }
}