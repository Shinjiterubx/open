/**
 * openNotes demo: note list, Markdown editor with live preview, clickable to-dos,
 * priority, deadline (published to the calendar demo) and mini mode.
 */
(() => {
  "use strict";
  const { Suite, isoDate, addDays, escapeHtml, prefersReducedMotion } = window.OpenSite;

  const PRIORITY_COLORS = { high: "#E0434F", medium: "#F0B429", low: "#8B6BD6" };

  function seedNotes() {
    const today = new Date();
    return [
      {
        id: "n1",
        prio: "high",
        due: isoDate(addDays(today, 2)),
        md: `# Költözés – teendők

  Ami még hátravan a **költözés** előtt:

  - [x] Dobozok beszerzése
  - [ ] Internet átíratása
  - [ ] Lakcímváltozás bejelentése
  - [ ] Kulcsok átadása

  > A dobozok a pincében vannak, a *kék* címkések törékenyek.`,
      },
      {
        id: "n2",
        prio: "medium",
        due: isoDate(addDays(today, 6)),
        md: `# Csapatmegbeszélés

  ## Napirend
  - Sprint áttekintés
  - Új \`openKanban\` tábla bemutatása
  - Szabadságok egyeztetése

  ## Döntések
  - [ ] Release jövő csütörtökön`,
      },
      {
        id: "n3",
        prio: "low",
        due: "",
        md: `# Ötletek

  - Hétvégi túra a Börzsönyben
  - Könyv: *A gyakorlat teszi a mestert*
  - ~~Új bicikli~~ – megvan!`,
      },
    ];
  }

  /** Small, safe Markdown subset: headings, lists, to-dos, quotes, bold/italic/strike/code. */
  function renderMarkdown(md) {
    const inline = (text) =>
      escapeHtml(text)
        .replace(/`([^`]+)`/g, "<code>$1</code>")
        .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
        .replace(/(^|[^*])\*([^*]+)\*/g, "$1<em>$2</em>")
        .replace(/~~([^~]+)~~/g, "<del>$1</del>");

    const out = [];
    let list = false;
    const closeList = () => {
      if (list) out.push("</ul>");
      list = false;
    };

    md.split("\n").forEach((line, index) => {
      const task = line.match(/^\s*[-*] \[( |x|X)\] (.*)$/);
      const bullet = line.match(/^\s*[-*] (.*)$/);
      const heading = line.match(/^(#{1,3}) (.*)$/);

      if (task) {
        if (!list) out.push("<ul>");
        list = true;
        const done = task[1].toLowerCase() === "x";
        out.push(
          `<li class="task${done ? " done" : ""}"><label><input type="checkbox" data-line="${index}"${done ? " checked" : ""} /><span>${inline(task[2])}</span></label></li>`,
        );
      } else if (bullet) {
        if (!list) out.push("<ul>");
        list = true;
        out.push(`<li>${inline(bullet[1])}</li>`);
      } else {
        closeList();
        if (heading) out.push(`<h${heading[1].length}>${inline(heading[2])}</h${heading[1].length}>`);
        else if (line.startsWith("> ")) out.push(`<blockquote>${inline(line.slice(2))}</blockquote>`);
        else if (line.trim()) out.push(`<p>${inline(line)}</p>`);
      }
    });
    closeList();
    return out.join("");
  }

  const titleOf = (note) => (note.md.match(/^#{1,3} (.+)$/m)?.[1] ?? note.md.split("\n").find((l) => l.trim()) ?? "Névtelen jegyzet").trim();
  const excerptOf = (note) =>
    note.md
      .split("\n")
      .filter((l) => l.trim() && !l.startsWith("#"))
      .map((l) => l.replace(/^[-*>] (\[.\] )?/, ""))[0] ?? "";

  function initNotesDemo(root) {
    const list = root.querySelector(".notes-list");
    const input = root.querySelector(".md-input");
    const preview = root.querySelector(".md-preview");
    const dueInput = root.querySelector('[data-field="due"]');
    const prioButtons = [...root.querySelectorAll("[data-prio]")];
    const miniButton = root.querySelector('[data-action="mini"]');

    const notes = seedNotes();
    let current = notes[0];

    function publish() {
      Suite.publishDeadlines(
        "notes",
        notes.map((n) => ({ id: n.id, date: n.due, title: titleOf(n) })),
      );
    }

    function renderList() {
      list.innerHTML = notes
        .map(
          (n) => `<button type="button" class="note-item" data-id="${n.id}" aria-current="${n === current}">
            <strong><span class="dot" style="background:${PRIORITY_COLORS[n.prio]}"></span>${escapeHtml(titleOf(n))}</strong>
            <small>${escapeHtml(excerptOf(n))}</small>
          </button>`,
        )
        .join("");
    }

    function renderPreview() {
      preview.innerHTML = renderMarkdown(current.md);
    }

    function renderMeta() {
      prioButtons.forEach((b) => b.setAttribute("aria-checked", String(b.dataset.prio === current.prio)));
      dueInput.value = current.due;
    }

    function select(note) {
      current = note;
      input.value = note.md;
      renderList();
      renderPreview();
      renderMeta();
    }

    list.addEventListener("click", (e) => {
      const item = e.target.closest(".note-item");
      if (item) select(notes.find((n) => n.id === item.dataset.id));
    });

    input.addEventListener("input", () => {
      current.md = input.value;
      renderPreview();
      renderList();
      publish();
    });

    // Ticking a to-do in the preview edits the Markdown, like in the app.
    preview.addEventListener("change", (e) => {
      const box = e.target.closest("input[data-line]");
      if (!box) return;
      const lines = current.md.split("\n");
      const i = Number(box.dataset.line);
      lines[i] = lines[i].replace(/\[( |x|X)\]/, box.checked ? "[x]" : "[ ]");
      current.md = lines.join("\n");
      input.value = current.md;
      renderPreview();
    });

    prioButtons.forEach((button) =>
      button.addEventListener("click", () => {
        current.prio = button.dataset.prio;
        renderMeta();
        renderList();
      }),
    );

    dueInput.addEventListener("change", () => {
      current.due = dueInput.value;
      publish();
    });

    // Mini mode: FLIP the window into a small always-on-top note.
    miniButton.addEventListener("click", () => {
      const before = root.getBoundingClientRect();
      const mini = root.classList.toggle("is-mini");
      miniButton.textContent = mini ? "Teljes nézet" : "Mini ablak";
      if (prefersReducedMotion()) return;
      const after = root.getBoundingClientRect();
      anime.remove(root);
      anime({
        targets: root,
        translateX: [before.left - after.left, 0],
        translateY: [before.top - after.top, 0],
        scaleX: [before.width / after.width, 1],
        scaleY: [before.height / after.height, 1],
        duration: 520,
        easing: "easeOutCubic",
        begin: () => (root.style.transformOrigin = "top left"),
        complete: () => (root.style.transform = ""),
      });
    });

    select(current);
    publish();
  }

  Object.assign(window.OpenSite, { renderMarkdown, initNotesDemo });
})();
