using GameTranslator.Translator;

namespace GameTranslator.ConsoleApp;

public class ConsoleInputProvider : IInputProvider
{
    public Task<string> ProvideUserInput()
    {
        return Task.FromResult(Console.ReadLine() ?? "");
    }
}