# Third-party notices

The open* suite (openNotes, openCalendar, openKanban, openLauncher and the landing page) is licensed under the
[MIT License](LICENSE). It uses the following third-party components. Every one of them permits free use in
private and commercial environments, including redistribution with the apps.

## Desktop apps

| Component | License | Used by |
|---|---|---|
| [Inter](https://rsms.me/inter/) font – Rasmus Andersson | SIL Open Font License 1.1 | all apps (embedded UI font) |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT | all apps |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) | MIT | all apps |
| .NET 8 runtime (self-contained builds) | MIT | all apps |
| [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) | MIT | openNotes |
| [Markdig](https://github.com/xoofx/markdig) | BSD 2-Clause | openNotes |
| [Microsoft.Web.WebView2](https://www.nuget.org/packages/Microsoft.Web.WebView2) | Microsoft WebView2 SDK license (redistributable) | openNotes |

The Inter font may be bundled with software but may not be sold on its own (OFL 1.1).
The WebView2 *runtime* is part of Windows 10/11 and is not redistributed by the installers.

## Installers

The installers are built with [Inno Setup](https://jrsoftware.org/isinfo.php) (Inno Setup License – free for
commercial use). Inno Setup itself is not part of this repository.

## Landing page (`landing/`)

three.js (r160, `three.min.js`) and anime.js (3.2.2, `anime.min.js`) are bundled in `landing/vendor/` together with their license files.
The only change: the deprecation `console.warn` at the top of `three.min.js` is replaced by `void 0`.

| Component | License |
|---|---|
| [three.js](https://threejs.org) | MIT |
| [anime.js](https://animejs.com) | MIT |
| [Bricolage Grotesque](https://fonts.google.com/specimen/Bricolage+Grotesque) and [Inter](https://rsms.me/inter/) (via Google Fonts) | SIL Open Font License 1.1 |

Each app folder also contains its own `THIRD-PARTY-NOTICES.md` (shipped with that app's installer).
