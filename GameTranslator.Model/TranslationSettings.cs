namespace GameTranslator.Model;

public class TranslationSettings
{
    public bool UpdateJsConst { get; set; }
    
    public int PhraseMaxLength { get; set; }

    public string[]? UnsafeJs { get; set; }

    public string[]? JsFilesRegex { get; set; }

    public List<KeyValue> TranslatedReplace { get; set; } = new();
    public List<KeyValue> OriginalReplace { get; set; } = new();
    
    public Dictionary<string, string[]> UnsafePathRegex { get; set; } = new();

    public Dictionary<string, string[]> UnsafeJsRegex { get; set; } = new();
    
    public Dictionary<string, string[]> NoFormatRegex { get; set; } = new();
}