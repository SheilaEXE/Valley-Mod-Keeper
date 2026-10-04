# Valley Modkeeper

Valley Modkeeper helps Stardew Valley players keep a translation library, transfer translations to installed mods, and back up mod settings before updates. It has a Windows desktop version and a native Linux version for Steam Deck.

The application scans a **translation library** separately from the game's **Mods** folder. A translation may remain listed after its mod is disabled or renamed; an unmatched entry is not installed or applied automatically. It recognizes locale files such as `i18n/pt.json`, locale folders such as `i18n/pt/`, selected `assets/Dialogues/<locale>` folders, and supported `content.<locale>.json` text maps. `pt` and `pt-BR` are treated as the same destination language to avoid replacing an existing translation.

Packages with multiple components are grouped in the list. Selecting a package selects only its safely matched components; uncertain destinations must be reviewed individually. Existing translations in the Mods folder are preserved. The application does not modify installed mods when it scans or imports translations into the library.

## Build

Requires the .NET 10 SDK.

```text
dotnet build TradutorMods/TradutorMods.csproj -c Release
dotnet build TradutorModsDeck/TradutorModsDeck.csproj -c Release
```

To publish the Windows app, use `dotnet publish TradutorMods/TradutorMods.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`. For Steam Deck, publish `TradutorModsDeck/TradutorModsDeck.csproj` with `-r linux-x64` and the same options. Copy the entire Linux publish folder to the Deck and run `bash install.sh` from that folder in Desktop Mode. The Linux installer updates the app under the current user's home directory without `sudo`; it does not install or remove game mods.

The Windows title font is Pixelify Sans. Its license is in [`TradutorMods/Assets/PixelifySans-OFL.txt`](TradutorMods/Assets/PixelifySans-OFL.txt).

---

Em português: escolha sua biblioteca de traduções e a pasta `Mods`, selecione o idioma e clique em **Analisar**. Confira os destinos incertos antes de aplicar. No Steam Deck, use a versão Linux nativa da pasta `TradutorModsDeck`, não o executável do Windows.
