# GameTranslator
A .net core project to translate games.

It generates a json file named '<game name>_TsStorage.json' containing the text translations, this file can be used to update the game files copied in the temp folder and you can then manually copy the file to your game as you see fit.

You can run the translator with chatgpt or manually update an existing TsStorage, then apply the translation to the temporary game files.

## Installation

1. Clone the repo
2. Make sure you have the core 6.0 runtime

## Usage

1. Fill the appsettings.json in GameTranslator.ConsoleApp

### Game config
```json
"GameConfig": {
# game engine, only Rpgm for now
      "Engine": "Rpgm",
# path to your game, note that no game files will ever be touched by this program
      "DirectoryPath": "E:\\Games\\RJ01130999_1.02",
# Rpgm version (only nothing or mv, it changes some paths)
      "Version": "mv"
    }
```
### Settings for translation
```json
"TranslationSettings": {
# Turn off if the game use japanese string in eval, usually it is ok to turn on
    "UpdateJsConst": true,
# Will split the text above this threshold
    "PhraseMaxLength": 100,
# Regex for js file path to process, usually only plugins file
    "JsFilesRegex": [
      "JsScript\\d+Set",
      "plugins\\.js"
    ],
# Black list dictionnary for json files, key is regex path and value is regex path
    "UnsafePathRegex": {
# ex: For all file if the path match parameters[<number>] it will not be translated
        ".*": ".*parameters\[+\d\]"
    },
# Black list dictionnary for js files, key is regex path and value is regex path
    "UnsafeJsRegex": {
      ".*": [
      ]
    },
    # Black list dictionnary for all files, key is regex path and value is regex path
    "NoFormatRegex": {
    # Files will still be translated but not formatted (splitted, character replace etc...)
      ".*": [
      ]
    },
    # Manual translation for texts, useful for names
    "TranslatedReplace": [
    # Example
      { Key: "<some jap>", Value: "Mary" }
    ]
  }
```

Only the game config is necessary, the rest can be left to default

2. Run the command 
```shell
dotnet GameTranslator.ConsoleApp.dll
```

3. Choose and option

- (1) => Run the translation program, a chatgpt key is required in appsettings.json
- (2) => Only copy the game filed to process from the game to temp directory
- (3) => Run the blacklist/replace etc... from the settings to the translation storage
- (4) => Apply the translation storage to the temp game files