namespace GameTranslator.Model;

public class GameConfig
{
    public GameEngine Engine { get; set; }

    public string Version { get; set; }
    
    public string DirectoryPath { get; set; }
}

public enum GameEngine {
    Rpgm
}