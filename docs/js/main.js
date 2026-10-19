(() => {
  "use strict";
  const { LINKS, prefersReducedMotion, initNotesDemo, initCalendarDemo, initKanbanDemo, initLauncherDemo, initHero } = window.OpenSite;
  const reduced = prefersReducedMotion();

  // ── Links (GitHub repo is detected from the Pages URL) ─────────
  document.querySelectorAll("[data-link]").forEach((a) => {
    const href = LINKS[a.dataset.link];
    if (!href) return;
    a.href = href;
    if (href.startsWith("http")) a.rel = "noopener";
  });
  document.querySelectorAll('[data-text="clone"]').forEach((el) => (el.textContent = LINKS.clone));

  // ── Copy buttons ───────────────────────────────────────────────
  document.querySelectorAll("[data-copy]").forEach((button) => {
    button.addEventListener("click", async () => {
      const text = document.querySelector(button.dataset.copy)?.textContent.trim() ?? "";
      try {
        await navigator.clipboard.writeText(text);
        button.textContent = "Másolva";
      } catch {
        button.textContent = "Jelöld ki és másold";
      }
      setTimeout(() => (button.textContent = "Másolás"), 1800);
    });
  });

  // ── Demos ──────────────────────────────────────────────────────
  // Kanban and notes publish deadlines, so the calendar subscribes first.
  initCalendarDemo(document.getElementById("calendar-demo"));
  initNotesDemo(document.getElementById("notes-demo"));
  initKanbanDemo(document.getElementById("kanban-demo"));
  initLauncherDemo(document.getElementById("launcher-demo"));

  // ── Accent follows the app you are reading about ───────────────
  const sections = [...document.querySelectorAll("[data-accent]")];
  const navLinks = new Map([...document.querySelectorAll(".nav a[href^='#']")].map((a) => [a.getAttribute("href").slice(1), a]));
  const root = document.documentElement;

  const accentObserver = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        root.style.setProperty("--accent", entry.target.dataset.accent);
        navLinks.forEach((a, id) => a.classList.toggle("is-current", id === entry.target.id));
      }
    },
    { rootMargin: "-45% 0px -45% 0px" },
  );
  sections.forEach((s) => accentObserver.observe(s));

  // Back at the top or past the apps: neutral accent and no current link.
  const resetObserver = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        root.style.removeProperty("--accent");
        navLinks.forEach((a) => a.classList.remove("is-current"));
      }
    },
    { rootMargin: "-45% 0px -45% 0px" },
  );
  document.querySelectorAll(".hero, .links, .download").forEach((s) => resetObserver.observe(s));

  // Reading progress on the top bar's accent rail.
  const topbar = document.querySelector(".topbar");
  const updateProgress = () => {
    const max = document.documentElement.scrollHeight - innerHeight;
    topbar.style.setProperty("--progress", `${max > 0 ? (scrollY / max) * 100 : 0}%`);
  };
  addEventListener("scroll", updateProgress, { passive: true });
  updateProgress();

  // ── Hero: the one orchestrated entrance ────────────────────────
  if (!reduced) {
    anime
      .timeline({ easing: "easeOutExpo", begin: () => root.classList.remove("intro") })
      .add({ targets: ".hero-title .line", translateY: [48, 0], opacity: [0, 1], duration: 1100, delay: anime.stagger(110) })
      .add({ targets: ".hero-lead, .hero-actions, .hero-meta", translateY: [16, 0], opacity: [0, 1], duration: 900, delay: anime.stagger(90) }, "-=800")
      .add({ targets: ".hero-legend li", opacity: [0, 1], duration: 700, delay: anime.stagger(70) }, "-=600");
  }

  const canvas = document.getElementById("hero-canvas");
  let hero = null;
  try {
    hero = window.THREE
      ? initHero(canvas, {
          onTileClick: (def) => document.querySelector(def.target)?.scrollIntoView({ behavior: reduced ? "auto" : "smooth" }),
        })
      : null;
  } catch (error) {
    console.warn("3D hero disabled:", error);
  }
  if (!hero) {
    showStaticHero();
  } else {
    document.querySelectorAll(".hero-legend a").forEach((a) => {
      const id = a.getAttribute("href").slice(1);
      a.addEventListener("pointerenter", () => hero.highlight(id));
      a.addEventListener("focus", () => hero.highlight(id));
      a.addEventListener("pointerleave", () => hero.highlight(null));
      a.addEventListener("blur", () => hero.highlight(null));
    });
  }

  /** Without WebGL: the four icons in a simple constellation. */
  function showStaticHero() {
    canvas.remove();
    const stage = document.querySelector(".hero-stage");
    const spots = [
      ["openNotes", "18%", "48%"],
      ["openCalendar", "50%", "24%"],
      ["openKanban", "82%", "48%"],
      ["openLauncher", "50%", "72%"],
    ];
    for (const [name, x, y] of spots) {
      const img = document.createElement("img");
      img.src = `assets/icons/${name}.png`;
      img.alt = name;
      img.style.cssText = `position:absolute;left:${x};top:${y};width:22%;transform:translate(-50%,-50%);filter:drop-shadow(0 10px 24px rgba(32,30,38,.15))`;
      stage.appendChild(img);
    }
  }

  // ── Connection map: draw the threads once, when it comes into view ──
  const flows = document.querySelectorAll(".links-svg .flow");
  if (!reduced) {
    flows.forEach((p) => (p.style.opacity = 0));
    const mapObserver = new IntersectionObserver(
      ([entry]) => {
        if (!entry.isIntersecting) return;
        mapObserver.disconnect();
        anime({
          targets: flows,
          strokeDashoffset: [anime.setDashoffset, 0],
          opacity: [0, 1],
          duration: 1300,
          delay: anime.stagger(160),
          easing: "easeInOutSine",
          complete: () => flows.forEach((p) => p.removeAttribute("stroke-dasharray")),
        });
      },
      { threshold: 0.4 },
    );
    mapObserver.observe(document.querySelector(".links-map"));
  }
})();
