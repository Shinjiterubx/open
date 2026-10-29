/**
 * Site configuration.
 * On GitHub Pages (<user>.github.io/<repo>/) the repository is detected automatically.
 * For a custom domain set MANUAL_REPO, e.g. "username/open".
 */
window.OpenSite = window.OpenSite || {};

(() => {
  "use strict";
  const MANUAL_REPO = "";

  function detectRepo() {
    if (MANUAL_REPO) return MANUAL_REPO;
    const host = location.hostname;
    if (host.endsWith(".github.io")) {
      const user = host.split(".")[0];
      const first = location.pathname.split("/").filter(Boolean)[0];
      return first ? `${user}/${first}` : `${user}/${host}`;
    }
    return null; // local preview: links fall back to the page anchors
  }

  const REPO = detectRepo();
  const REPO_URL = REPO ? `https://github.com/${REPO}` : null;

  const LINKS = {
    repo: REPO_URL ?? "#top",
    release: REPO_URL ? `${REPO_URL}/releases/latest` : "#download",
    releases: REPO_URL ? `${REPO_URL}/releases` : "#download",
    license: REPO_URL ? `${REPO_URL}/blob/main/LICENSE` : "#top",
    clone: REPO_URL ? `${REPO_URL}.git` : "https://github.com/<felhasználó>/<repó>.git",
  };

  const APPS = {
    notes: { name: "openNotes", color: "#5B3E96" },
    calendar: { name: "openCalendar", color: "#D45D79" },
    kanban: { name: "openKanban", color: "#2F5A8A" },
    launcher: { name: "openLauncher", color: "#D2691E" },
  };

  const prefersReducedMotion = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  Object.assign(window.OpenSite, { REPO, REPO_URL, LINKS, APPS, prefersReducedMotion });
})();
