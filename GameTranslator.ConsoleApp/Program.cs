// See https://aka.ms/new-console-template for more information

using System.Text;
using GameTranslator.ConsoleApp;
using GameTranslator.FileManager;
using GameTranslator.Model;
using GameTranslator.Rpgm;
using GameTranslator.Service;
using GameTranslator.Translator;
using GameTranslator.Utils;
using Lakerfield.ConsoleMenu;
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

Console.WriteLine("Choose an option: 1,2,3,4,... or Q to quit");
await ConsoleMenu.RunMainMenuAndWaitForCompletion(
    "Game translator", 
    menu =>
    {
        menu.Add(ConsoleKey.D1, "(1) Translate game from settings", async () =>
        {
            using var scope = builder.CreateScope();
            var logModule = scope.ServiceProvider.GetService<ILogModule>();
            var gameTranslator = scope.ServiceProvider.GetService<GameTranslatorService>();
            var translationStorage = scope.ServiceProvider.GetService<ITranslationStorage>();
            await gameTranslator.TranslateGame();
            await translationStorage.ClearJapanese();
            await translationStorage.ClearUnsafeJs();
            await translationStorage.ClearUnsafeTranslations();
            await translationStorage.ApplyReplace();
            await translationStorage.FixInconsistencies();
            await translationStorage.FixInconsistenciesInNames();
            await gameTranslator.WriteTranslationsToTemp();
            await logModule.WriteLog("Done");
        });
        menu.Add(ConsoleKey.D2, "(2) Copy game files to temp", async () =>
        {
            using var scope = builder.CreateScope();
            var logModule = scope.ServiceProvider.GetService<ILogModule>();
            var gameTranslator = scope.ServiceProvider.GetService<GameTranslatorService>();
            await gameTranslator.CopyFilesToTemp();
            await logModule.WriteLog("Done");
        });
        menu.Add(ConsoleKey.D3, "(3) Clean/Update translation storage", async () =>
        {
            using var scope = builder.CreateScope();
            var logModule = scope.ServiceProvider.GetService<ILogModule>();
            var translationStorage = scope.ServiceProvider.GetService<ITranslationStorage>();
            await translationStorage.ClearJapanese();
            await translationStorage.ClearUnsafeJs();
            await translationStorage.ClearUnsafeTranslations();
            await translationStorage.ApplyReplace();
            await translationStorage.FixInconsistencies();
            await translationStorage.FixInconsistenciesInNames();
            await logModule.WriteLog("Done");
        });
        menu.Add(ConsoleKey.D4, "(4) Apply storage to game files", async () =>
        {
            using var scope = builder.CreateScope();
            var logModule = scope.ServiceProvider.GetService<ILogModule>();
            var gameTranslator = scope.ServiceProvider.GetService<GameTranslatorService>();
            await gameTranslator.WriteTranslationsToTemp();
            await logModule.WriteLog("Done");
        });
    }, 
    onlyExitWhenPressingQuit: true);
