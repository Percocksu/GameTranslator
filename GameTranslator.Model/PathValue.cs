namespace GameTranslator.Model;

public class PathValue
{
    public PathValue()
    {
    }

    public PathValue(string[] path, string value)
    {
        Path = path;
        Value = value;
    }

    public string[] Path { get; set; }

    public string Value { get; set; }
}