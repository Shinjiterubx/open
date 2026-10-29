/**
 * Tiny shared store for the demos – the web version of the apps' integration:
 * openNotes and openKanban publish deadlines, openCalendar listens and shows them.
 */
(() => {
  "use strict";
  const deadlines = new Map(); // key "notes:<id>" / "kanban:<id>" → { date, title, source }
  const listeners = new Set();

  const Suite = {
    /** @param {"notes"|"kanban"} source */
    publishDeadlines(source, items) {
      for (const key of [...deadlines.keys()]) if (key.startsWith(source + ":")) deadlines.delete(key);
      for (const item of items) {
        if (item.date) deadlines.set(`${source}:${item.id}`, { ...item, source });
      }
      listeners.forEach((fn) => fn());
    },

    deadlinesOn(isoDate) {
      return [...deadlines.values()].filter((d) => d.date === isoDate);
    },

    onChange(fn) {
      listeners.add(fn);
      return () => listeners.delete(fn);
    },
  };

  /** Local-time ISO date (yyyy-mm-dd). */
  function isoDate(date) {
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, "0");
    const d = String(date.getDate()).padStart(2, "0");
    return `${y}-${m}-${d}`;
  }

  function addDays(date, days) {
    const copy = new Date(date);
    copy.setDate(copy.getDate() + days);
    return copy;
  }

  const huDate = new Intl.DateTimeFormat("hu-HU", { month: "long", day: "numeric" });
  const huMonth = new Intl.DateTimeFormat("hu-HU", { year: "numeric", month: "long" });
  const huWeekday = new Intl.DateTimeFormat("hu-HU", { weekday: "long", month: "long", day: "numeric" });

  function escapeHtml(text) {
    return String(text).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
  }

  Object.assign(window.OpenSite, { Suite, isoDate, addDays, huDate, huMonth, huWeekday, escapeHtml });
})();
