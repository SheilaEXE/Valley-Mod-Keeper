# Valley Modkeeper

Valley Modkeeper helps Stardew Valley players keep a translation library in their chosen language, transfer translations to installed mods, and back up mod settings before updates. It has Windows and Linux versions.

The application scans a **translation library** separately from the game's **Mods** folder. A translation may remain listed after its mod is disabled or renamed; an unmatched entry is not installed or applied automatically. It recognizes locale files such as `i18n/es.json`, locale folders such as `i18n/fr/`, selected `assets/Dialogues/<locale>` folders, and supported `content.<locale>.json` text maps. For Portuguese translations, `pt` and `pt-BR` are treated as the same destination language to avoid replacing an existing translation.

Packages with multiple components are grouped in the list. Selecting a package selects only its safely matched components; uncertain destinations must be reviewed individually. Existing translations in the Mods folder are preserved. The application does not modify installed mods when it scans or imports translations into the library.

## Compatibility and languages

- The Linux x86-64 version has **only been tested on Steam Deck**; other Linux distributions are not yet verified.
- The Windows x86 (32-bit) package is experimental and has not yet been tested on a 32-bit Windows installation.
- The app interface is currently available in **English and Brazilian Portuguese**. You can choose the translation language independently of the interface language.

## Download and install

Download the ready-to-use ZIP for your operating system from the [latest release](https://github.com/SheilaEXE/Valley-Mod-Keeper/releases/latest). The repository folders contain source code, not the ready-to-run application.

- **Windows x64 (PC):** extract the Windows x64 ZIP and open `ValleyModkeeper.exe`.
- **Windows x86 (32-bit, experimental):** extract the Windows x86 ZIP and open `ValleyModkeeper.exe`. The executable was verified as 32-bit and started on 64-bit Windows, but has not yet been tested on a 32-bit Windows installation.
- **Linux x86-64 (including Steam Deck):** extract the Linux ZIP and run `bash install.sh` from the extracted folder. Open Valley Modkeeper from your applications menu; a desktop shortcut is also created if a desktop folder exists. Installation is per-user and does not require `sudo`.

Choose your translation library and Stardew Valley `Mods` folder in the app, and verify both paths before applying translations. The installer does not install or remove game mods.

The Windows title font is Pixelify Sans. Its license is in [`Valley-Mod-Keeper/Assets/PixelifySans-OFL.txt`](Valley-Mod-Keeper/Assets/PixelifySans-OFL.txt).

---

Em português: o programa aceita traduções no idioma selecionado; a interface, por enquanto, está disponível em inglês e português do Brasil. Baixe o ZIP adequado ao seu sistema na página de lançamentos. O pacote Windows x86 (32 bits) é experimental e ainda não foi testado num Windows de 32 bits. A versão Linux x86-64 foi testada apenas no Steam Deck até agora. Escolha sua biblioteca de traduções e a pasta `Mods`, selecione o idioma e clique em **Analisar**. Confira os destinos incertos antes de aplicar.
