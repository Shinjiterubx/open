/**
 * openKanban demo: pointer-based drag & drop (mouse, pen and touch), WIP limit,
 * "done" column, quick add and keyboard moves (Alt + ← / →).
 * Cards with a due date are published to the calendar demo.
 */
(() => {
  "use strict";
  const { Suite, isoDate, addDays, huDate, escapeHtml, prefersReducedMotion } = window.OpenSite;

  const LABELS = {
    urgent: { text: "Sürgős", color: "#E0434F" },
    admin: { text: "Ügyintézés", color: "#2F5A8A" },
    home: { text: "Otthon", color: "#2E8B57" },
    buy: { text: "Vásárlás", color: "#C77D0A" },
  };

  function seed() {
    const today = new Date();
    const due = (days) => isoDate(addDays(today, days));
    return {
      columns: [
        { id: "todo", title: "Teendő", color: "#8c8994", limit: null, done: false },
        { id: "doing", title: "Folyamatban", color: "#2F5A8A", limit: 3, done: false },
        { id: "done", title: "Kész", color: "#2E8B57", limit: null, done: true },
      ],
      cards: [
        { id: "c1", col: "todo", title: "Költöztető cég ajánlatai", labels: ["admin"], due: due(4), check: [1, 3] },
        { id: "c2", col: "todo", title: "Új bútorok kiválasztása", labels: ["home", "buy"], due: "", check: null, cover: "#F0B429" },
        { id: "c3", col: "todo", title: "Áramszolgáltató értesítése", labels: ["admin"], due: due(8), check: null },
        { id: "c4", col: "doing", title: "Dobozok bepakolása", labels: ["home"], due: due(1), check: [4, 7] },
        { id: "c5", col: "doing", title: "Internet átíratása", labels: ["urgent", "admin"], due: due(-1), check: null },
        { id: "c6", col: "done", title: "Dobozok beszerzése", labels: ["buy"], due: "", check: [2, 2] },
      ],
    };
  }

  function initKanbanDemo(root) {
    const board = root.querySelector(".kb-board");
    const state = seed();
    let nextId = 100;
    const reduced = prefersReducedMotion();
    const today = isoDate(new Date());

    const column = (id) => state.columns.find((c) => c.id === id);
    const cardsIn = (id) => state.cards.filter((c) => c.col === id);
    const isFull = (col) => col.limit !== null && cardsIn(col.id).length >= col.limit;

    function publish() {
      Suite.publishDeadlines(
        "kanban",
        state.cards.filter((c) => c.due && !column(c.col).done).map((c) => ({ id: c.id, date: c.due, title: c.title })),
      );
    }

    function dueBadge(card, done) {
      if (!card.due) return "";
      const [y, m, d] = card.due.split("-").map(Number);
      const days = Math.round((new Date(y, m - 1, d) - new Date(today)) / 86400000);
      const cls = done ? "is-ok" : days < 0 ? "is-overdue" : days <= 2 ? "is-soon" : "";
      return `<span class="${cls}" title="Határidő">⏱ ${huDate.format(new Date(y, m - 1, d))}</span>`;
    }

    function cardHtml(card) {
      const done = column(card.col).done;
      const labels = card.labels.map((l) => `<span class="kb-label" style="--c:${LABELS[l].color}">${LABELS[l].text}</span>`).join("");
      const check = card.check ? `<span>☑ ${done ? card.check[1] : card.check[0]}/${card.check[1]}</span>` : "";
      return `<article class="kb-card${done ? " is-done" : ""}" data-id="${card.id}" tabindex="0"
          aria-roledescription="húzható kártya" aria-label="${escapeHtml(card.title)}. Mozgatás: Alt + nyilak.">
          ${card.cover ? `<div class="cover" style="--cover:${card.cover}"></div>` : ""}
          ${labels ? `<div class="kb-labels">${labels}</div>` : ""}
          <div class="kb-title">${escapeHtml(card.title)}</div>
          ${card.due || check ? `<div class="kb-meta">${dueBadge(card, done)}${check}</div>` : ""}
        </article>`;
    }

    function render() {
      board.innerHTML = state.columns
        .map((col) => {
          const count = cardsIn(col.id).length;
          const counter = col.limit ? `${count} / ${col.limit}` : `${count}`;
          return `<section class="kb-col" data-col="${col.id}" aria-label="${col.title} oszlop">
            <header class="kb-col-head">
              <span class="stripe" style="--c:${col.color}"></span>
              <h3>${col.title}</h3>
              <span class="kb-count${isFull(col) ? " is-full" : ""}" title="${col.limit ? "WIP limit" : "Kártyák"}">${counter}</span>
            </header>
            <div class="kb-cards">${cardsIn(col.id).map(cardHtml).join("")}</div>
            <div class="kb-add"><button type="button" data-add="${col.id}">+ Kártya hozzáadása</button></div>
          </section>`;
        })
        .join("");
    }

    function refuse(colId, message) {
      const el = board.querySelector(`[data-col="${colId}"]`);
      if (!el) return;
      el.querySelector(".kb-count").title = message;
      if (reduced) return;
      anime({ targets: el, translateX: [0, -8, 8, -5, 5, 0], duration: 420, easing: "easeInOutSine" });
    }

    function moveCard(card, toCol, index) {
      // remove, then insert at index among that column's cards
      state.cards.splice(state.cards.indexOf(card), 1);
      card.col = toCol;
      const siblings = cardsIn(toCol);
      const anchor = siblings[index];
      state.cards.splice(anchor ? state.cards.indexOf(anchor) : state.cards.length, 0, card);
      render();
      publish();
    }

    // ── Pointer drag & drop ──────────────────────────────────────
    let drag = null;

    board.addEventListener("pointerdown", (e) => {
      const el = e.target.closest(".kb-card");
      if (!el || e.button !== 0) return;
      drag = { el, card: state.cards.find((c) => c.id === el.dataset.id), startX: e.clientX, startY: e.clientY, active: false, pointerId: e.pointerId };
    });

    window.addEventListener("pointermove", (e) => {
      if (!drag || e.pointerId !== drag.pointerId) return;
      if (!drag.active) {
        if (Math.hypot(e.clientX - drag.startX, e.clientY - drag.startY) < 6) return;
        startDrag(e);
      }
      e.preventDefault();
      drag.ghost.style.left = `${e.clientX - drag.dx}px`;
      drag.ghost.style.top = `${e.clientY - drag.dy}px`;
      updateTarget(e.clientX, e.clientY);
    });

    window.addEventListener("pointerup", (e) => {
      if (!drag || e.pointerId !== drag.pointerId) return;
      if (drag.active) finishDrag();
      drag = null;
    });
    window.addEventListener("pointercancel", () => {
      if (drag?.active) finishDrag(true);
      drag = null;
    });

    function startDrag(e) {
      const rect = drag.el.getBoundingClientRect();
      drag.active = true;
      drag.dx = e.clientX - rect.left;
      drag.dy = e.clientY - rect.top;
      drag.ghost = drag.el.cloneNode(true);
      drag.ghost.classList.add("kb-ghost");
      drag.ghost.style.width = `${rect.width}px`;
      drag.ghost.style.left = `${rect.left}px`;
      drag.ghost.style.top = `${rect.top}px`;
      document.body.appendChild(drag.ghost);

      drag.placeholder = document.createElement("div");
      drag.placeholder.className = "kb-placeholder";
      drag.placeholder.style.height = `${rect.height}px`;
      drag.el.after(drag.placeholder);
      drag.el.classList.add("is-dragging");
      drag.el.style.display = "none";
      drag.target = drag.card.col;
    }

    function updateTarget(x, y) {
      const colEl = document.elementFromPoint(x, y)?.closest(".kb-col");
      board.querySelectorAll(".kb-col").forEach((c) => c.classList.remove("is-target", "is-blocked"));
      if (!colEl || !board.contains(colEl)) return;

      const col = column(colEl.dataset.col);
      if (col.id !== drag.card.col && isFull(col)) {
        colEl.classList.add("is-blocked");
        drag.blocked = col.id;
        return;
      }
      drag.blocked = null;
      colEl.classList.add("is-target");
      drag.target = col.id;

      const list = colEl.querySelector(".kb-cards");
      const others = [...list.querySelectorAll(".kb-card:not(.is-dragging)")];
      const before = others.find((c) => {
        const r = c.getBoundingClientRect();
        return y < r.top + r.height / 2;
      });
      if (before) list.insertBefore(drag.placeholder, before);
      else list.appendChild(drag.placeholder);
    }

    function finishDrag(cancelled = false) {
      board.querySelectorAll(".kb-col").forEach((c) => c.classList.remove("is-target", "is-blocked"));
      const { ghost, placeholder, card, blocked } = drag;
      const list = placeholder.parentElement;
      const index = [...list.children].filter((c) => c === placeholder || !c.classList.contains("is-dragging")).indexOf(placeholder);
      const toCol = list.closest(".kb-col").dataset.col;
      const to = placeholder.getBoundingClientRect();

      const done = () => {
        ghost.remove();
        if (cancelled) render();
        else moveCard(card, toCol, index);
        if (blocked && !cancelled) refuse(blocked, "Az oszlop tele van (WIP limit).");
        if (!cancelled && column(toCol).done && !reduced) {
          const el = board.querySelector(`[data-id="${card.id}"]`);
          anime({ targets: el, scale: [1.05, 1], duration: 380, easing: "easeOutBack" });
        }
      };

      if (reduced) return done();
      anime({ targets: ghost, left: to.left, top: to.top, rotate: 0, duration: 220, easing: "easeOutCubic", complete: done });
    }

    // ── Keyboard: Alt + ← / → moves the focused card between columns ──
    board.addEventListener("keydown", (e) => {
      const el = e.target.closest(".kb-card");
      if (!el || !e.altKey || (e.key !== "ArrowLeft" && e.key !== "ArrowRight")) return;
      e.preventDefault();
      const card = state.cards.find((c) => c.id === el.dataset.id);
      const i = state.columns.findIndex((c) => c.id === card.col) + (e.key === "ArrowRight" ? 1 : -1);
      const target = state.columns[i];
      if (!target) return;
      if (isFull(target)) return refuse(target.id, "Az oszlop tele van (WIP limit).");
      moveCard(card, target.id, cardsIn(target.id).length);
      board.querySelector(`[data-id="${card.id}"]`)?.focus();
    });

    // ── Quick add ───────────────────────────────────────────────
    board.addEventListener("click", (e) => {
      const add = e.target.closest("[data-add]");
      if (!add) return;
      const col = column(add.dataset.add);
      if (isFull(col)) return refuse(col.id, "Az oszlop tele van (WIP limit).");
      const wrap = add.parentElement;
      wrap.innerHTML = `<form>
          <input name="cardTitle" type="text" placeholder="Kártya címe" aria-label="Kártya címe" maxlength="60" required />
          <div class="row">
            <input name="cardDue" type="date" aria-label="Határidő (nem kötelező)" />
            <button class="btn btn-small" type="submit">Hozzáadás</button>
          </div>
        </form>`;
      const form = wrap.querySelector("form");
      form.elements.namedItem("cardTitle").focus();
      form.addEventListener("submit", (ev) => {
        ev.preventDefault();
        if (isFull(col)) return refuse(col.id, "Az oszlop tele van (WIP limit).");
        const title = form.elements.namedItem("cardTitle").value.trim();
        if (!title) return;
        state.cards.push({ id: `c${nextId++}`, col: col.id, title, labels: [], due: form.elements.namedItem("cardDue").value, check: null });
        render();
        publish();
        // keep the composer open for the next card, like the app
        board.querySelector(`[data-add="${col.id}"]`)?.click();
      });
      form.addEventListener("keydown", (ev) => {
        if (ev.key === "Escape") render();
      });
    });

    render();
    publish();
  }

  Object.assign(window.OpenSite, { initKanbanDemo });
})();
