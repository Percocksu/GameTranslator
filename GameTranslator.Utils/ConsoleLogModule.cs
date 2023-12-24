using System.Text;

namespace GameTranslator.Utils;

public class ConsoleLogModule : ILogModule
{
    private const string LogFolder = "logs";
    private static SemaphoreSlim  _lock = new SemaphoreSlim(1, 1);

    private string LogFileName(string altenativeName = null)
    {
        return Path.Combine(LogFolder,
            altenativeName == null 
                ? $"{DateTime.Now:yyMMdd}.log"
                : $"{DateTime.Now:yyMMdd}_{altenativeName}.log");
    }
    
    public async Task WriteLog(string content)
    {
        await WriteLog(content, "");
    }

    public async Task WriteLog(string content, string altFile)
    {
        await _lock.WaitAsync();
        try
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}|" +
                              $"{(!string.IsNullOrEmpty(altFile) ? $"{altFile}|" : "")}" +
                              $"{content}");
            if (!Directory.Exists(LogFolder))
                Directory.CreateDirectory(LogFolder);

            await using var stream = new FileStream(LogFileName(altFile), FileMode.Append);
            await using var streamWriter = new StreamWriter(stream, Encoding.UTF8);
            await streamWriter.WriteLineAsync($"{DateTime.Now:yy.MM.dd-HH:mm:ss.fff}|{content}");
        }
        finally
        {
            _lock.Release();
        }
    }
}