# Valley Modkeeper

Valley Modkeeper helps Stardew Valley players keep a translation library in their chosen language, transfer translations to installed mods, and back up mod settings before updates. It has a Windows desktop version and a native Linux version for Steam Deck. The interface is currently available in English and Brazilian Portuguese; the translation language can be selected separately.

The application scans a **translation library** separately from the game's **Mods** folder. A translation may remain listed after its mod is disabled or renamed; an unmatched entry is not installed or applied automatically. It recognizes locale files such as `i18n/es.json`, locale folders such as `i18n/fr/`, selected `assets/Dialogues/<locale>` folders, and supported `content.<locale>.json` text maps. For Portuguese translations, `pt` and `pt-BR` are treated as the same destination language to avoid replacing an existing translation.

Packages with multiple components are grouped in the list. Selecting a package selects only its safely matched components; uncertain destinations must be reviewed individually. Existing translations in the Mods folder are preserved. The application does not modify installed mods when it scans or imports translations into the library.

## Build

Requires the .NET 10 SDK.

```text
dotnet build Valley-Mod-Keeper/TradutorMods.csproj -c Release
dotnet build Valley-Mod-Keeper-Deck/TradutorModsDeck.csproj -c Release
```

To publish the Windows app, use `dotnet publish Valley-Mod-Keeper/TradutorMods.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`. For Steam Deck, publish `Valley-Mod-Keeper-Deck/TradutorModsDeck.csproj` with `-r linux-x64` and the same options. Copy the entire Linux publish folder to the Deck and run `bash install.sh` from that folder in Desktop Mode. The Linux installer updates the app under the current user's home directory without `sudo`; it does not install or remove game mods.

The Windows title font is Pixelify Sans. Its license is in [`Valley-Mod-Keeper/Assets/PixelifySans-OFL.txt`](Valley-Mod-Keeper/Assets/PixelifySans-OFL.txt).

---

Em português: o programa aceita traduções no idioma selecionado; a interface, por enquanto, está disponível em inglês e português do Brasil. Escolha sua biblioteca de traduções e a pasta `Mods`, selecione o idioma e clique em **Analisar**. Confira os destinos incertos antes de aplicar. No Steam Deck, use a versão Linux nativa da pasta `Valley-Mod-Keeper-Deck`, não o executável do Windows.
