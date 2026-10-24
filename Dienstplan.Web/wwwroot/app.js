(() => {
    'use strict';

    const COLORS = ['sun', 'coral', 'night', 'mint', 'sky', 'plum', 'rose', 'slate'];
    const DEFAULT_SHIFTS = [
        { id: 'f', code: 'F', name: 'Frühdienst', color: 'sun' },
        { id: 's', code: 'S', name: 'Spätdienst', color: 'coral' },
        { id: 'n', code: 'N', name: 'Nachtdienst', color: 'night' },
    ];
    const ERASER = 'erase';
    const KEYS = { shifts: 'dienstplan.shifts', plan: 'dienstplan.plan', theme: 'dienstplan.theme' };
    const MONTHS = ['Januar', 'Februar', 'März', 'April', 'Mai', 'Juni', 'Juli', 'August', 'September', 'Oktober', 'November', 'Dezember'];

    const $ = (id) => document.getElementById(id);
    const el = (tag, props = {}, children = []) => {
        const node = Object.assign(document.createElement(tag), props);
        for (const child of [].concat(children)) node.append(child);
        return node;
    };

    // Storage may be unavailable (private mode etc.) - the app must still work without it.
    const load = (key, fallback) => {
        try { const raw = localStorage.getItem(key); return raw ? JSON.parse(raw) : fallback; } catch { return fallback; }
    };
    const save = (key, value) => {
        try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* ignore */ }
    };

    const clone = (value) => JSON.parse(JSON.stringify(value));
    let shifts = load(KEYS.shifts, null);
    if (!Array.isArray(shifts) || shifts.length === 0) shifts = clone(DEFAULT_SHIFTS);
    let plan = load(KEYS.plan, {});
    if (typeof plan !== 'object' || plan === null) plan = {};

    const today = new Date();
    let year = today.getFullYear();
    let month = today.getMonth();
    let selected = shifts[0].id;

    const pad = (n) => String(n).padStart(2, '0');
    const dateKey = (y, m, d) => `${y}-${pad(m + 1)}-${pad(d)}`;
    const monthPrefix = () => `${year}-${pad(month + 1)}-`;
    const shiftById = (id) => shifts.find((s) => s.id === id);
    const label = (s) => s.name.trim() || s.code.trim() || 'Dienst';

    // ---------- Calendar ----------
    function renderGrid(popKey) {
        $('month-label').textContent = `${MONTHS[month]} ${year}`;
        const grid = $('grid');
        grid.replaceChildren();

        const offset = (new Date(year, month, 1).getDay() + 6) % 7; // Monday first
        for (let i = 0; i < offset; i++) grid.append(el('div', { className: 'spacer' }));

        const days = new Date(year, month + 1, 0).getDate();
        const todayKey = dateKey(today.getFullYear(), today.getMonth(), today.getDate());
        for (let d = 1; d <= days; d++) {
            const key = dateKey(year, month, d);
            const weekday = (offset + d - 1) % 7;
            const shift = shiftById(plan[key]);
            const button = el('button', { type: 'button', className: 'day' }, [
                el('span', { className: 'num', textContent: d }),
                el('span', { className: 'code', textContent: shift ? shift.code : '' }),
            ]);
            button.dataset.date = key;
            if (weekday >= 5) button.classList.add('weekend');
            if (key === todayKey) button.classList.add('today');
            if (key === popKey) button.classList.add('pop');
            if (shift) {
                button.classList.add('filled');
                button.style.setProperty('--c', `var(--${shift.color})`);
            }
            button.setAttribute('aria-label', `${d}. ${MONTHS[month]}: ${shift ? label(shift) : 'kein Dienst'}`);
            grid.append(button);
        }

        renderSummary();
        updateExport();
    }

    function renderSummary() {
        const counts = new Map();
        for (const [key, id] of Object.entries(plan)) {
            if (key.startsWith(monthPrefix()) && shiftById(id)) counts.set(id, (counts.get(id) || 0) + 1);
        }
        const summary = $('summary');
        summary.replaceChildren();
        if (counts.size === 0) {
            summary.textContent = 'Noch keine Dienste eingetragen';
            $('clear-month').hidden = true;
            return;
        }
        $('clear-month').hidden = false;
        for (const shift of shifts) {
            if (!counts.has(shift.id)) continue;
            const dot = el('span', { className: 'dot' });
            const pill = el('span', { className: 'pill' }, [dot, `${counts.get(shift.id)}× ${shift.code}`]);
            pill.style.setProperty('--c', `var(--${shift.color})`);
            pill.title = label(shift);
            summary.append(pill);
        }
    }

    $('grid').addEventListener('click', (event) => {
        const button = event.target.closest('.day');
        if (!button) return;
        const key = button.dataset.date;
        // Tapping a day that already has the selected shift clears it again.
        if (selected === ERASER || plan[key] === selected) delete plan[key];
        else plan[key] = selected;
        save(KEYS.plan, plan);
        renderGrid(key);
    });

    function changeMonth(delta) {
        const date = new Date(year, month + delta, 1);
        year = date.getFullYear();
        month = date.getMonth();
        renderGrid();
    }
    $('prev-month').addEventListener('click', () => changeMonth(-1));
    $('next-month').addEventListener('click', () => changeMonth(1));

    $('clear-month').addEventListener('click', () => {
        if (!confirm(`Alle Dienste im ${MONTHS[month]} ${year} entfernen?`)) return;
        for (const key of Object.keys(plan)) if (key.startsWith(monthPrefix())) delete plan[key];
        save(KEYS.plan, plan);
        renderGrid();
    });

    // ---------- Palette ----------
    function renderPalette() {
        const palette = $('palette');
        palette.replaceChildren();
        const items = [...shifts, { id: ERASER, code: '–', name: 'Frei', color: 'slate' }];
        for (const shift of items) {
            const chip = el('button', { type: 'button', className: 'chip' }, [
                el('span', { className: 'badge', textContent: shift.code }),
                el('b', { textContent: shift.id === ERASER ? 'Frei' : label(shift) }),
            ]);
            if (shift.id === ERASER) chip.title = 'Eintrag löschen';
            chip.dataset.id = shift.id;
            chip.setAttribute('role', 'radio');
            chip.setAttribute('aria-checked', String(shift.id === selected));
            chip.style.setProperty('--c', `var(--${shift.color})`);
            if (shift.id === ERASER) chip.classList.add('eraser');
            palette.append(chip);
        }
        updatePaletteFades();
    }

    // Fade the edges only where more chips are hidden, so it reads as "swipe for more".
    function updatePaletteFades() {
        const palette = $('palette');
        const max = palette.scrollWidth - palette.clientWidth;
        palette.classList.toggle('more-left', palette.scrollLeft > 2);
        palette.classList.toggle('more-right', palette.scrollLeft < max - 2);
    }

    $('palette').addEventListener('scroll', updatePaletteFades, { passive: true });
    window.addEventListener('resize', updatePaletteFades);

    // Mouse wheel scrolls the chips sideways (desktop); touch devices swipe natively.
    $('palette').addEventListener('wheel', (event) => {
        const palette = $('palette');
        if (palette.scrollWidth <= palette.clientWidth || Math.abs(event.deltaX) > Math.abs(event.deltaY)) return;
        event.preventDefault();
        palette.scrollBy({ left: event.deltaY });
    }, { passive: false });

    $('palette').addEventListener('click', (event) => {
        const chip = event.target.closest('.chip');
        if (!chip) return;
        selected = chip.dataset.id;
        for (const other of $('palette').children) other.setAttribute('aria-checked', String(other === chip));
        // Pull a half-hidden chip fully into view once it's picked.
        chip.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
    });

    // ---------- Export ----------
    // The plan travels in the URL so the server can answer a plain GET with text/calendar.
    // That real navigation is what makes iOS show "Alle hinzufügen" instead of a silent download.
    function exportUrl() {
        const used = [];
        const index = new Map();
        const days = {};
        for (const [key, id] of Object.entries(plan)) {
            const shift = shiftById(id);
            if (!key.startsWith(monthPrefix()) || !shift) continue;
            if (!index.has(id)) {
                index.set(id, used.length);
                used.push(label(shift));
            }
            days[key] = index.get(id);
        }
        if (used.length === 0) return null;

        const bytes = new TextEncoder().encode(JSON.stringify({ s: used, d: days }));
        let binary = '';
        bytes.forEach((b) => { binary += String.fromCharCode(b); });
        const encoded = btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
        return { url: `dienstplan.ics?p=${encoded}`, count: Object.keys(days).length };
    }

    function updateExport() {
        const link = $('export');
        const result = exportUrl();
        if (result) {
            link.href = result.url;
            link.setAttribute('aria-disabled', 'false');
            $('export-label').textContent = `${result.count} ${result.count === 1 ? 'Dienst' : 'Dienste'} in den Kalender`;
        } else {
            link.href = '#';
            link.setAttribute('aria-disabled', 'true');
            $('export-label').textContent = 'In den Kalender';
        }
    }

    const isApple = /iPhone|iPad|iPod|Macintosh/.test(navigator.userAgent) && 'ontouchend' in document;
    $('export').addEventListener('click', (event) => {
        if ($('export').getAttribute('aria-disabled') === 'true') { event.preventDefault(); return; }
        toast(isApple ? 'Gleich „Alle hinzufügen“ antippen 👍' : 'Kalenderdatei wird geöffnet …');
    });

    let toastTimer;
    function toast(message) {
        const node = $('toast');
        node.textContent = message;
        node.classList.add('show');
        clearTimeout(toastTimer);
        toastTimer = setTimeout(() => node.classList.remove('show'), 3200);
    }

    // ---------- Settings ----------
    const dialog = $('settings');

    function field(labelText, className, input) {
        return el('label', { className: `field ${className}` }, [el('span', { textContent: labelText }), input]);
    }

    function renderShiftList() {
        const list = $('shift-list');
        list.replaceChildren();
        for (const shift of shifts) {
            const row = el('div', { className: 'shift-row' });
            row.style.setProperty('--c', `var(--${shift.color})`);

            const swatch = el('button', { type: 'button', className: 'swatch', title: 'Farbe ändern' });
            swatch.setAttribute('aria-label', `Farbe für ${label(shift)} ändern`);
            swatch.addEventListener('click', () => {
                shift.color = COLORS[(COLORS.indexOf(shift.color) + 1) % COLORS.length];
                row.style.setProperty('--c', `var(--${shift.color})`);
                persistShifts();
            });

            const code = el('input', { value: shift.code, maxLength: 3, autocapitalize: 'characters', autocomplete: 'off' });
            code.addEventListener('input', () => { shift.code = code.value.trim().toUpperCase(); persistShifts(); });

            const name = el('input', { value: shift.name, maxLength: 40, autocomplete: 'off', placeholder: 'z. B. Zwischendienst' });
            name.addEventListener('input', () => { shift.name = name.value; persistShifts(); });

            const remove = el('button', { type: 'button', className: 'delete', title: 'Dienst löschen', innerHTML:
                '<svg viewBox="0 0 24 24" width="19" height="19" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 7h16M10 11v6M14 11v6M6 7l1 12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-12M9 7V4h6v3"/></svg>' });
            remove.setAttribute('aria-label', `${label(shift)} löschen`);
            remove.addEventListener('click', () => deleteShift(shift));

            row.append(
                swatch,
                field('Kürzel', 'code', code),
                field('Bezeichnung', 'name', name),
                remove,
            );
            list.append(row);
        }
    }

    function persistShifts() {
        for (const shift of shifts) if (!shift.code) shift.code = (shift.name.trim()[0] || '?').toUpperCase();
        save(KEYS.shifts, shifts);
        renderPalette();
        renderGrid();
    }

    function deleteShift(shift) {
        if (shifts.length === 1) { toast('Mindestens ein Dienst wird benötigt.'); return; }
        const used = Object.values(plan).filter((id) => id === shift.id).length;
        if (used > 0 && !confirm(`„${label(shift)}“ ist ${used}× eingetragen. Trotzdem löschen?`)) return;
        shifts = shifts.filter((s) => s !== shift);
        for (const [key, id] of Object.entries(plan)) if (id === shift.id) delete plan[key];
        save(KEYS.plan, plan);
        if (selected === shift.id) selected = shifts[0].id;
        persistShifts();
        renderShiftList();
    }

    $('add-shift').addEventListener('click', () => {
        const color = COLORS.find((c) => !shifts.some((s) => s.color === c)) || 'slate';
        shifts.push({ id: `c${Date.now().toString(36)}`, code: 'Z', name: '', color });
        persistShifts();
        renderShiftList();
        $('shift-list').lastElementChild.querySelector('.name input').focus();
    });

    $('reset-shifts').addEventListener('click', () => {
        if (!confirm('Dienste auf Früh/Spät/Nacht zurücksetzen? Eingetragene Tage mit eigenen Diensten werden entfernt.')) return;
        shifts = clone(DEFAULT_SHIFTS);
        for (const [key, id] of Object.entries(plan)) if (!shiftById(id)) delete plan[key];
        save(KEYS.plan, plan);
        selected = shifts[0].id;
        persistShifts();
        renderShiftList();
    });

    $('open-settings').addEventListener('click', () => {
        renderShiftList();
        dialog.showModal();
    });
    // Tap on the backdrop closes the sheet.
    dialog.addEventListener('click', (event) => { if (event.target === dialog) dialog.close(); });

    // ---------- Theme ----------
    const root = document.documentElement;
    const themeMeta = document.querySelector('meta[name="theme-color"]');
    const systemDark = matchMedia('(prefers-color-scheme: dark)');

    function applyTheme(theme) {
        root.dataset.theme = theme;
        themeMeta.content = theme === 'dark' ? '#0e1014' : '#f6f3ee';
    }

    $('theme-toggle').addEventListener('click', (event) => {
        const next = root.dataset.theme === 'dark' ? 'light' : 'dark';
        // Stored as plain string: the inline script in App.razor reads it before first paint.
        try { localStorage.setItem(KEYS.theme, next); } catch { /* ignore */ }
        const swap = () => applyTheme(next);

        if (!document.startViewTransition || matchMedia('(prefers-reduced-motion: reduce)').matches) { swap(); return; }

        // Reveal the new theme as a circle growing out of the toggle button.
        const rect = event.currentTarget.getBoundingClientRect();
        const x = rect.left + rect.width / 2;
        const y = rect.top + rect.height / 2;
        const radius = Math.hypot(Math.max(x, innerWidth - x), Math.max(y, innerHeight - y));
        document.startViewTransition(swap).ready.then(() => {
            root.animate(
                { clipPath: [`circle(0px at ${x}px ${y}px)`, `circle(${radius}px at ${x}px ${y}px)`] },
                { duration: 550, easing: 'cubic-bezier(.2, .8, .2, 1)', pseudoElement: '::view-transition-new(root)' },
            );
        });
    });

    // Follow the system setting until the user picks a theme explicitly.
    systemDark.addEventListener('change', (event) => {
        let stored = null;
        try { stored = localStorage.getItem(KEYS.theme); } catch { /* ignore */ }
        if (!stored) applyTheme(event.matches ? 'dark' : 'light');
    });

    applyTheme(root.dataset.theme === 'dark' ? 'dark' : 'light');
    renderPalette();
    renderGrid();
})();
