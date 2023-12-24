namespace GameTranslator.Model;

public class AppSettings
{
    public int TranslationThreads { get; set; }
    
    public string TempDir { get; set; }

    public GameConfig GameConfig { get; set; }

    public string ChatGptApiKey { get; set; }

    public string DeepLApiKey { get; set; }

    public bool KeepTempImages { get; set; }

    public bool ExtractImages { get; set; }
}