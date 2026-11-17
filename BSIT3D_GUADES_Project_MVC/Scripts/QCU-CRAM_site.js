/* QCU-CRAM shared client script
   - Show/hide eye on every password input
   - Dashed student-number formatting (23-0001)
   - Live password-strength meter + confirm-match hint
   No external libraries; icons are inline SVG. */
(function () {
    'use strict';

    /* ------------------------------------------------------------------
       1. Eye icon on every <input type="password">
       ------------------------------------------------------------------ */
    var SVG_OPEN = '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">';
    var ICON_EYE = SVG_OPEN + '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>';
    var ICON_EYE_OFF = SVG_OPEN + '<path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>';

    function wrapPassword(input) {
        if (input.getAttribute('data-qcu-pw') === '1') return;
        input.setAttribute('data-qcu-pw', '1');

        var wrap = document.createElement('div');
        wrap.className = 'qcu-pw-wrap';
        input.parentNode.insertBefore(wrap, input);
        wrap.appendChild(input);

        var btn = document.createElement('button');
        btn.type = 'button';                       // never submits the form / fires validators
        btn.className = 'qcu-pw-toggle';
        btn.setAttribute('aria-label', 'Show password');
        btn.setAttribute('aria-pressed', 'false');
        btn.title = 'Show password';
        btn.innerHTML = ICON_EYE;

        // keep focus in the text box when the eye is clicked with the mouse
        btn.addEventListener('mousedown', function (e) { e.preventDefault(); });
        btn.addEventListener('click', function () {
            var show = (input.type === 'password');
            input.type = show ? 'text' : 'password';
            btn.innerHTML = show ? ICON_EYE_OFF : ICON_EYE;
            btn.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
            btn.setAttribute('aria-pressed', show ? 'true' : 'false');
            btn.title = show ? 'Hide password' : 'Show password';
            try { input.focus(); var n = input.value.length; input.setSelectionRange(n, n); } catch (x) { }
        });
        wrap.appendChild(btn);
    }

    function initPasswordToggles(root) {
        var list = (root || document).querySelectorAll('input[type="password"]');
        for (var i = 0; i < list.length; i++) wrapPassword(list[i]);
    }

    /* ------------------------------------------------------------------
       2. Dashed student number  (230001 -> 23-0001)
       ------------------------------------------------------------------ */
    // Auto-dash only while the text is purely digits (faculty IDs with letters are left alone).
    function formatDashed(v) {
        var d = v.replace(/-/g, '');
        if (d === '') return '';
        if (!/^\d+$/.test(d)) return v;
        d = d.slice(0, 6);
        return d.length > 2 ? d.slice(0, 2) + '-' + d.slice(2) : d;
    }

    /* opts.enabled()    -> true when auto-dash should run
       opts.digitsOnly() -> true when everything except digits/dash must be stripped */
    function attachDash(input, opts) {
        opts = opts || {};
        input.addEventListener('input', function () {
            var v = input.value;
            var caret = input.selectionStart;
            var clean = v;
            if (opts.digitsOnly && opts.digitsOnly()) clean = v.replace(/[^\d-]/g, '');
            var out = (opts.enabled && opts.enabled()) ? formatDashed(clean) : clean;
            if (out === v) return;

            var digitsBefore = (v.slice(0, caret === null ? v.length : caret).match(/\d/g) || []).length;
            input.value = out;
            var pos = 0, seen = 0;
            while (pos < out.length && seen < digitsBefore) { if (/\d/.test(out.charAt(pos))) seen++; pos++; }
            try { input.setSelectionRange(pos, pos); } catch (x) { }
        });
    }

    /* ------------------------------------------------------------------
       3. Password strength
       ------------------------------------------------------------------ */
    var COMMON = ['password', '12345678', '123456789', 'qwerty', 'qwerty123', 'admin', 'admin123', 'letmein',
        'welcome', 'iloveyou', 'abc123', 'monkey', 'dragon', 'football', 'pass1234', 'test1234'];

    function joinList(a) {
        if (a.length <= 1) return a.join('');
        if (a.length === 2) return a[0] + ' and ' + a[1];
        return a.slice(0, -1).join(', ') + ', and ' + a[a.length - 1];
    }

    // tokens: lower-case words that must not appear inside the password (name, ID, email name)
    function evaluate(pw, tokens) {
        pw = pw || '';
        var low = pw.toLowerCase();
        var r = {
            len: pw.length >= 8,
            upper: /[A-Z]/.test(pw),
            lower: /[a-z]/.test(pw),
            digit: /[0-9]/.test(pw),
            symbol: /[^A-Za-z0-9\s]/.test(pw)
        };
        var valid = r.len && r.upper && r.lower && r.digit && r.symbol;

        var letters = low.replace(/[^a-z]/g, '');
        var common = false;
        for (var i = 0; i < COMMON.length && !common; i++) {
            var cl = COMMON[i].replace(/[^a-z]/g, '');
            if (low === COMMON[i] || (cl.length >= 5 && letters === cl)) common = true;
        }
        var personal = false;
        for (var j = 0; j < (tokens || []).length && !personal; j++) {
            if (tokens[j].length >= 3 && low.indexOf(tokens[j]) >= 0) personal = true;
        }
        var long12 = pw.length >= 12;

        var pts = Math.min(pw.length, 8) / 8 * 20;
        if (r.upper) pts += 12;
        if (r.lower) pts += 12;
        if (r.digit) pts += 12;
        if (r.symbol) pts += 12;
        if (long12) pts += 10;
        if (valid && !common) pts += 11;      // bonus points only count once the basics are met
        if (valid && !personal) pts += 11;
        if (!valid) pts = Math.min(pts, 59);   // can never look "Good" while a rule is missing
        if (common || personal) pts = Math.min(pts, 79);   // and never "Strong"
        if (pw.length === 0) pts = 0;
        pts = Math.round(pts);

        var level = pts < 20 ? 0 : pts < 40 ? 1 : pts < 60 ? 2 : pts < 80 ? 3 : 4;
        return {
            rules: r, valid: valid, common: common, personal: personal, long12: long12,
            score: pts, level: level, strong: valid && !common && !personal && pts >= 80
        };
    }

    var LEVELS = ['Very weak', 'Weak', 'Fair', 'Good', 'Strong'];

    function attachStrength(box) {
        var input = document.getElementById(box.getAttribute('data-input'));
        if (!input) return;
        var ctxIds = (box.getAttribute('data-context') || '').split(',').filter(function (s) { return s; });
        var hasCtx = ctxIds.length > 0;

        var rows = [
            { k: 'len', t: 'At least 8 characters', req: true },
            { k: 'upper', t: 'An uppercase letter (A-Z)', req: true },
            { k: 'lower', t: 'A lowercase letter (a-z)', req: true },
            { k: 'digit', t: 'A number (0-9)', req: true },
            { k: 'symbol', t: 'A symbol (! @ # $ % & * ?)', req: true },
            { k: 'long12', t: '12 or more characters (bonus)', req: false },
            { k: 'common', t: 'Not a commonly used password (bonus)', req: false, invert: true }
        ];
        if (hasCtx) rows.push({ k: 'personal', t: 'Does not contain your name, ID or email (bonus)', req: false, invert: true });

        var html = '<div class="qcu-strength-top">' +
            '<div class="qcu-strength-bar" role="progressbar" aria-label="Password strength" aria-valuemin="0" aria-valuemax="100" aria-valuenow="0"><div class="qcu-strength-fill"></div></div>' +
            '<span class="qcu-strength-label"></span></div>' +
            '<div class="qcu-strength-need" aria-live="polite"></div><ul class="qcu-strength-list">';
        rows.forEach(function (r) { html += '<li data-k="' + r.k + '"></li>'; });
        html += '</ul>';
        box.className = (box.className + ' qcu-strength').trim();
        box.innerHTML = html;

        var fill = box.querySelector('.qcu-strength-fill');
        var bar = box.querySelector('.qcu-strength-bar');
        var label = box.querySelector('.qcu-strength-label');
        var need = box.querySelector('.qcu-strength-need');

        function tokens() {
            var out = [];
            ctxIds.forEach(function (id) {
                var el = document.getElementById(id);
                if (!el || !el.value) return;
                var v = el.value.toLowerCase().trim();
                if (v.indexOf('@') > 0) v = v.split('@')[0];
                v = v.replace(/-/g, '');
                if (v.length >= 3) out.push(v);
                v.split(/[\s._]+/).forEach(function (p) { if (p.length >= 3 && p !== v) out.push(p); });
            });
            return out;
        }

        function refresh() {
            var pw = input.value;
            var e = evaluate(pw, tokens());
            var empty = pw.length === 0;

            fill.style.width = e.score + '%';
            fill.className = 'qcu-strength-fill lv' + e.level;
            bar.setAttribute('aria-valuenow', e.score);
            label.textContent = empty ? '' : LEVELS[e.level];
            label.className = 'qcu-strength-label lv' + e.level;

            rows.forEach(function (r) {
                var li = box.querySelector('li[data-k="' + r.k + '"]');
                var ok = r.invert ? !e[r.k] : (r.req ? e.rules[r.k] : e[r.k]);
                var mark, cls;
                if (empty) { mark = '\u25CB '; cls = 'idle'; }
                else if (ok) { mark = '\u2714 '; cls = 'ok'; }
                else { mark = r.req ? '\u2716 ' : '\u25CB '; cls = r.req ? 'bad' : 'idle'; }
                li.textContent = mark + r.t;
                li.className = cls;
            });

            var msg;
            if (empty) {
                msg = 'Start typing to see how strong your password is.';
            } else if (!e.valid) {
                var miss = [];
                if (!e.rules.len) miss.push('at least 8 characters (' + (8 - pw.length) + ' more)');
                if (!e.rules.upper) miss.push('an uppercase letter');
                if (!e.rules.lower) miss.push('a lowercase letter');
                if (!e.rules.digit) miss.push('a number');
                if (!e.rules.symbol) miss.push('a symbol');
                msg = 'Still needed: ' + joinList(miss) + '.';
            } else if (e.strong) {
                msg = 'Strong password. You are all set.' + (e.long12 ? '' : ' Tip: 12 or more characters make it even stronger.');
            } else {
                var adv = [];
                if (e.common) adv.push('this is a commonly used password, so try something less predictable');
                if (e.personal) adv.push('avoid using your name, ID or email in your password');
                msg = 'Meets the requirements. To make it stronger: ' + adv.join('; ') + '.';
            }
            need.textContent = msg;
        }

        input.addEventListener('input', refresh);
        ctxIds.forEach(function (id) {
            var el = document.getElementById(id);
            if (el) el.addEventListener('input', refresh);
        });
        refresh();
    }

    /* "passwords match" hint under a confirm field */
    function attachMatch(el) {
        var a = document.getElementById(el.getAttribute('data-a'));
        var b = document.getElementById(el.getAttribute('data-b'));
        if (!a || !b) return;
        el.setAttribute('aria-live', 'polite');
        function refresh() {
            if (!b.value) { el.textContent = ''; el.className = 'qcu-match'; return; }
            var same = (a.value === b.value);
            el.textContent = same ? '\u2714 Passwords match' : '\u2716 Passwords do not match';
            el.className = 'qcu-match ' + (same ? 'ok' : 'bad');
        }
        a.addEventListener('input', refresh);
        b.addEventListener('input', refresh);
        refresh();
    }

    // Used by CustomValidators: ClientValidationFunction="qcuValidateStrongPassword"
    window.qcuValidateStrongPassword = function (source, args) {
        args.IsValid = evaluate(args.Value || '', []).valid;
    };

    window.Qcu = {
        initPasswordToggles: initPasswordToggles,
        formatDashed: formatDashed,
        attachDash: attachDash,
        evaluatePassword: evaluate,
        attachStrength: attachStrength
    };

    document.addEventListener('DOMContentLoaded', function () {
        initPasswordToggles(document);
        var boxes = document.querySelectorAll('[data-qcu-strength]');
        for (var i = 0; i < boxes.length; i++) attachStrength(boxes[i]);
        var ms = document.querySelectorAll('[data-qcu-match]');
        for (var k = 0; k < ms.length; k++) attachMatch(ms[k]);
    });
})();