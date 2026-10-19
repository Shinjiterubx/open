# openLauncher

Az **open\*** alkalmazáscsomag indítója és telepítője – **openNotes**, **openCalendar** és **openKanban** egy helyen.
Nyílt forráskódú (MIT), céges és otthoni használatra is.

![icon](installer/icon-1024.png)

## Mit tud?

- **Indítás** – minden alkalmazás egy kattintással, a saját színével és ikonjával.
- **Telepítés utólag** – a hiányzó alkalmazások a launcherből telepíthetők (internet nélkül: a telepítők a launcher mellett vannak).
- **Eltávolítás** – megerősítés után az alkalmazás saját eltávolítójával; ha fut, előtte bezárja. A felhasználói adatok (jegyzetek, naptár, táblák) megmaradnak.
- **Frissítés / javítás** – ha a csomagolt telepítő újabb, a kártyán megjelenik a „Frissítés” gomb; a „…” menüben újratelepítés és a telepítési mappa megnyitása is van.
- **Állapot** – telepítve (verzióval), hordozható példány, nincs telepítve, fut. Ablakaktiváláskor és F5-re frissül.
- Egy példányban fut; újbóli indításkor az előző ablak jön előre.

## Telepítő

Az `openSuite-Setup-<verzió>-x64.exe` egy közös telepítő:

| Típus | Tartalom |
|---|---|
| Teljes | openLauncher + mindhárom alkalmazás |
| Csak az openLauncher | a többi később a launcherből telepíthető |
| Egyéni | a komponenslistán bepipált alkalmazások |

- Alapból **felhasználónként** telepít (nem kell rendszergazda); rendszergazda „minden felhasználó” módot is választhat – a kiválasztott alkalmazások ugyanabban a módban kerülnek fel.
- Az alkalmazások a **saját telepítőjükkel**, csendesen települnek, így a Windows „Alkalmazások” listájában külön-külön szerepelnek, és külön is eltávolíthatók.
- A launcher eltávolítása **nem** távolítja el az alkalmazásokat.
- Céges terjesztéshez: `openSuite-Setup.exe /VERYSILENT /COMPONENTS="launcher,opennotes,openkanban"`.

## Fordítás

A repó gyökerében egymás mellett van a négy projekt (`openNotes`, `openCalendar`, `openKanban`, `openLauncher`);
a build ezekre a testvérmappákra hivatkozik.

```powershell
cd openLauncher
.\build-all.cmd                     # mindent lefordít, tesztel, és elkészíti a közös telepítőt
.\build-all.cmd -Version 1.1.0
.\build-all.cmd -SkipApps           # az installer\payload-ban lévő telepítőket használja újra
```

(A gyökérben lévő `build.cmd` ugyanezt hívja.)

Kell hozzá: .NET 8 SDK, Inno Setup 6 (`winget install JRSoftware.InnoSetup`).
Eredmény: `artifacts\installer\openSuite-Setup-<verzió>-x64.exe`.

Fejlesztéshez (Visual Studio / `dotnet run`) másold a telepítőket a `bin\…\Installers` mappába, vagy telepítsd az alkalmazásokat külön – a launcher a már telepítetteket felismeri.

## Felépítés

```
src/OpenLauncher
├─ Core/            modellek (AppDefinition, AppStatus), interfészek, verzió-összehasonlítás
├─ Infrastructure/  AppCatalog, Inno Setup alapú telepítés/felismerés, beállítások, egy-példány őr
└─ UI/              MVVM nézetmodellek (AppCardViewModel, MainViewModel), nézetek, téma
```

**Új alkalmazás hozzáadása:** egy sor az `AppCatalog`-ba (név, szín, exe, Inno `AppId`, telepítő neve),
egy ikon az `Assets/Apps/<Id>.png`-be, valamint egy-egy sor az `installer/openLauncher.iss` `[Components]`, `[Files]`
és `[Run]` szakaszába és a `build-all.ps1` `$apps` listájába.

### Felismerés

1. `HK(CU|LM)\Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppId}_is1` – telepített példány (verzió, mappa, eltávolító);
2. `HKCU\Software\<app>\ExePath` – ezt minden alkalmazás induláskor beírja, így a hordozható példányok is indíthatók.

## Licenc

MIT – lásd [LICENSE](LICENSE). Az Inter betűtípus SIL OFL 1.1 licencű.
