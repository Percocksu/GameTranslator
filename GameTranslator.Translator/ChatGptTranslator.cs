using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using ChatGPT.Net;
using GameTranslator.Model;
using GameTranslator.Utils;
using Newtonsoft.Json;
using SharpToken;

namespace GameTranslator.Translator;

public class ChatGptTranslator : ITranslator
{
    private int _qId = 0;
    private const int TokenLimit = 1000;
    private readonly ILogModule _logModule;
    private readonly AppSettings _appSettings;
    private readonly ITranslationStorage _translationStorage;
    private readonly ChatGpt _chatGpt;
    private readonly GptEncoding _gptEncoding;
    private readonly IInputProvider _inputProvider;

    public ChatGptTranslator(ILogModule logModule, AppSettings appSettings, ITranslationStorage translationStorage, IInputProvider inputProvider)
    {
        _logModule = logModule;
        _appSettings = appSettings;
        _translationStorage = translationStorage;
        _inputProvider = inputProvider;
        _chatGpt = new ChatGpt(_appSettings.ChatGptApiKey);
        _chatGpt.Config.Temperature = 0.5;
        _chatGpt.Config.Model = "gpt-3.5-turbo";
        _chatGpt.Config.MaxTokens = 2000;
        _gptEncoding = GptEncoding.GetEncodingForModel("gpt-3.5-turbo");

    }

    public Task TranslateImage(FileDefinition fileDefinition)
    {
        throw new NotImplementedException();
    }

    private async Task TranslateTypeExtracts(IList<TextExtractFile> textExtracts)
    {
        var storedTranslations = await _translationStorage.ReadTranslations();
        var fileType = textExtracts.First().FileDefinition.Type;
        var extractsToTranslate = fileType == FileType.Json
            ? textExtracts
                .Where(x => !storedTranslations.GameTranslations.ContainsKey(_appSettings.GameConfig.DirectoryPath)
                            || !storedTranslations.GameTranslations[_appSettings.GameConfig.DirectoryPath]
                                .JsonFiles.ContainsKey(x.FileDefinition.PathToTranslate)
                            || !storedTranslations.GameTranslations[_appSettings.GameConfig.DirectoryPath]
                                .JsonFiles[x.FileDefinition.PathToTranslate]
                                .ContainsKey(x.TextExtract.Path))
                .ToArray()
            : textExtracts
                .Where(x => !storedTranslations.GameTranslations.ContainsKey(_appSettings.GameConfig.DirectoryPath)
                            || !storedTranslations.GameTranslations[_appSettings.GameConfig.DirectoryPath]
                                .JsFiles.ContainsKey(x.FileDefinition.PathToTranslate)
                            || !storedTranslations.GameTranslations[_appSettings.GameConfig.DirectoryPath]
                                .JsFiles[x.FileDefinition.PathToTranslate]
                                .ContainsKey(x.TextExtract.Path))
                .ToArray();


        if (!extractsToTranslate.Any())
            return;

        var extractTranslated = 0;
        extractsToTranslate = extractsToTranslate.OrderBy(x => x.FileDefinition.PathToTranslate).ToArray();
        await _logModule.WriteLog(
            $"Translating {extractsToTranslate.Length} texts from {extractsToTranslate.GroupBy(x => x.FileDefinition).Count()} {fileType} files",
            "chatgpt");

        var translationTasks = new List<Task>();
        var splitExtracts = extractsToTranslate.Length < 20
            ? extractsToTranslate.Length
            : (int)Math.Ceiling((double)extractsToTranslate.Length / _appSettings.TranslationThreads);

        var taskGroupedExtracts = new List<IEnumerable<IGrouping<FileDefinition, TextExtractFile>>>(); 
        for (var i = 0; i < _appSettings.TranslationThreads; i++)
        {
            taskGroupedExtracts.Add(extractsToTranslate.Skip(i * splitExtracts).Take(splitExtracts)
                .GroupBy(x => x.FileDefinition));
        }
        foreach (var taskGroupedExtract in taskGroupedExtracts)
        {
            translationTasks.Add(Task.Run(async () =>
            {
                var guid = Guid.NewGuid().ToString();
                foreach (var splitExtractsToTranslate in taskGroupedExtract)
                {
                    await _logModule.WriteLog(
                        $"Starting translation thread (id: {guid}) with {splitExtractsToTranslate.Count()} extracts and file {splitExtractsToTranslate.Key.PathToTranslate}",
                        "chatgpt");
                    var fileDefinition = splitExtractsToTranslate.Key;
                    var fileSplitExtracts = splitExtractsToTranslate
                        .Select(x => x.TextExtract)
                        .ToArray();
                    var answers = new Dictionary<string, string>();
                    var questions = new List<List<PathValue>>
                    {
                        new List<PathValue>()
                    };
                    var tokenCount = 0;
                    var groupedExtracts = fileSplitExtracts
                        .GroupBy(x => x.Value)
                        .ToArray();
                    foreach (var extracts in groupedExtracts)
                    {
                        //For Js file we take json translated in priority to match eval calls
                        if (splitExtractsToTranslate.Key.Type == FileType.Js)
                        {
                            var existingJson = storedTranslations
                                .GameTranslations[_appSettings.GameConfig.DirectoryPath]
                                .JsonFiles.SelectMany(x => x.Value.Select(y => y.Value))
                                .FirstOrDefault(x => x.Value == extracts.First().Value.Trim('"'));
                            if (existingJson != null)
                            {
                                foreach (var textExtract in extracts.ToArray())
                                {
                                    textExtract.Translated = $"\"existingJson.Translated\"";
                                }

                                continue;
                            }
                        }

                        tokenCount += _gptEncoding
                            .Encode(
                                Environment.NewLine + $"'{extracts.First().Path}' => '{extracts.First().Value}'")
                            .Count;
                        if (tokenCount < TokenLimit)
                        {
                            questions.Last().Add(new PathValue(extracts.Select(x => x.Path).ToArray(),
                                extracts.First().Value));
                        }
                        else
                        {
                            questions.Add(new List<PathValue>
                            {
                                new(extracts.Select(x => x.Path).ToArray(), extracts.First().Value)
                            });
                            tokenCount = 0;
                        }
                    }


                    foreach (var question in questions)
                    {
                        answers.Clear();
                        var varToTranslate = question
                            .DistinctBy(x => x.Path)
                            .ToDictionary(x => x.Path.First(), x => x.Value);

                        if (varToTranslate.Any())
                        {
                            var convId = Guid.NewGuid().ToString();
                            var conversation = _chatGpt.GetConversation(convId);
                            var response = string.Empty;
                            await _logModule.WriteLog(
                                $"{fileDefinition.PathToTranslate} {questions.IndexOf(question) + 1}/{questions.Count}",
                                "chatgpt");
                            var stringBuilder = new StringBuilder();
                            stringBuilder.AppendLine(
                                $"Translate the values in json from" +
                                $" the japanese rpgm game file {Path.GetFileName(fileDefinition.PathToTranslate)} to english:");
                            stringBuilder.AppendLine(JsonConvert.SerializeObject(varToTranslate,
                                Formatting.Indented));

                            try
                            {
                                response = await AskGpt(stringBuilder.ToString(), convId);

                                try
                                {
                                    answers = await DeserializeResponse(response);
                                }
                                catch (Exception e)
                                {
                                    // await _logModule.WriteLog(
                                    //     $"Error: Failed to deserialize chatgpt response, swapping to manual mode",
                                    //     "chatgpt");
                                    // answers = await ManualAsk(convId);
                                    await _logModule.WriteLog(
                                        $"Error: Failed to deserialize chatgpt response,skipping",
                                        "chatgpt");
                                    continue;
                                }

                                foreach (var answer in answers)
                                {
                                    var textExtract = fileSplitExtracts.FirstOrDefault(x => x.Path == answer.Key);
                                    if (textExtract == null)
                                        continue;

                                    if (answer.Value == textExtract.Value)
                                    {
                                        await _logModule.WriteLog(
                                            $"Detected non translated value for {answer.Key}, swapping to manual mode",
                                            "chatgpt");
                                        answers = await ManualAsk(convId);
                                    }
                                }
                            }
                            catch (TaskCanceledException e)
                            {
                                if (e.GetBaseException() is SocketException socketException)
                                {
                                    if (socketException.SocketErrorCode == SocketError.TimedOut ||
                                        socketException.SocketErrorCode == SocketError.OperationAborted)
                                    {
                                        await _logModule.WriteLog($"ChatGpt timeout, moving on after a bit..",
                                            "chatgpt");
                                        await Task.Delay(20000);
                                    }
                                }
                            }
                            catch (HttpRequestException e)
                            {
                                await _logModule.WriteLog($"ChatGpt {e.StatusCode}, moving on after a bit..",
                                    "chatgpt");
                                await Task.Delay(30000);
                            }
                            catch (Exception e)
                            {
                                await _logModule.WriteLog(
                                    $"ChatGpt request failed, moving on after a bit... ({e.GetBaseException().Message})",
                                    "chatgpt");
                                await Task.Delay(30000);
                            }
                        }

                        foreach (var answer in answers)
                        {
                            var answerExtracts =
                                groupedExtracts.FirstOrDefault(xs => xs.Any(y => y.Path == answer.Key));
                            if (answerExtracts == null)
                            {
                                await _logModule.WriteLog(
                                    $"Failed to find extract path {answer.Key} from chatgpt answer",
                                    "chatgpt");
                                continue;
                            }

                            foreach (var answerExtract in answerExtracts)
                            {
                                extractTranslated++;
                                answerExtract.Translated = answer.Value;
                            }
                        }
                        
                        await _translationStorage.WriteTranslations(fileDefinition,
                            fileSplitExtracts.Where(x => !string.IsNullOrWhiteSpace(x.Translated)).ToArray());
                        await _logModule.WriteLog($"Progress: {Math.Round((decimal)extractTranslated / extractsToTranslate.Length, 2)}%", "chatgpt");
                    }
                }
            }));
        }

        await Task.WhenAll(translationTasks);
    }

    public async Task TranslateTextExtracts(IList<TextExtractFile> textExtracts)
    {
        var grouped = textExtracts
            .GroupBy(x => x.FileDefinition.Type)
            .ToArray();
        foreach (var group in grouped)
        {
            await TranslateTypeExtracts(group.ToArray());
        }
    }

    private async Task<Dictionary<string, string>> ManualAsk(string convId)
    {
        var response = string.Empty;
        var userInput = string.Empty;
        await _logModule.WriteLog($"Tell chat gpt manually to make the answer usable, enter exit to stop and process the last response. If response is not valid process will be skipped");

        while ((userInput = await _inputProvider.ProvideUserInput()) != "exit")
        {
            response = await AskGpt(userInput, convId);
        }

        try
        {
            return await DeserializeResponse(response.SubstringBetween("{", "}", true));
        }
        catch
        {
            await _logModule.WriteLog($"Manual response was not valid, skipping",
                "chatgpt");
            return new Dictionary<string, string>();
        }
    }

    private async Task<Dictionary<string, string>> DeserializeResponse(string response)
    {
        var sb = new StringBuilder();
        foreach (var line in response.Split("\n"))
        {
            if (line.StartsWith("\"") && (line.EndsWith("\"") || line.EndsWith("\",")))
            {
                var splitReg = new Regex("\":\\s*\"");
                var lineSplit = splitReg.Split(line);
                if (lineSplit.Length != 2)
                {
                    sb.AppendLine(line);
                    continue;
                }

                var value = lineSplit[1].Substring(0, lineSplit[1].LastIndexOf("\""));
                if (value.Contains("\""))
                {
                    await _logModule.WriteLog($"Detected unescaped quote in chatgpt answer: {value}", "chatgpt");
                    var fixedVal = value.Replace("\"", "\\\"");
                    await _logModule.WriteLog($"Modified to: {fixedVal}", "chatgpt");
                    sb.AppendLine(line.Replace(value, fixedVal));
                }
                else
                {
                    sb.AppendLine(line);
                }
            }
            else
            {
                sb.AppendLine(line);
            }
        }
        
        return JsonConvert.DeserializeObject<Dictionary<string, string>>(sb.ToString()) 
               ?? new Dictionary<string, string>();
    }
    
    private async Task<string> AskGpt(string message, string convId)
    {
        var id = ++_qId;
        await _logModule.WriteLog($"Asking (id: {id}) (token: {_gptEncoding.Encode(message).Count}): {message}", "chatgpt");
        var response = await _chatGpt.Ask(message, convId);
        await _logModule.WriteLog($"Answer (id: {id}) (token: {_gptEncoding.Encode(response).Count}): {response}", "chatgpt");
        return response;
    }
}