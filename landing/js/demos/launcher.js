/**
 * openLauncher demo: install, launch and uninstall the three apps.
 * Mirrors the real launcher: status chips, busy state with a progress bar, confirm before removing.
 */
(() => {
  "use strict";
  const { prefersReducedMotion } = window.OpenSite;

  const APPS = [
    { id: "notes", name: "openNotes", tag: "Jegyzetek Markdownban", color: "#5B3E96", icon: "assets/icons/openNotes.png", installed: true },
    { id: "calendar", name: "openCalendar", tag: "Naptár és emlékeztetők", color: "#D45D79", icon: "assets/icons/openCalendar.png", installed: false },
    { id: "kanban", name: "openKanban", tag: "Kanban táblák", color: "#2F5A8A", icon: "assets/icons/openKanban.png", installed: false },
  ];

  function initLauncherDemo(root) {
    const grid = root.querySelector(".ln-grid");
    const summary = root.querySelector(".ln-summary");
    const reduced = prefersReducedMotion();
    const state = APPS.map((a) => ({ ...a, running: false, busy: null, confirm: false }));

    function cardHtml(app) {
      const chips = [
        `<span class="ln-chip${app.installed ? " is-installed" : ""}">${app.installed ? "Telepítve · v1.0.0" : "Nincs telepítve"}</span>`,
        app.running ? `<span class="ln-chip is-running">Fut</span>` : "",
      ].join("");

      const actions = app.busy
        ? `<div class="ln-busy"><p>${app.busy}</p><div class="ln-bar"><i></i></div></div>`
        : `<div class="ln-actions">
            ${
              app.installed
                ? `<button type="button" class="btn go" data-do="launch">${app.running ? "Előtérbe hozás" : "Indítás"}</button>
                   <button type="button" class="btn ghost" data-do="remove">${app.confirm ? "Biztosan eltávolítod?" : "Eltávolítás"}</button>`
                : `<button type="button" class="btn go" data-do="install">Telepítés</button>`
            }
          </div>`;

      return `<article class="ln-card" data-id="${app.id}" style="--c:${app.color}">
        <div class="strip"></div>
        <div class="body">
          <header><img src="${app.icon}" alt="" width="40" height="40" /><div><h3>${app.name}</h3><span class="tag">${app.tag}</span></div></header>
          <div class="ln-chips">${chips}</div>
        </div>
        ${actions}
      </article>`;
    }

    function render() {
      grid.innerHTML = state.map(cardHtml).join("");
      summary.textContent = `${state.filter((a) => a.installed).length} / ${state.length} alkalmazás telepítve`;
    }

    function runBusy(app, text, finish) {
      app.busy = text;
      app.confirm = false;
      render();
      const bar = grid.querySelector(`[data-id="${app.id}"] .ln-bar i`);
      const complete = () => {
        app.busy = null;
        finish();
        render();
      };
      if (reduced) {
        setTimeout(complete, 400);
        return;
      }
      anime({ targets: bar, width: ["0%", "100%"], duration: 1700, easing: "easeInOutQuad", complete });
    }

    grid.addEventListener("click", (e) => {
      const button = e.target.closest("[data-do]");
      if (!button) return;
      const app = state.find((a) => a.id === button.closest(".ln-card").dataset.id);

      switch (button.dataset.do) {
        case "install":
          runBusy(app, "Telepítés…", () => (app.installed = true));
          break;
        case "launch":
          app.running = true;
          app.confirm = false;
          render();
          if (!reduced) {
            const card = grid.querySelector(`[data-id="${app.id}"]`);
            anime({ targets: card.querySelector("header img"), scale: [1, 1.18, 1], duration: 520, easing: "easeOutQuad" });
          }
          break;
        case "remove":
          if (!app.confirm) {
            app.confirm = true;
            render();
            setTimeout(() => {
              if (app.confirm) {
                app.confirm = false;
                render();
              }
            }, 3500);
            return;
          }
          app.running = false; // the real launcher closes the app first
          runBusy(app, "Eltávolítás…", () => (app.installed = false));
          break;
      }
    });

    render();
  }

  Object.assign(window.OpenSite, { initLauncherDemo });
})();
