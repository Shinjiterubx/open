/**
 * openCalendar demo: month grid, recurring events, day agenda with quick add,
 * plus the deadlines published by the notes and Kanban demos.
 */
(() => {
  "use strict";
  const { Suite, isoDate, addDays, huMonth, huWeekday, escapeHtml, APPS, prefersReducedMotion } = window.OpenSite;

  const WEEKDAYS = ["H", "K", "Sze", "Cs", "P", "Szo", "V"];
  const SOURCE_COLOR = { event: APPS.calendar.color, notes: APPS.notes.color, kanban: APPS.kanban.color };
  const SOURCE_NAME = { notes: "openNotes", kanban: "openKanban" };

  /** Recurring rules, like the app's "hetente, a kiválasztott napokon". weekday: 0 = Monday. */
  const RECURRING = [
    { weekday: 0, time: "09:00", title: "Heti egyeztetés", note: "hetente, hétfőn" },
    { weekday: 2, time: "18:00", title: "Edzés", note: "hetente, szerdán" },
    { weekday: 4, time: "16:30", title: "Heti zárás", note: "hetente, pénteken" },
  ];

  function initCalendarDemo(root) {
    const grid = root.querySelector(".cal-grid");
    const monthLabel = root.querySelector(".cal-month");
    const dayTitle = root.querySelector(".cal-day");
    const items = root.querySelector(".cal-items");
    const form = root.querySelector(".cal-add");

    const today = new Date();
    today.setHours(0, 0, 0, 0);
    let view = new Date(today.getFullYear(), today.getMonth(), 1);
    let selected = new Date(today);

    /** One-off events by ISO date. */
    const events = new Map([
      [isoDate(addDays(today, 1)), [{ time: "12:30", title: "Ebéd Annával" }]],
      [isoDate(addDays(today, 3)), [{ time: "08:00", title: "Fogorvos" }]],
      [isoDate(addDays(today, 9)), [{ time: "", title: "Szabadság", allDay: true }]],
    ]);

    function entriesOn(date) {
      const iso = isoDate(date);
      const weekday = (date.getDay() + 6) % 7;
      const list = [
        ...(events.get(iso) ?? []).map((e) => ({ ...e, source: "event" })),
        ...RECURRING.filter((r) => r.weekday === weekday).map((r) => ({ ...r, source: "event" })),
        ...Suite.deadlinesOn(iso).map((d) => ({ time: "", title: d.title, source: d.source, note: `határidő – ${SOURCE_NAME[d.source]}` })),
      ];
      return list.sort((a, b) => (a.time || "00:00").localeCompare(b.time || "00:00"));
    }

    function renderGrid() {
      monthLabel.textContent = huMonth.format(view);
      const start = addDays(view, -((view.getDay() + 6) % 7)); // Monday on or before the 1st
      const cells = WEEKDAYS.map((d) => `<div class="cal-head" role="columnheader">${d}</div>`);

      for (let i = 0; i < 42; i++) {
        const date = addDays(start, i);
        const iso = isoDate(date);
        const entries = entriesOn(date);
        const pills = entries
          .slice(0, 2)
          .map((e) => `<span class="cal-pill" style="--c:${SOURCE_COLOR[e.source]}">${escapeHtml(e.time ? `${e.time} ${e.title}` : e.title)}</span>`)
          .join("");
        const more = entries.length > 2 ? `<span class="cal-more">+${entries.length - 2} további</span>` : "";
        const classes = ["cal-cell", date.getMonth() !== view.getMonth() && "is-out", iso === isoDate(today) && "is-today"].filter(Boolean).join(" ");
        cells.push(
          `<button type="button" role="gridcell" class="${classes}" data-date="${iso}" aria-selected="${iso === isoDate(selected)}"
             aria-label="${huWeekday.format(date)}, ${entries.length} bejegyzés"><span class="num">${date.getDate()}</span>${pills}${more}</button>`,
        );
      }
      grid.innerHTML = cells.join("");
    }

    function renderAgenda() {
      dayTitle.textContent = huWeekday.format(selected);
      const entries = entriesOn(selected);
      items.innerHTML = entries.length
        ? entries
            .map(
              (e) => `<li style="--c:${SOURCE_COLOR[e.source]}"><time>${e.time || (e.allDay ? "egész nap" : "–")}</time>
                <span>${escapeHtml(e.title)}${e.note ? `<small>${escapeHtml(e.note)}</small>` : ""}</span></li>`,
            )
            .join("")
        : `<li class="empty">Erre a napra nincs bejegyzés. Adj hozzá egyet lent.</li>`;
    }

    function render() {
      renderGrid();
      renderAgenda();
    }

    grid.addEventListener("click", (e) => {
      const cell = e.target.closest(".cal-cell");
      if (!cell) return;
      const [y, m, d] = cell.dataset.date.split("-").map(Number);
      selected = new Date(y, m - 1, d);
      if (selected.getMonth() !== view.getMonth()) view = new Date(y, m - 1, 1);
      render();
    });

    root.querySelector('[data-action="prev"]').addEventListener("click", () => {
      view = new Date(view.getFullYear(), view.getMonth() - 1, 1);
      renderGrid();
    });
    root.querySelector('[data-action="next"]').addEventListener("click", () => {
      view = new Date(view.getFullYear(), view.getMonth() + 1, 1);
      renderGrid();
    });

    form.addEventListener("submit", (e) => {
      e.preventDefault();
      const titleInput = form.elements.namedItem("title"); // not form.title – that is the form's own attribute
      const title = titleInput.value.trim();
      if (!title) return;
      const iso = isoDate(selected);
      events.set(iso, [...(events.get(iso) ?? []), { time: form.elements.namedItem("time").value, title }]);
      titleInput.value = "";
      render();
      if (!prefersReducedMotion()) {
        const cell = grid.querySelector(`[data-date="${iso}"]`);
        anime({ targets: cell?.querySelectorAll(".cal-pill"), scale: [0.6, 1], opacity: [0, 1], duration: 420, easing: "easeOutBack" });
      }
    });

    Suite.onChange(render);
    render();
  }

  Object.assign(window.OpenSite, { initCalendarDemo });
})();
