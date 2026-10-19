<p align="center">
  <img src="src/OpenNotes/Assets/icon.png" width="112" alt="openNotes" />
</p>

<h1 align="center">openNotes</h1>

<p align="center">
  Letisztult, nyílt forráskódú Markdown jegyzetelő Windowsra – mini ablakkal és globális gyorsbillentyűkkel.<br/>
  <sub>Clean, open source Markdown note taking for Windows. MIT licensed – free for personal and commercial use.</sub>
</p>

---

## Funkciók

- **Markdown szerkesztő** szintaxiskiemeléssel, formázó eszköztárral és billentyűparancsokkal
  (címsorok, félkövér/dőlt/áthúzott, listák, **teendőlista**, idézet, kód, link, táblázat, elválasztó).
- **Élő előnézet** – szerkesztő / osztott / előnézet nézet; a teendők az előnézetben kipipálhatók.
- **Képek** beszúrása fájlból, **vágólapról (Ctrl+V képernyőkép)** vagy behúzással – a jegyzetbe ágyazva.
- **Saját `.openNote` formátum** (ZIP: Markdown + képek + metaadat) – egy fájl, bárkinek elküldhető,
  dupla kattintással importálható. Leírás: [docs/FORMAT.md](docs/FORMAT.md).
- **Markdown export / import** (`.md` + `Név.assets` képmappa).
- **Mini ablak** – kicsi, mindig felül lévő gyors jegyzet ablak.
- **Globális gyorsbillentyűk** (Windows által nem használt alapértékekkel, átállíthatók):
  | Művelet | Alapértelmezés |
  |---|---|
  | Legutóbbi jegyzet megnyitása (újbóli nyomás: elrejtés) | `Ctrl + Alt + Shift + O` |
  | Új jegyzet | `Ctrl + Alt + Shift + N` |
- **Prioritás** (piros = magas, sárga = közepes, lila = alacsony), **határidő** dátummal és opcionális időponttal,
  **emlékeztető** és **Kész** jelölés – a főablak jegyzetfejlécében.
- **Teendők:** az aktív emlékeztetők naponta jeleznek a határidőig és utána is, amíg el nem végzed / ki nem kapcsolod.
  A nap első indulásakor (és a beállított időpontban) **összegző ablak** mutatja a lejárt, mai és közelgő teendőket;
  a főablak oldalsávjában a **Teendők panel** és a címsor harang-ikonja (számlálóval) mindig szem előtt tartja őket.
- Kitűzés, keresés, automatikus mentés, tálcaikon, indítás a Windows-zal.
- **Telepítő** (Inno Setup) – rendszergazdai jog nélkül is telepíthető, `.openNote` fájltársítással.

## openCalendar / openKanban integráció

Az openNotes induláskor beírja a `HKCU\Software\openNotes` kulcsba a saját helyét (`ExePath`) és a jegyzetek mappáját (`NotesDirectory`).
Ebből tudja az [openCalendar](../openCalendar), hol vannak a jegyzetek, és onnan olvassa be a határidőket (csak olvas, nem ír).
Egy adott jegyzet így nyitható meg kívülről:

```
openNotes.exe --open-note <jegyzet-id>
```

Ha az openNotes már fut, a kérést a meglévő ablak kapja meg.

Az [openKanban](../openKanban) ugyanígy olvassa a jegyzetlistát: kártyákhoz jegyzet kapcsolható, ami ezzel a paranccsal nyílik meg.
Az [openLauncher](../openLauncher)-ből az openNotes indítható, telepíthető és eltávolítható. Egyik alkalmazás sem szükséges a másikhoz.

## Billentyűparancsok

| | |
|---|---|
| `Ctrl+N` új jegyzet · `Ctrl+O` importálás · `Ctrl+F` keresés · `Ctrl+S` mentés most | `Ctrl+M` mini mód · `Ctrl+E` nézetváltás · `Ctrl+P` kitűzés · `Ctrl+,` beállítások |
| `Ctrl+B` félkövér · `Ctrl+I` dőlt · `Ctrl+Shift+X` áthúzott · ``Ctrl+` `` kód | `Ctrl+1/2/3` címsor · `Ctrl+0` normál szöveg · `Ctrl+K` link |
| `Ctrl+Shift+L` teendőlista · `Ctrl+Shift+8` felsorolás · `Ctrl+Shift+7` számozott | `Ctrl+Enter` teendő pipálása · `Ctrl+Shift+Q` idézet · `Enter` lista folytatása |

## Fordítás és telepítő készítése

Követelmények: Windows 10/11, [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0),
a telepítőhöz [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

```powershell
# fejlesztés
dotnet run --project src/OpenNotes

# tesztek
dotnet test

# önálló (self-contained) build + telepítő → artifacts\installer\openNotes-Setup-1.0.0-x64.exe
.\publish.cmd    -Version 1.0.0
```

Visual Studio 2022-ben: nyisd meg az `openNotes.sln`-t, indító projekt: **OpenNotes**.

Az előnézet a **Microsoft Edge WebView2 Runtime**-ot használja (Windows 11-en és frissített Windows 10-en
gyárilag telepítve van; ha hiányzik, a telepítő felajánlja a letöltését).

## Adatok helye

| | |
|---|---|
| Jegyzetek | `%APPDATA%\openNotes\Notes\*.openNote` |
| Törölt jegyzetek (visszaállítható) | `%APPDATA%\openNotes\Trash` |
| Beállítások | `%APPDATA%\openNotes\settings.json` |
| Gyorsítótár, napló | `%LOCALAPPDATA%\openNotes` |

**Hordozható mód:** tegyél egy üres `portable.txt` fájlt az `openNotes.exe` mellé – ekkor minden adat a
program melletti `Data` mappába kerül (pl. pendrive-on).

A `%APPDATA%` (roaming) mappa vállalati környezetben a felhasználói profillal együtt vándorol.

## Felépítés

```
src/OpenNotes
├── Core/                 UI-független domain és logika (nincs WPF függőség)
│   ├── Models/           Note, AppSettings, HotkeyGesture, AssetName
│   ├── Abstractions/     interfészek (repository, szerializáló, import/export, renderer, hotkey…)
│   ├── Editing/          MarkdownFormatter + ITextSurface (bármely szerkesztőhöz, tesztelhető)
│   └── Markdown/         TaskListToggler, MarkdownText, MarkdownImageReferences
├── Infrastructure/       a Core interfészek megvalósításai
│   ├── Storage/          .openNote (ZIP) szerializáló, fájl alapú repository, JSON beállítások
│   ├── Exchange/         import/export formátumok (.openNote, Markdown) + NoteImportService
│   ├── Rendering/        Markdig renderer, előnézeti képgyorsítótár
│   ├── Hotkeys/          Win32 RegisterHotKey
│   └── Platform/         autostart (HKCU\Run), egypéldányos futás (mutex + named pipe)
├── UI/
│   ├── Theme/            színek, betűtípus, vezérlőstílusok (Colors.xaml, Controls.xaml)
│   ├── Controls/         MarkdownEditor (AvalonEdit), MarkdownPreview (WebView2), HotkeyBox
│   ├── ViewModels/       MainViewModel, NoteEditorViewModel, SettingsViewModel (MVVM)
│   ├── Views/            fő-, mini-, beállítás- és párbeszédablak
│   └── Services/         ablak- és párbeszédkezelés, tálcaikon, gyorsbillentyű-kötések
├── Web/                  előnézet HTML váz + CSS
└── App.xaml.cs           kompozíciós gyökér (DI regisztrációk)
```

**Elvek:** minden függőség interfészen keresztül, konstruktorban érkezik (DI); egy osztály – egy felelősség;
új formátum = új `INoteImporter` / `INoteExporter` osztály + egy sor regisztráció az `App.xaml.cs`-ben
(meglévő kód módosítása nélkül).

### Gyakori bővítések

- **Szín / téma módosítása:** `src/OpenNotes/UI/Theme/Colors.xaml` és `src/OpenNotes/Web/preview.css` (`:root` változók).
- **Új eszköztár-gomb:** `MarkdownEditor.xaml` (gomb `Tag`-gel) + `CreateActions()` a `MarkdownEditor.xaml.cs`-ben;
  a szövegművelet a `MarkdownFormatter`-be kerül (unit tesztelhető).
- **Új export formátum (pl. HTML/PDF):** új `INoteExporter` implementáció és `NoteExportFormat` érték.

## Licenc

[MIT](LICENSE) – szabadon használható magán- és vállalati környezetben, módosítható és terjeszthető.
Harmadik féltől származó komponensek: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
