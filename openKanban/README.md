<p align="center"><img src="src/OpenKanban/Assets/icon.png" width="112" alt="openKanban" /></p>

<h1 align="center">openKanban</h1>

<p align="center">
  Nyílt forráskódú Kanban-táblák Windowsra – helyi tárolással, openNotes és openCalendar kapcsolattal.<br/>
  <sub>Open source Kanban boards for Windows. Local storage, MIT licensed.</sub>
</p>

---

## Funkciók

- **Workspace-ek → táblák → oszlopok → kártyák.** Mindegyiknek saját neve és színe van; a táblának háttérszíne és saját címkéi is vannak.
- **Húzd és ejtsd:** a kártyák az oszlopon belül és oszlopok között is mozgathatók, az oszlopok sorrendje is átrendezhető. A beillesztés helyét egy jelölővonal mutatja.
- **Oszlopok:**
  - Korlátozható, hány kártya lehet bennük (WIP limit). Tele oszlopba nem lehet kártyát hozzáadni vagy áthúzni, a számláló ilyenkor narancssárga lesz.
  - Egy oszlop „Kész” oszlopnak jelölhető, így az odahúzott kártyák automatikusan késznek számítanak.
- **Kártyák:**
  - Leírás és borítószín.
  - Címkék.
  - Határidő dátummal és időponttal (a lejárt és a hamarosan esedékes színnel kiemelve).
  - Kész állapot.
  - Ellenőrzőlista haladásjelzővel.
  - A kártya áthelyezhető másik oszlopba.
- **Gyors felvétel:** az oszlop alján „+ Kártya hozzáadása”. Az Enter hozzáadja a kártyát, a mező nyitva marad a következőhöz.
- **Szűrés** a táblán (`Ctrl+F`): címre, leírásra és címkére.
- **`.openKanban` export és import:** teljes workspace egy fájlban. Behúzással vagy dupla kattintással is importálható; ha már létezik, felülírható vagy másolatként adható hozzá. Leírás: [docs/FORMAT.md](docs/FORMAT.md).
- **Integráció** (opcionális, egyik app sem függ a másiktól):
  - **openNotes:** jegyzet kapcsolható a kártyához, egy kattintással megnyílik.
  - **openCalendar:** a határidős kártyák megjelennek a naptárban, és a kártyából a nap megnyitható a naptárban.
- Az **openLauncher**-ből indítható, telepíthető és eltávolítható; a közös `openSuite-Setup` telepítő is tartalmazza.

## Fordítás és telepítő

```powershell
dotnet run --project src/OpenKanban
dotnet test
.\publish.cmd -Version 1.0.0      # → artifacts\installer\openKanban-Setup-1.0.0-x64.exe
```

## Adatok

| | |
|---|---|
| Workspace-ek | `%APPDATA%\openKanban\Workspaces\*.openKanban` |
| Törölt workspace-ek | `%APPDATA%\openKanban\Trash` |
| Határidő-feed (openCalendarnak) | `%APPDATA%\openKanban\deadlines.json` |
| Beállítások | `%APPDATA%\openKanban\settings.json` |

Hordozható mód: tegyél egy üres `portable.txt` fájlt az exe mellé.

## Felépítés

```
src/OpenKanban
├── Core/            Models (Workspace, Board, BoardColumn, Card, CardLabel) · Boards (BoardOperations: mozgatás, WIP, klónozás;
│                    CardDue; DeadlineEntry) · Abstractions · Time
├── Infrastructure/  Storage (.openKanban ZIP+JSON, repository, beállítások) · Integration (deadline feed, openNotes olvasó,
│                    társalkalmazás-indító, registry) · Platform
├── Services/        KanbanService – minden módosítás egy helyen, mentés + feed frissítés
└── UI/              BoardView (drag & drop), WorkspaceOverview, ColorPicker, DateField · ViewModels · Views (kártya, oszlop, tábla,
                     workspace dialógus) · Services
```

## Licenc

[MIT](LICENSE)
