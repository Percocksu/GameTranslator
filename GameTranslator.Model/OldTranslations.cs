namespace GameTranslator.Model;

public class OldTranslations
{
    public Dictionary<string, Dictionary<string, Dictionary<string, JsonExtract>>> GameTranslations { get; set; } = new();

}