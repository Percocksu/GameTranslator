// See https://aka.ms/new-console-template for more information

using System.Text;
using GameTranslator.ConsoleApp;
using GameTranslator.FileManager;
using GameTranslator.Model;
using GameTranslator.Rpgm;
using GameTranslator.Service;
using GameTranslator.Translator;
using GameTranslator.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var environmentName = "prod";
#if DEBUG
environmentName = "dev";
#endif

var configuration =  new ConfigurationBuilder()
    .AddJsonFile($"appsettings.json", false, true)
    .AddJsonFile($"appsettings.{environmentName}.json", true, true)
    .Build();

var builder = new ServiceCollection()
    .AddScoped<IFileManager, RpgmFileManager>()
    .AddScoped<JsonExtractor>()
    .AddScoped<JsExtractor>()
    .AddScoped<LzStringConvert>()
    .AddScoped<RpgmvpConvert>()
    .AddScoped<ITranslator, ChatGptTranslator>()
    .AddScoped<ITranslationAnalyser, BasicTranslationAnalyser>()
    .AddScoped<ILogModule, ConsoleLogModule>()
    .AddScoped<IInputProvider, ConsoleInputProvider>()
    .AddScoped<ITranslationStorage, JsonTranslationStorage>()
    .AddScoped<TranslationWriter>()
    .AddScoped<AppSettings>(x =>
    {
        var sett = new AppSettings();
        configuration.Bind("AppSettings", sett);
        return sett;
    })
    .AddScoped<TranslationSettings>(x =>
    {
        var sett = new TranslationSettings();
        configuration.Bind("TranslationSettings", sett);
        return sett;
    })
    .AddScoped<GameTranslatorService>()
    .BuildServiceProvider();

Console.OutputEncoding = Encoding.UTF8;
using var scope = builder.CreateScope();

var logModule = scope.ServiceProvider.GetService<ILogModule>();
var gameTranslator = scope.ServiceProvider.GetService<GameTranslatorService>();
var translationStorage = scope.ServiceProvider.GetService<ITranslationStorage>();

//await translationStorage.UpdateFile();
await translationStorage.ClearJapanese();
await translationStorage.ClearUnsafeJs();
await translationStorage.ClearUnsafeTranslations();
await translationStorage.ApplyReplace();
await gameTranslator.TranslateGame();
await translationStorage.FixInconsistencies();
await translationStorage.FixInconsistenciesInNames();
await logModule.WriteLog("Done");
Console.ReadLine();
