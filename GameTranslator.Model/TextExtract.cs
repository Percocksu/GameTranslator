using Newtonsoft.Json;

namespace GameTranslator.Model;

public abstract class TextExtract
{
    public string Value { get; set; }

    public string Path { get; set; }

    public string? Translated { get; set; }

    [JsonIgnore]
    public bool? Unsafe { get; set; }
    
    [JsonIgnore]
    public abstract string? TranlatedValue { get; }
}