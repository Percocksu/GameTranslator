using System.Text;
using GameTranslator.Model;
using GameTranslator.Translator;
using GameTranslator.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameTranslator.FileManager;

public class JsonExtractor
{
    private readonly ILogModule _logModule;
    private readonly ITranslationAnalyser _translationAnalyser;

    public JsonExtractor(ILogModule logModule, ITranslationAnalyser translationAnalyser)
    {
        _logModule = logModule;
        _translationAnalyser = translationAnalyser;
    }

    public async Task<List<JsonExtract>> ExtractToTranslate(FileDefinition fileDefinition)
    {
        var jsonExtracts = new List<JsonExtract>();
        dynamic dynObj =
            JsonConvert.DeserializeObject(await File.ReadAllTextAsync(fileDefinition.StoredPath, Encoding.UTF8));
        await _logModule.WriteLog(fileDefinition.StoredPath);
        await ExtractString(fileDefinition, dynObj, jsonExtracts);

        return jsonExtracts;
    }

    private async Task<ExtractStatus> ShouldExtract(FileDefinition fileDefinition, JsonExtract jsonExtract)
    {
        if (!string.IsNullOrWhiteSpace(jsonExtract.Value) 
            && jsonExtract.Value
                .Select((x, xi) => new { x, xi })
                .Any(x => x.x.IsJapanese() && !jsonExtract.Value.Substring(0, x.xi).Contains("//")))
        {
            if (await _translationAnalyser.IsJsCode(jsonExtract.Value))
                return ExtractStatus.Ignore;
            
            if(await _translationAnalyser.IsUnsafe(fileDefinition.PathToTranslate, jsonExtract))
                return ExtractStatus.Unsafe;

            return ExtractStatus.Safe;
        }

        return ExtractStatus.Ignore;
    }
    
    private async Task ExtractString(FileDefinition fileDefinition, JToken jToken, ICollection<JsonExtract> jsonExtracts)
    {
        if (jToken is JValue { Type: JTokenType.String } jValue)
        {
            var jExtract = new JsonExtract
            {
                Path = jValue.Path,
                Value = jValue.Value.ToString()
            };
            var status = await ShouldExtract(fileDefinition, jExtract);
            jExtract.Unsafe = status == ExtractStatus.Unsafe;
            if(status != ExtractStatus.Ignore)
                jsonExtracts.Add(jExtract);
        }
        
        // _logModule.WriteLog(typeof(JToken).FullName).Wait();
        if (jToken is JProperty jProp)
        {
            if (jProp.Value.Type == JTokenType.String)
            {
                var jExtract = new JsonExtract
                {
                    Path = jProp.Path,
                    Value = jProp.Value.ToString()
                };
                var status = await ShouldExtract(fileDefinition, jExtract);
                jExtract.Unsafe = status == ExtractStatus.Unsafe;
                if(status != ExtractStatus.Ignore)
                    jsonExtracts.Add(jExtract);
            }

            if (jProp.Value.Type == JTokenType.Array)
            {
                foreach (var arrayProp in jProp.Value)
                {
                    await ExtractString(fileDefinition, arrayProp, jsonExtracts);
                }
            }
            
            if (jProp.Value.Type == JTokenType.Object)
            {
                foreach (var propChild in jProp.Value.Children())
                {
                    await ExtractString(fileDefinition, propChild, jsonExtracts);
                }
            }
        }

        if (jToken is JObject jObj)
        {
            foreach (var child in jObj.Children())
            {
                await ExtractString(fileDefinition, child, jsonExtracts);
            }
        }

        if (jToken is JArray jArray)
        {
            foreach (var arrayProp in jArray)
            {
                await ExtractString(fileDefinition, arrayProp, jsonExtracts);
            }
        }
    }
}