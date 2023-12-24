using System.Text;
using System.Text.RegularExpressions;

namespace GameTranslator.Utils;

public static class TextUtils
{
    public static string SplicePhrase(string phrase, int maxCharLength)
    {
        var stringBuilder = new StringBuilder();
        var charCount = 0;
        var lines = string.Join(" ", phrase.Split(new [] { Environment.NewLine, @"\r\n" }, StringSplitOptions.None)).Split(new [] {" "}, StringSplitOptions.RemoveEmptyEntries)
            .GroupBy(w => (charCount += w.Length + 1) / maxCharLength)
            .Select(g => string.Join(" ", g));

        stringBuilder.Append(string.Join("\n", lines.ToArray()));

        return stringBuilder.ToString();
    }

    public static string SubstringBetween(this string str, string start, string end, bool includeBoundaries)
    {
        var pFrom = str.IndexOf(start, StringComparison.Ordinal);
        var pTo = str.LastIndexOf(end, StringComparison.Ordinal);

        return includeBoundaries 
            ? str.Substring(pFrom, pTo - pFrom + end.Length)
            : str.Substring(pFrom + start.Length, pTo - pFrom - end.Length);
    }
}