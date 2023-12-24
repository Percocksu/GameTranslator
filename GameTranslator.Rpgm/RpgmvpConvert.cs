using System.Text;
using GameTranslator.Model;
using GameTranslator.Utils;
using Newtonsoft.Json;

namespace GameTranslator.Rpgm;

public class RpgmvpConvert
{
    private const int HEADER_LENGTH = 16;
    private readonly AppSettings _appSettings;
    private readonly ILogModule _logModule;

    public RpgmvpConvert(AppSettings appSettings, ILogModule logModule)
    {
        _appSettings = appSettings;
        _logModule = logModule;
    }

    public async Task Extract(string filePath, string destinationPath)
    {
        var key = HexToBytes(await ReadEncryptionKey());

        await using var inputStream = File.OpenRead(filePath);
        await using var outputStream = new FileStream(destinationPath, FileMode.CreateNew);
        await outputStream.WriteAsync(DecryptHeader(inputStream, key), 0, HEADER_LENGTH);
        await inputStream.CopyToAsync(outputStream);
    }
    
    public async Task SaveImage(string filePath, string destinationPath)
    {

    }

    private const string KeyAttr = "encryptionKey";
    private async Task<string> ReadEncryptionKey()
    {
        var encryptionKey = string.Empty;
        var systemJson = Directory
            .GetFiles(_appSettings.GameConfig.DirectoryPath, "System.json", SearchOption.AllDirectories)
            .MaxBy(x => x.Contains(@"www\data\"));
        if (systemJson == null)
        {
            await _logModule.WriteLog("ERROR: Failed to locate System.json file");
            throw new FileNotFoundException("System.json file not found");
        }

        dynamic parsedObject = JsonConvert.DeserializeObject(await File.ReadAllTextAsync(systemJson, Encoding.UTF8));
        encryptionKey = parsedObject[KeyAttr];

        if (string.IsNullOrWhiteSpace(encryptionKey))
        {
            await _logModule.WriteLog("ERROR: Failed to read encryption key in System.json file");
            throw new Exception("Encryption key not found");
        }

        return encryptionKey;
    }
    
    private byte[] HexToBytes(string hex) {
        if (hex == null) throw new ArgumentNullException(nameof(hex));

        hex = hex.Replace(" ", "");
        var length = hex.Length;
        var bytes = new byte[length / 2];

        for (int i = 0; i < length; i += 2) {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }

        return bytes;
    }
    
    private byte[] DecryptHeader(Stream inputStream, byte[] key) {
        Skip(inputStream, HEADER_LENGTH);
        var header = new byte[HEADER_LENGTH];
        inputStream.Read(header, 0, HEADER_LENGTH);

        for (var i = 0; i < HEADER_LENGTH; i++) {
            header[i] = (byte)(header[i] ^ key[i]);
        }

        return header;
    }
    
    private bool Skip(Stream stream, long bytes = 1, long lowerLimit = 0, long upperLimit = -1) {
        var newPosition = stream.Position + bytes;
        if (upperLimit < 0) upperLimit = stream.Length - 1;
        if (newPosition < lowerLimit || newPosition > upperLimit) return false;

        stream.Position = newPosition;
        return true;
    }
}