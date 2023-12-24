namespace GameTranslator.Model;

public class JsExtract : TextExtract
{
    public int Line { get; set; }

    public int Pos { get; set; }

    public override string? TranlatedValue => Translated?.Trim('"');
}