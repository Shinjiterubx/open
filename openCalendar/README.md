<p align="center">
  <img src="src/OpenCalendar/Assets/icon.png" width="112" alt="openCalendar" />
</p>

<h1 align="center">openCalendar</h1>

<p align="center">
  Letisztult, nyílt forráskódú naptár Windowsra – ismétlődő eseményekkel, emlékeztetőkkel és openNotes és openKanban szinkronnal.<br/>
  <sub>Clean, open source calendar for Windows with recurring events, reminders and openNotes sync. MIT licensed.</sub>
</p>

---

## Funkciók

- **Nézetek:** Nap · Hét · Hónap · Napló (a következő 60 nap listája). A sávban mini naptár, naptárlista és a közelgő események látszanak.
- **Események** órára és percre megadott időtartammal, egész napos és többnapos eseményekkel, helyszínnel és leírással.
  - Új eseményt duplakattintással lehet létrehozni a rácson (a hét/nap nézetben az adott időpontra), vagy a `Ctrl+N` billentyűvel.
  - Az egymást átfedő események egymás mellé kerülnek.
- **Ismétlődés:** naponta, hetente (a hét kiválasztott napjain), havonta vagy évente, minden N-edik alkalommal.
  - A sorozat vége lehet: soha, egy adott dátum, vagy N alkalom után.
  - Egy ismétlődő eseménynek csak egy alkalma is törölhető.
- **Emlékeztetők:** egy eseményhez több is megadható (kezdéskor, 5/10/15/30 perccel, 1–2 órával, 1–2 nappal vagy 1 héttel előtte).
  - Ismétlődő eseménynél minden alkalom előtt szól, például naponta vagy hetente.
  - Az értesítés a képernyő jobb alsó sarkában jelenik meg, gombjai: *Megnyitás* és *Szundi*.
- **Több naptár** saját színnel és be-/kikapcsolható láthatósággal.
- **`.opencalendar` export/import:** egy naptár vagy az összes exportálható és importálható. A fájl behúzható az ablakba, vagy dupla kattintással megnyitható. A formátum leírása: [docs/FORMAT.md](docs/FORMAT.md).
- **openNotes szinkron:**
  - A jegyzetekhez beállított határidők automatikusan megjelennek a naptárban, élőben frissülve.
  - Kattintásra a jegyzet megnyílik az openNotes-ban.
  - Eseményhez openNotes jegyzet kapcsolható („Megnyitás” gombbal).
  - Az időponthoz kötött jegyzet-határidőkre is jön emlékeztető.
- **openKanban szinkron** (opcionális, a Beállításokban kikapcsolható):
  - A határidős kártyák külön „openKanban” naptárként jelennek meg (`%APPDATA%\openKanban\deadlines.json`, élőben frissülve).
  - Kattintásra a kártya megnyílik az openKanban-ban (`openKanban.exe --open-card <id>`).
  - Az openKanban a naptár napjára ugorhat: `openCalendar.exe --goto yyyy-MM-dd`.
- Az **openLauncher**-ből indítható, telepíthető és eltávolítható. Egyik alkalmazás sem függ a másiktól.
- Tálcaikon, indítás a Windows-zal (hogy az emlékeztetők mindig szóljanak), egypéldányos futás, **telepítő**.

## Billentyűparancsok

| | |
|---|---|
| `Ctrl+N` új esemény · `Ctrl+T` ma | `Ctrl+←/→`, `PgUp/PgDn` előző / következő |
| `Ctrl+1/2/3/4` nap / hét / hónap / napló | `Ctrl+I` import · `Ctrl+,` beállítások |
| Eseményszerkesztőben: `Ctrl+S` / `Ctrl+Enter` mentés, `Esc` mégse | |

## Fordítás és telepítő

Követelmények: Windows 10/11, [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), a telepítőhöz [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
dotnet run --project src/OpenCalendar          # fejlesztés
dotnet test                                    # tesztek
.\publish.cmd    -Version 1.0.0             # → artifacts\installer\openCalendar-Setup-1.0.0-x64.exe
```

## Adatok

| | |
|---|---|
| Naptárak | `%APPDATA%\openCalendar\Calendars\*.opencalendar` |
| Törölt naptárak | `%APPDATA%\openCalendar\Trash` |
| Beállítások | `%APPDATA%\openCalendar\settings.json` |
| Napló | `%LOCALAPPDATA%\openCalendar\Logs` |

**Hordozható mód:** ha egy üres `portable.txt` kerül az `openCalendar.exe` mellé, minden adat a program melletti `Data` mappába kerül.

## Felépítés

```
src/OpenCalendar
├── Core/              UI-független domain (tesztelhető)
│   ├── Models/        CalendarDocument, CalendarEvent, RecurrenceRule, NoteReference, AppSettings
│   ├── Recurrence/    OccurrenceExpander – ismétlődések kibontása időtartományra
│   ├── Reminders/     ReminderPlanner – mely emlékeztetők esedékesek
│   ├── Agenda/        CalendarEntry, AgendaBuilder (események + jegyzet-határidők), DayLayout (átfedések)
│   ├── Time/          WeekMath (hétfős hét), TimeInput
│   └── Abstractions/  interfészek (repository, szerializáló, jegyzetforrás, indító…)
├── Infrastructure/
│   ├── Storage/       .opencalendar (ZIP+JSON) szerializáló, fájl repository, JSON beállítások
│   ├── Integration/   openNotes: könyvtár olvasó + FileSystemWatcher, megkereső, jegyzet-megnyitó
│   └── Platform/      autostart, egypéldányos futás
├── Services/          CalendarService – alkalmazásszolgáltatás (állapot + perzisztencia + lekérdezés)
└── UI/                Theme · Controls (Hónap/Hét/Napló nézet, mini naptár, DateField, DayEventsPanel)
                       ViewModels · Views (főablak, eseményszerkesztő, értesítés) · Services (ablakok, emlékeztetők, tálca)
```

Új export formátumhoz (például iCalendar `.ics`) elég egy új `ICalendarSerializer` implementáció, a meglévő kód nem változik.

## Licenc

[MIT](LICENSE): szabadon használható magán- és vállalati környezetben. Harmadik féltől származó komponensek: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
