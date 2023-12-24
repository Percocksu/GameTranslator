namespace GameTranslator.Translator;

public interface IInputProvider
{
    Task<string> ProvideUserInput();
}