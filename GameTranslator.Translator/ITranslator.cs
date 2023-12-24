using GameTranslator.Model;

namespace GameTranslator.Translator;

public interface ITranslator
{
    Task TranslateImage(FileDefinition fileDefinition);
    Task TranslateTextExtracts(IList<TextExtractFile> textExtracts);
}