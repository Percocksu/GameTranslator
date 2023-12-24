using System.Text;
using GameTranslator.Model;
using LZStringCSharp;

namespace GameTranslator.FileManager;

public class LzStringConvert
{
    public async Task<string> ExtractLzString(string filePath)
    {
        return LZString.DecompressFromBase64(await File.ReadAllTextAsync(filePath, Encoding.UTF8));
    }
    
    public Task SaveLzString(string filePath, string content)
    {
        LZString.CompressToBase64(content);
        return Task.CompletedTask;
    }
}