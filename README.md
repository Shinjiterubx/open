<p align="center">
  <img src="docs/images/openNotes.png" width="84" alt="openNotes" />
  <img src="docs/images/openCalendar.png" width="84" alt="openCalendar" />
  <img src="docs/images/openKanban.png" width="84" alt="openKanban" />
  <img src="docs/images/openLauncher.png" width="84" alt="openLauncher" />
</p>

<h1 align="center">open* – nyílt forráskódú produktivitási csomag Windowsra</h1>

<p align="center">
  Jegyzetek, naptár és Kanban-táblák – helyben tárolt adatokkal, egymással összekötve, egy közös indítóval.<br/>
  <sub>Notes, calendar and Kanban boards for Windows – local-first, connected, MIT licensed.</sub>
</p>

<p align="center">
  <img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-2F5A8A" />
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8-5B3E96" />
  <img alt="Windows 10/11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-D45D79" />
  <img alt="Made with Claude Opus 5.5" src="https://img.shields.io/badge/k%C3%A9sz%C3%BClt-Claude%20Opus%205.5-D2691E" />
</p>

---

## Az alkalmazások

| | Alkalmazás | Mire jó |
|---|---|---|
| <img src="docs/images/openNotes.png" width="44" /> | **[openNotes](openNotes)** | Markdown jegyzetelő mini ablakkal, globális gyorsbillentyűkkel, képekkel, teendőlistával, színes mappákkal, lezárt jegyzetekkel, kártyanézettel, prioritással, határidővel és emlékeztetőkkel. |
| <img src="docs/images/openCalendar.png" width="44" /> | **[openCalendar](openCalendar)** | Naptár nap/hét/hónap/napló nézettel, ismétlődő eseményekkel, emlékeztetőkkel – az openNotes és openKanban határidőivel együtt. |
| <img src="docs/images/openKanban.png" width="44" /> | **[openKanban](openKanban)** | Trello-szerű workspace-ek, táblák, oszlopok és kártyák húzd-és-ejtsd kezeléssel, WIP limittel, címkékkel, határidőkkel, ellenőrzőlistával. |
| <img src="docs/images/openLauncher.png" width="44" /> | **[openLauncher](openLauncher)** | Közös indító: az alkalmazások indítása, utólagos telepítése, frissítése és eltávolítása egy helyen. |

Mindegyik alkalmazás **önállóan is működik** – a kapcsolatok (határidők a naptárban, jegyzet a kártyán)
csak akkor élnek, ha a másik alkalmazás is telepítve van.

## Letöltés és telepítés

A kész telepítők a **[Releases](../../releases/latest)** oldalon vannak:

| Fájl | Tartalom |
|---|---|
| `openSuite-Setup-<verzió>-x64.exe` | **Közös telepítő:** openLauncher + a kiválasztott alkalmazások (Teljes / Csak launcher / Egyéni). |
| `openNotes-Setup-…`, `openCalendar-Setup-…`, `openKanban-Setup-…` | Egy-egy alkalmazás külön. |

- Windows 10 (1809) vagy újabb, 64 bit. Minden kijelzőméreten és nagyításon (100–200 %) képernyőre igazított ablakok. **Nem kell** hozzá .NET (a telepítők tartalmazzák).
- Alapból **felhasználónként** telepít, rendszergazdai jog nélkül; rendszergazda „minden felhasználó” módot is választhat.
- Céges, csendes telepítés:
  ```
  openSuite-Setup-1.0.0-x64.exe /VERYSILENT /COMPONENTS="launcher,opennotes,opencalendar,openkanban"
  ```
- A telepítők nincsenek digitálisan aláírva, ezért a SmartScreen első indításkor figyelmeztethet
  („További információ” → „Futtatás mindenképp”).

## Adatok és adatvédelem

- Minden adat **helyben**, a felhasználó profiljában tárolódik (`%APPDATA%\openNotes`, `\openCalendar`, `\openKanban`). Nincs fiók, felhő vagy telemetria.
- Saját, nyitott fájlformátumok (ZIP + JSON/Markdown), leírással:
  [`.openNote`](openNotes/docs/FORMAT.md) · [`.opencalendar`](openCalendar/docs/FORMAT.md) · [`.openKanban`](openKanban/docs/FORMAT.md).
- Hordozható mód: egy üres `portable.txt` az exe mellett → az adatok az exe melletti `Data` mappába kerülnek.

## Hogyan kapcsolódnak?

```
          határidők (olvasás)                 határidős kártyák (deadlines.json)
openNotes ───────────────────► openCalendar ◄────────────────────────────── openKanban
    ▲      ◄── --open-note ───                ── --open-card ──►                │
    └──────────────────────── jegyzet kapcsolása kártyához ─────────────────────┘

                     openLauncher: indítás · telepítés · frissítés · eltávolítás
```

Minden alkalmazás induláskor beírja a helyét a `HKCU\Software\<alkalmazás>` kulcsba, a többiek innen találják meg.
Parancssori belépési pontok: `openNotes.exe --open-note <id>`, `openCalendar.exe --goto yyyy-MM-dd`, `openKanban.exe --open-card <id>`.

## Fordítás

Követelmények: Windows, [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), a telepítőkhöz [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

```powershell
git clone <repo-url> open
cd open

dotnet test open.sln                 # minden teszt
.\build.cmd                          # minden app + telepítők + közös telepítő
.\build.cmd -Version 1.1.0           # alapból a VERSION fájl verziója
```

Eredmény: `openLauncher\artifacts\installer\openSuite-Setup-<verzió>-x64.exe`, az egyes appok telepítői a saját `artifacts\installer` mappájukban.
Egy alkalmazás külön: `cd openNotes` → `.\publish.cmd`. Visual Studio 2022-ben az `open.sln` nyitható meg.

## Felépítés

```
open/
├─ openNotes/      jegyzetelő            (src · tests · installer · build · docs)
├─ openCalendar/   naptár                (…)
├─ openKanban/     Kanban-táblák         (…)
├─ openLauncher/   indító + közös telepítő (installer/openLauncher.iss, build/build-all.ps1)
├─ landing/        bemutató weboldal (GitHub Pages)
├─ docs/images/    ikonok a README-hez
├─ .github/workflows/   CI: build + teszt, kiadás tagre, Pages
├─ build.cmd       a teljes csomag fordítása
└─ open.sln        az összes projekt egy solutionben
```

Az alkalmazások WPF / .NET 8 alapúak, MVVM (CommunityToolkit.Mvvm) és dependency injection szerint felépítve,
OOP / SOLID elvek mentén: a `Core` réteg modellje és interfészei nem függnek a UI-tól, az `Infrastructure`
valósítja meg a tárolást és az integrációt, a `UI` réteg csak nézetmodelleket és nézeteket tartalmaz.
Részletek az egyes alkalmazások README-jében.

## Weboldal

A `landing/` mappa egy statikus bemutató oldal (Three.js + anime.js) interaktív demókkal.
A `main` ágra pusholva a GitHub Actions automatikusan kiteszi a GitHub Pagesre.
Helyben elég duplán kattintani a `landing/index.html`-re (nincs build lépés, nincs szerver).

## Közreműködés

Hibajegyek és pull requestek szívesen. PR előtt fusson le a `dotnet test open.sln`, a stílust az `.editorconfig` írja le.

## Készítés

A csomag a **Claude Opus 5.5** (Anthropic) használatával készült – a tervezéstől a kódon és a telepítőkön át a weboldalig.

## Licenc

[MIT](LICENSE) © 2026 Szegedi Tamás és közreműködők – szabadon használható magán- és céges környezetben, módosítható és terjeszthető.
A felhasznált külső komponensek licencei: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
