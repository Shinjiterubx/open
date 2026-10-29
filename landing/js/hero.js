/**
 * Hero scene: the four app tiles floating above a dotted desk, joined by the threads that
 * really connect them (deadlines into the calendar, card → note links, the launcher starting all).
 * Light pulses travel along the threads in the direction the data flows.
 */
(() => {
  "use strict";
  const { prefersReducedMotion } = window.OpenSite;
  const THREE = window.THREE;

  const TILES = [
    { id: "notes", icon: "assets/icons/openNotes.png", color: "#5B3E96", pos: [-3.05, 0.15, 0.1], target: "#notes" },
    { id: "calendar", icon: "assets/icons/openCalendar.png", color: "#D45D79", pos: [0, 1.75, -0.9], target: "#calendar" },
    { id: "kanban", icon: "assets/icons/openKanban.png", color: "#2F5A8A", pos: [3.05, 0.35, 0.1], target: "#kanban" },
    { id: "launcher", icon: "assets/icons/openLauncher.png", color: "#D2691E", pos: [0, -1.85, 0.7], target: "#launcher" },
  ];

  /** [from, to] – the real integrations of the suite. */
  const LINKS = [
    ["notes", "calendar"],
    ["kanban", "calendar"],
    ["kanban", "notes"],
    ["launcher", "notes"],
    ["launcher", "calendar"],
    ["launcher", "kanban"],
  ];

  const FLOOR_Y = -3.4;

  function initHero(canvas, { onTileClick } = {}) {
    let renderer;
    try {
      renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, powerPreference: "high-performance" });
    } catch {
      return null; // no WebGL – caller shows the static fallback
    }

    const reduced = prefersReducedMotion();
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.05;

    const scene = new THREE.Scene();
    scene.fog = new THREE.Fog(0xffffff, 13, 30);

    const camera = new THREE.PerspectiveCamera(34, 1, 0.1, 100);
    camera.position.set(0, 0.9, 13.6);
    camera.lookAt(0, -0.45, 0);

    // Lights: soft sky + a warm key from the upper left.
    scene.add(new THREE.HemisphereLight(0xffffff, 0xd9d7e2, 1.6));
    const key = new THREE.DirectionalLight(0xffffff, 2.2);
    key.position.set(-4, 6, 8);
    scene.add(key);
    const rim = new THREE.DirectionalLight(0xffffff, 0.8);
    rim.position.set(5, -2, -6);
    scene.add(rim);

    const world = new THREE.Group();
    scene.add(world);

    // ── Dotted desk ──────────────────────────────────────────────
    {
      const positions = [];
      for (let x = -22; x <= 22; x += 0.55) {
        for (let z = -26; z <= 6; z += 0.55) positions.push(x, FLOOR_Y, z);
      }
      const geometry = new THREE.BufferGeometry();
      geometry.setAttribute("position", new THREE.Float32BufferAttribute(positions, 3));
      const dots = new THREE.Points(geometry, new THREE.PointsMaterial({ color: 0xb9b6c4, size: 0.035, sizeAttenuation: true }));
      world.add(dots);
    }

    // ── Tiles ────────────────────────────────────────────────────
    const loader = new THREE.TextureLoader();
    const tileGeometry = roundedTileGeometry(2.1, 0.34, 0.36);
    const faceGeometry = new THREE.PlaneGeometry(2.02, 2.02);
    const shadowTexture = radialTexture("rgba(32,30,38,0.30)", "rgba(32,30,38,0)");
    const tiles = new Map();

    for (const def of TILES) {
      const color = new THREE.Color(def.color);
      const group = new THREE.Group();
      group.position.set(...def.pos);
      group.userData = { def, base: new THREE.Vector3(...def.pos), hover: 0, phase: Math.random() * Math.PI * 2 };

      const body = new THREE.Mesh(
        tileGeometry,
        new THREE.MeshPhysicalMaterial({
          color: new THREE.Color(0xffffff).lerp(color, 0.18),
          roughness: 0.32,
          metalness: 0,
          clearcoat: 0.6,
          clearcoatRoughness: 0.25,
        }),
      );
      group.add(body);

      const texture = loader.load(window.OpenSite.iconData?.[def.id] ?? def.icon);
      texture.colorSpace = THREE.SRGBColorSpace;
      texture.anisotropy = renderer.capabilities.getMaxAnisotropy();
      const face = new THREE.Mesh(faceGeometry, new THREE.MeshBasicMaterial({ map: texture, transparent: true, toneMapped: false }));
      face.position.z = 0.172;
      group.add(face);

      const shadow = new THREE.Mesh(
        new THREE.PlaneGeometry(2.6, 2.6),
        new THREE.MeshBasicMaterial({ map: shadowTexture, transparent: true, depthWrite: false }),
      );
      shadow.rotation.x = -Math.PI / 2;
      shadow.position.set(def.pos[0], FLOOR_Y + 0.01, def.pos[2]);
      world.add(shadow);
      group.userData.shadow = shadow;

      body.userData.tile = group;
      face.userData.tile = group;
      world.add(group);
      tiles.set(def.id, group);
    }

    // ── Threads + pulses ─────────────────────────────────────────
    const glowTexture = radialTexture("rgba(255,255,255,1)", "rgba(255,255,255,0)");
    const threads = LINKS.map(([from, to], index) => {
      const a = tiles.get(from).userData.base;
      const b = tiles.get(to).userData.base;
      const mid = a.clone().add(b).multiplyScalar(0.5);
      mid.z += 1.6;
      mid.y += from === "launcher" ? -0.2 : 0.55;
      const curve = new THREE.QuadraticBezierCurve3(a.clone(), mid, b.clone());
      const color = new THREE.Color(TILES.find((t) => t.id === from).color);

      const tube = new THREE.Mesh(
        new THREE.TubeGeometry(curve, 64, 0.014, 6, false),
        new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.42, depthWrite: false }),
      );
      world.add(tube);

      const pulses = [0, 0.5].map((offset) => {
        const sprite = new THREE.Sprite(
          new THREE.SpriteMaterial({ map: glowTexture, color, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending }),
        );
        sprite.scale.setScalar(0.42);
        const core = new THREE.Mesh(new THREE.SphereGeometry(0.045, 12, 12), new THREE.MeshBasicMaterial({ color }));
        world.add(sprite, core);
        return { sprite, core, offset };
      });

      return { curve, tube, pulses, speed: 0.11 + (index % 3) * 0.025, from, to };
    });

    // ── Interaction ──────────────────────────────────────────────
    const pointer = new THREE.Vector2(0, 0);
    const pointerTarget = new THREE.Vector2(0, 0);
    const raycaster = new THREE.Raycaster();
    const pickables = [...tiles.values()].flatMap((g) => g.children);
    let hovered = null;
    let highlighted = null; // from the legend

    function setPointer(event) {
      const rect = canvas.getBoundingClientRect();
      pointerTarget.set(((event.clientX - rect.left) / rect.width) * 2 - 1, -((event.clientY - rect.top) / rect.height) * 2 + 1);
    }

    function pick() {
      raycaster.setFromCamera(pointerTarget, camera);
      const hit = raycaster.intersectObjects(pickables, false)[0];
      return hit?.object.userData.tile ?? null;
    }

    canvas.addEventListener("pointermove", (event) => {
      setPointer(event);
      hovered = pick();
      canvas.style.cursor = hovered ? "pointer" : "default";
    });
    canvas.addEventListener("pointerleave", () => {
      pointerTarget.set(0, 0);
      hovered = null;
      canvas.style.cursor = "default";
    });
    canvas.addEventListener("click", (event) => {
      setPointer(event);
      const tile = pick();
      if (tile && onTileClick) onTileClick(tile.userData.def);
    });

    // ── Size / visibility ────────────────────────────────────────
    function resize() {
      const { clientWidth: w, clientHeight: h } = canvas.parentElement;
      if (!w || !h) return;
      renderer.setSize(w, h, false);
      camera.aspect = w / h;
      // Keep all four tiles in frame on narrow screens.
      camera.position.z = w / h < 1.05 ? 16.5 : 13.6;
      camera.updateProjectionMatrix();
      if (reduced) render(0);
    }
    new ResizeObserver(resize).observe(canvas.parentElement);

    let visible = true;
    new IntersectionObserver(([entry]) => {
      visible = entry.isIntersecting;
      if (visible && !reduced) loop();
    }).observe(canvas);
    document.addEventListener("visibilitychange", () => {
      if (!document.hidden && visible && !reduced) loop();
    });

    // ── Frame ────────────────────────────────────────────────────
    const clock = new THREE.Clock();
    let running = false;
    const intro = { t: reduced ? 1 : 0 }; // 0 → 1, driven by anime.js

    function render(time) {
      pointer.lerp(pointerTarget, 0.06);
      world.rotation.y = pointer.x * 0.22;
      world.rotation.x = -pointer.y * 0.08;

      for (const group of tiles.values()) {
        const { base, phase } = group.userData;
        const isFocus = group === hovered || group.userData.def.id === highlighted;
        group.userData.hover += ((isFocus ? 1 : 0) - group.userData.hover) * 0.12;

        const bob = reduced ? 0 : Math.sin(time * 0.9 + phase) * 0.12;
        const enter = easeOutBack(clamp01(intro.t * 1.6 - TILES.indexOf(group.userData.def) * 0.18));
        group.position.set(base.x, base.y + bob + (1 - enter) * -2.2, base.z + group.userData.hover * 0.5);
        group.scale.setScalar(Math.max(0.001, enter) * (1 + group.userData.hover * 0.08));
        group.rotation.y = Math.sin(time * 0.5 + phase) * 0.12 - pointer.x * 0.25;
        group.rotation.x = Math.cos(time * 0.4 + phase) * 0.06 + pointer.y * 0.12;

        const shadow = group.userData.shadow;
        const height = group.position.y - FLOOR_Y;
        shadow.scale.setScalar((0.85 + height * 0.08) * Math.max(0.001, enter));
        shadow.material.opacity = Math.max(0, 0.6 - height * 0.08) * enter;
      }

      const threadAlpha = clamp01(intro.t * 2 - 1);
      for (const thread of threads) {
        const focus = highlighted && (thread.from === highlighted || thread.to === highlighted);
        thread.tube.material.opacity = threadAlpha * (highlighted ? (focus ? 0.8 : 0.12) : 0.42);
        for (const pulse of thread.pulses) {
          const t = reduced ? pulse.offset + 0.25 : (time * thread.speed + pulse.offset) % 1;
          const point = thread.curve.getPoint(t);
          pulse.sprite.position.copy(point);
          pulse.core.position.copy(point);
          const fade = Math.sin(t * Math.PI) * threadAlpha * (highlighted && !focus ? 0.2 : 1);
          pulse.sprite.material.opacity = fade * 0.9;
          pulse.core.scale.setScalar(Math.max(0.001, fade));
        }
      }

      renderer.render(scene, camera);
    }

    function loop() {
      if (running) return;
      running = true;
      const tick = () => {
        if (!visible || document.hidden) {
          running = false;
          return;
        }
        render(clock.getElapsedTime());
        requestAnimationFrame(tick);
      };
      requestAnimationFrame(tick);
    }

    resize();
    if (reduced) {
      render(0);
    } else {
      anime({ targets: intro, t: [0, 1], duration: 2200, easing: "easeOutQuart", delay: 250 });
      loop();
    }

    return {
      /** Highlight one app's tile and its threads (legend hover). */
      highlight(id) {
        highlighted = id;
        if (reduced) render(0);
      },
    };
  }

  // ── helpers ────────────────────────────────────────────────────

  /** Rounded square slab (an app icon tile), centred on the origin, front face at +depth/2. */
  function roundedTileGeometry(size, depth, radius) {
    const bevel = 0.06;
    const half = size / 2 - bevel;
    const r = radius - bevel;
    const shape = new THREE.Shape();
    shape.moveTo(-half + r, -half);
    shape.lineTo(half - r, -half);
    shape.quadraticCurveTo(half, -half, half, -half + r);
    shape.lineTo(half, half - r);
    shape.quadraticCurveTo(half, half, half - r, half);
    shape.lineTo(-half + r, half);
    shape.quadraticCurveTo(-half, half, -half, half - r);
    shape.lineTo(-half, -half + r);
    shape.quadraticCurveTo(-half, -half, -half + r, -half);

    const geometry = new THREE.ExtrudeGeometry(shape, {
      depth: depth - bevel * 2,
      bevelEnabled: true,
      bevelThickness: bevel,
      bevelSize: bevel,
      bevelSegments: 4,
      curveSegments: 12,
    });
    geometry.translate(0, 0, -(depth - bevel * 2) / 2);
    geometry.computeVertexNormals();
    return geometry;
  }

  function radialTexture(inner, outer) {
    const size = 128;
    const canvas = document.createElement("canvas");
    canvas.width = canvas.height = size;
    const ctx = canvas.getContext("2d");
    const gradient = ctx.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
    gradient.addColorStop(0, inner);
    gradient.addColorStop(1, outer);
    ctx.fillStyle = gradient;
    ctx.fillRect(0, 0, size, size);
    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    return texture;
  }

  function clamp01(v) {
    return Math.min(1, Math.max(0, v));
  }

  function easeOutBack(x) {
    const c1 = 1.4;
    const c3 = c1 + 1;
    return x <= 0 ? 0 : x >= 1 ? 1 : 1 + c3 * Math.pow(x - 1, 3) + c1 * Math.pow(x - 1, 2);
  }

  Object.assign(window.OpenSite, { initHero });
})();
