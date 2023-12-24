namespace GameTranslator.Utils;

public interface ILogModule
{
    public Task WriteLog(string content);
    public Task WriteLog(string content, string altFile);
}