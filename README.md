# Valley Modkeeper

Valley Modkeeper helps Stardew Valley players keep their mod translations and settings when mods are updated or reinstalled. Save translations in your chosen language once, then use the app to find them again instead of downloading the same translations after every update. You can also back up `config.json` files and restore your settings later.

## Quick start

1. Create a folder for your translation library somewhere **outside** the game's `Mods` folder. The app will also store your settings backups inside this library.
2. In Valley Modkeeper, select that folder under **Translation library**, then select your Stardew Valley **Mods folder** and the language of the translations you want to save.
3. Click **Find new mods**. The app scans installed mods and copies translations it finds in your selected language into your library. This does not change the installed mods or download missing translations.
4. To save your settings too, open **Settings backup** and click **Back up all configs** on Windows or **Export all settings** on Linux. This is a separate step from **Find new mods**.
5. After updating or reinstalling a mod, click **Analyze**, review any uncertain matches, and use **Apply selected** to transfer saved translations back. Open **Settings backup** to restore a saved `config.json` for an installed mod.

The app preserves translations already present in your Mods folder. It asks you to review uncertain destinations instead of guessing.

## Compatibility and languages

- The Linux x86-64 version has **only been tested on Steam Deck**; other Linux distributions are not yet verified.
- The Windows x86 (32-bit) package is experimental and has not yet been tested on a 32-bit Windows installation.
- The app interface is currently available in **English and Brazilian Portuguese**. You can choose the translation language independently of the interface language.

## Download and install

Download the ready-to-use ZIP for your operating system from the [latest release](https://github.com/SheilaEXE/Valley-Mod-Keeper/releases/latest). The repository folders contain source code, not the ready-to-run application.

- **Windows x64 (PC):** extract the Windows x64 ZIP and open `ValleyModkeeper.exe`.
- **Windows x86 (32-bit, experimental):** extract the Windows x86 ZIP and open `ValleyModkeeper.exe`. The executable was verified as 32-bit and started on 64-bit Windows, but has not yet been tested on a 32-bit Windows installation.
- **Linux x86-64 (including Steam Deck):** extract the Linux ZIP and run `bash install.sh` from the extracted folder. Open Valley Modkeeper from your applications menu; a desktop shortcut is also created if a desktop folder exists. Installation is per-user and does not require `sudo`.

The installer does not install or remove game mods. Check both folder paths in the app before applying translations.

## How it works

The application scans your **translation library** separately from the game's **Mods** folder. A translation may remain listed after its mod is disabled or renamed; an unmatched entry is not installed or applied automatically. It recognizes locale files such as `i18n/es.json`, locale folders such as `i18n/fr/`, selected `assets/Dialogues/<locale>` folders, and supported `content.<locale>.json` text maps. For Portuguese translations, `pt` and `pt-BR` are treated as the same destination language to avoid replacing an existing translation.

Select **en (default)** to save an `en.json` translation and, when present, keep `i18n/default.json` or `i18n/default/` as an English reference. The default reference is not copied for other selected languages and is never applied over a mod's updated default file.

Packages with multiple components are grouped in the list. Selecting a package selects only its safely matched components; uncertain destinations must be reviewed individually. Existing translations in the Mods folder are preserved. The application does not modify installed mods when it scans or imports translations into the library.

---

