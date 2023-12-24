using GameTranslator.Model;

namespace GameTranslator.Translator;

public class DeepLTranslator : ITranslator
{
    public Task TranslateImage(FileDefinition fileDefinition)
    {
        throw new NotImplementedException();
    }

    public Task TranslateTextExtracts(IList<TextExtractFile> textExtracts)
    {
        throw new NotImplementedException();
    }
}