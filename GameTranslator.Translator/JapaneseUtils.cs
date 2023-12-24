namespace GameTranslator.Translator;

public static class JapaneseUtils
{
    public static bool IsJapanese(this char c)
    {
        return IsHiragana(c) || IsKatakana(c) || IsKanji(c);
    }

    public static bool IsHiragana(char c)
    {
        return 0x3040 <= c && c <= 0x309F;
    }
    
    public static bool IsKatakana(char c)
    {
        return 0x30A0 <= c && c <= 0x30FF;
    }
    
    public static bool IsKanji(char c)
    {
        return 0x4E00 <= c && c <= 0x9FBF;
    }
}