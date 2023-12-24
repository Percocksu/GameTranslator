namespace GameTranslator.Model;

public class Translations
{
    public Dictionary<string, GameTranslation> GameTranslations { get; set; } = new();
}

public class GameTranslation
{
    public Dictionary<string, Dictionary<string, JsonExtract>> JsonFiles { get; set; }= new();

    public List<string> JsonUnsafe { get; set; } = new();
    
    public Dictionary<string, Dictionary<string, JsExtract>> JsFiles { get; set; }= new();
}