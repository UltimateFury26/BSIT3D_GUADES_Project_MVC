/* QCU-CRAM shared UI script
   1. qcuConfirm(el, opts)  - designed confirmation modal (replaces window.confirm)
   2. Live search / filters - debounced typing (text AND date boxes), auto-refresh through
                              UpdatePanels, focus + caret kept, in-flight request cancelled
   2b. Toast messages      - qcuToast(): upper-right pop-out, 5-second countdown bar, then slides away.
                              Server messages arrive through qcuQueue / qcuFlush (see ShowMessage in C#)
   2c. Disabled until ready - buttons with data-qcu-needs / data-qcu-gate stay disabled until their
                              required fields are filled in (so no pop-up opens half-empty)
   3. Form pop-ups          - Add / Edit modals on the maintenance pages: scroll lock,
                              first-field focus, Esc / Cancel / X to close, focus trap, and
                              re-initialising the strength meter + student-number box after
                              every UpdatePanel refresh
   4. Content modal        - any link/button with data-qcu-load opens the target page
                              inside a modal instead of a new browser tab (Print / Details),
                              with a Print button that prints the real page through a hidden
                              iframe. data-qcu-print prints a page directly, no modal.
   Plain JavaScript, no libraries. Icons are inline SVG. */
(function () {
    'use strict';

    /* ==================================================================
       1. CONFIRMATION MODAL
       Usage (from an asp:Button / asp:LinkButton):
         OnClientClick="return qcuConfirm(this, { tone: 'danger', title: 'Delete this room?',
                         message: 'This cannot be undone.', confirmText: 'Delete' });"
       Options:
         tone         success | danger | warning | primary   (icon + button colour)
         title        heading.  {action} / {Action} = the clicked control's own text
         message      short text (plain text, line breaks allowed)
         confirmText  label of the coloured button (default "Confirm")
         cancelText   label of the neutral button  (default "Cancel")
         requireCheck text of a tick box that must be ticked before Confirm works
         input        { label, target, required, maxLength, rows, placeholder }
                      shows a text area; its value is copied into the control matched
                      by the CSS selector `target` (e.g. "[id$=txtReason]") on Confirm
       ================================================================== */
    var ICONS = {
        success: '<svg viewBox="0 0 24 24" width="30" height="30" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="M7.5 12.5l3 3 6-7"/></svg>',
        danger: '<svg viewBox="0 0 24 24" width="30" height="30" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M10.3 3.9L1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>',
        warning: '<svg viewBox="0 0 24 24" width="30" height="30" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10"/><line x1="12" y1="7.5" x2="12" y2="13"/><line x1="12" y1="16.5" x2="12.01" y2="16.5"/></svg>',
        primary: '<svg viewBox="0 0 24 24" width="30" height="30" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="M9.5 9a2.5 2.5 0 1 1 3.6 2.2c-.7.4-1.1 1-1.1 1.8"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>'
    };
    var BTN = { success: 'btn-success', danger: 'btn-danger', warning: 'btn-warning', primary: 'btn-primary' };

    var overlay, dialog, elIcon, elTitle, elMsg, elExtra, btnCancel, btnOk;
    var current = null;      // { trigger, opts, ta, chk, err, target }
    var lastFocus = null;

    function build() {
        if (overlay) return;
        overlay = document.createElement('div');
        overlay.className = 'qcu-modal-overlay';
        overlay.hidden = true;
        overlay.innerHTML =
            '<div class="qcu-modal" role="dialog" aria-modal="true" aria-labelledby="qcuModalTitle" aria-describedby="qcuModalMsg" tabindex="-1">' +
            '<div class="qcu-modal-icon" aria-hidden="true"></div>' +
            '<h3 class="qcu-modal-title" id="qcuModalTitle"></h3>' +
            '<p class="qcu-modal-msg" id="qcuModalMsg"></p>' +
            '<div class="qcu-modal-extra"></div>' +
            '<div class="qcu-modal-actions">' +
            '<button type="button" class="btn btn-outline-secondary qcu-modal-cancel">Cancel</button>' +
            '<button type="button" class="btn qcu-modal-ok">Confirm</button>' +
            '</div></div>';
        document.body.appendChild(overlay);

        dialog = overlay.querySelector('.qcu-modal');
        elIcon = overlay.querySelector('.qcu-modal-icon');
        elTitle = overlay.querySelector('.qcu-modal-title');
        elMsg = overlay.querySelector('.qcu-modal-msg');
        elExtra = overlay.querySelector('.qcu-modal-extra');
        btnCancel = overlay.querySelector('.qcu-modal-cancel');
        btnOk = overlay.querySelector('.qcu-modal-ok');

        btnCancel.addEventListener('click', function () { close(false); });
        btnOk.addEventListener('click', function () { close(true); });
        // click on the dimmed area (not on the dialog itself) closes
        overlay.addEventListener('mousedown', function (e) { if (e.target === overlay) close(false); });
        document.addEventListener('keydown', onKey, true);
    }

    function onKey(e) {
        if (!current) return;
        if (e.key === 'Escape' || e.key === 'Esc') {
            e.preventDefault(); e.stopPropagation(); close(false); return;
        }
        if (e.key === 'Tab') {                        // focus trap
            var list = Array.prototype.filter.call(
                dialog.querySelectorAll('button, textarea, input, [href]'),
                function (n) { return !n.disabled && n.offsetParent !== null; });
            if (!list.length) { e.preventDefault(); return; }
            var first = list[0], last = list[list.length - 1];
            if (e.shiftKey && (document.activeElement === first || document.activeElement === dialog)) {
                e.preventDefault(); last.focus();
            } else if (!e.shiftKey && document.activeElement === last) {
                e.preventDefault(); first.focus();
            }
        } else if (e.key === 'Enter' && current.ta && e.target === current.ta && !e.shiftKey) {
            e.preventDefault();                       // Enter in the reason box must not submit the page
        }
    }

    function labelOf(el) {
        if (!el) return '';
        var t = (el.tagName === 'INPUT') ? el.value : el.textContent;
        return (t || '').replace(/\s+/g, ' ').trim();
    }

    function fillTokens(text, el) {
        if (!text) return '';
        var a = labelOf(el), lower = a.toLowerCase();
        var upper = lower.charAt(0).toUpperCase() + lower.slice(1);
        return text.replace(/\{action\}/g, lower).replace(/\{Action\}/g, upper);
    }

    function show(trigger, opts) {
        build();
        var tone = BTN[opts.tone] ? opts.tone : 'warning';
        current = { trigger: trigger, opts: opts, ta: null, chk: null, err: null };
        lastFocus = document.activeElement;

        dialog.setAttribute('data-tone', tone);
        elIcon.innerHTML = ICONS[tone];
        elTitle.textContent = fillTokens(opts.title || 'Are you sure?', trigger);
        elMsg.textContent = fillTokens(opts.message || '', trigger);
        elMsg.style.display = opts.message ? '' : 'none';
        btnCancel.textContent = opts.cancelText || 'Cancel';
        btnOk.textContent = fillTokens(opts.confirmText || 'Confirm', trigger);
        btnOk.className = 'btn qcu-modal-ok ' + BTN[tone];
        btnOk.disabled = false;

        elExtra.innerHTML = '';
        if (opts.input) {
            var wrap = document.createElement('div');
            wrap.className = 'qcu-modal-field';
            var lab = document.createElement('label');
            lab.setAttribute('for', 'qcuModalText');
            lab.textContent = opts.input.label || 'Reason';
            var ta = document.createElement('textarea');
            ta.id = 'qcuModalText';
            ta.className = 'form-control';
            ta.rows = opts.input.rows || 3;
            if (opts.input.maxLength) ta.maxLength = opts.input.maxLength;
            if (opts.input.placeholder) ta.placeholder = opts.input.placeholder;
            var tgt = opts.input.target ? document.querySelector(opts.input.target) : null;
            if (tgt) ta.value = tgt.value || '';
            var err = document.createElement('div');
            err.className = 'qcu-modal-error';
            err.setAttribute('role', 'alert');
            wrap.appendChild(lab); wrap.appendChild(ta); wrap.appendChild(err);
            elExtra.appendChild(wrap);
            current.ta = ta; current.err = err;
        }
        if (opts.requireCheck) {
            var cl = document.createElement('label');
            cl.className = 'qcu-modal-check';
            var cb = document.createElement('input');
            cb.type = 'checkbox';
            var sp = document.createElement('span');
            sp.textContent = opts.requireCheck;
            cl.appendChild(cb); cl.appendChild(sp);
            elExtra.appendChild(cl);
            current.chk = cb;
            btnOk.disabled = true;
            cb.addEventListener('change', function () { btnOk.disabled = !cb.checked; });
        }

        overlay.hidden = false;
        document.documentElement.classList.add('qcu-modal-open');
        // next frame so the fade / scale transition runs
        window.requestAnimationFrame(function () { overlay.classList.add('show'); });

        // focus: Cancel for danger; the field / tick box when there is one; otherwise Confirm
        var focusEl = btnOk;
        if (current.ta) focusEl = current.ta;
        else if (current.chk) focusEl = current.chk;
        else if (tone === 'danger') focusEl = btnCancel;
        setTimeout(function () { try { focusEl.focus(); } catch (x) { } }, 30);
    }

    function close(ok) {
        if (!current) return;
        var c = current;

        if (ok) {
            if (c.ta) {
                var v = c.ta.value.trim();
                if (c.opts.input.required && !v) {
                    c.err.textContent = c.opts.input.requiredMessage || 'Please enter a reason.';
                    c.ta.focus();
                    return;                              // stay open
                }
                var tgt = c.opts.input.target ? document.querySelector(c.opts.input.target) : null;
                if (tgt) tgt.value = c.ta.value;
            }
            if (c.chk && !c.chk.checked) return;
        }

        current = null;
        overlay.classList.remove('show');
        document.documentElement.classList.remove('qcu-modal-open');
        var done = function () { if (!current) overlay.hidden = true; };
        setTimeout(done, 180);

        if (ok && c.trigger) {
            // let qcuConfirm() return true on the re-click, so the original postback runs untouched
            c.trigger.setAttribute('data-qcu-ok', '1');
            c.trigger.click();
        } else if (lastFocus && lastFocus.focus) {
            try { lastFocus.focus(); } catch (x) { }
        }
    }

    window.qcuConfirm = function (el, opts) {
        if (el && el.getAttribute && el.getAttribute('data-qcu-ok') === '1') {
            el.removeAttribute('data-qcu-ok');
            return true;                                  // confirmed: allow the normal postback
        }
        // A button that needs fields filled in first never opens the pop-up while they are empty
        if (el && el.getAttribute && el.hasAttribute('data-qcu-needs') && !needsMet(el)) {
            window.qcuToast(el.getAttribute('data-qcu-needs-hint') || 'Please complete the required fields first.', true);
            return false;
        }
        show(el, opts || {});
        return false;                                     // stop the postback while the modal is open
    };

    /* ==================================================================
       2. LIVE SEARCH / FILTERS
       - A text box with  data-live-btn="btnSearch"  re-runs that button ~350 ms after typing stops.
       - Drop-downs use AutoPostBack (set in the .aspx). Date boxes use data-live-btn too, so
         typing a year does not fire one request per digit.
       - Everything sits inside an UpdatePanel, so only the results refresh;
         focus and caret are put back afterwards.
       ================================================================== */
    var DEBOUNCE_MS = 350;
    var timers = {};

    function findButton(suffix, from) {
        var ctx = (from && from.form) || document;
        return ctx.querySelector('[id$="_' + suffix + '"]') || document.getElementById(suffix);
    }

    function prm() {
        return (typeof Sys !== 'undefined' && Sys.WebForms && Sys.WebForms.PageRequestManager)
            ? Sys.WebForms.PageRequestManager.getInstance() : null;
    }

    function fire(input, btnSuffix) {
        var btn = findButton(btnSuffix, input);
        if (!btn) return;
        var m = prm();
        if (m) {
            if (m.get_isInAsyncPostBack()) m.abortPostBack();   // cancel the stale request
            btn.click();
            return;
        }
        // MVC has no UpdatePanel, so the button does a normal GET submit. Remember
        // the focused box and its caret first so the page comes back in the same
        // state - this is the MVC equivalent of the UpdatePanel focus restore.
        rememberLiveFocus();
        btn.click();
    }

    /* ---- MVC live-filter focus restore (the UpdatePanel equivalent) ---- */
    var FOCUS_KEY = 'qcu-live-focus';

    function rememberLiveFocus() {
        try {
            var a = document.activeElement;
            if (!a || !a.id) return;
            var s = null, e = null;
            try { s = a.selectionStart; e = a.selectionEnd; } catch (x) { }
            sessionStorage.setItem(FOCUS_KEY, JSON.stringify({ id: a.id, s: s, e: e }));
        } catch (x) { }
    }

    function restoreLiveFocus() {
        try {
            var raw = sessionStorage.getItem(FOCUS_KEY);
            if (!raw) return;
            sessionStorage.removeItem(FOCUS_KEY);
            var st = JSON.parse(raw);
            var el = document.getElementById(st.id);
            if (!el) return;
            el.focus();
            if (st.s !== null && st.e !== null && el.setSelectionRange) {
                try { el.setSelectionRange(st.s, st.e); } catch (x) { }
            }
        } catch (x) { }
    }

    document.addEventListener('input', function (e) {
        var t = e.target;
        if (!t || !t.getAttribute) return;
        var suffix = t.getAttribute('data-live-btn');
        if (!suffix) return;
        var key = t.id || suffix;
        clearTimeout(timers[key]);
        timers[key] = setTimeout(function () { fire(t, suffix); }, DEBOUNCE_MS);
    });

    // Enter in a live box: the panel's default button handles it, so cancel the pending timer
    document.addEventListener('keydown', function (e) {
        var t = e.target;
        if (e.key === 'Enter' && t && t.getAttribute && t.getAttribute('data-live-btn')) {
            clearTimeout(timers[t.id || t.getAttribute('data-live-btn')]);
        }
    });

    function initAsync() {
        var m = prm();
        if (!m) return;
        var saved = null;

        m.add_beginRequest(function () { document.documentElement.classList.add('qcu-refreshing'); });
        m.add_endRequest(function () { document.documentElement.classList.remove('qcu-refreshing'); });

        // Just before the panels are replaced: remember the focused box, its text and caret
        m.add_pageLoading(function () {
            var a = document.activeElement;
            saved = null;
            if (!a || !a.id) return;
            var tag = a.tagName;
            if (tag === 'INPUT' && (a.type === 'button' || a.type === 'submit' || a.type === 'checkbox' || a.type === 'radio')) return;
            if (tag !== 'INPUT' && tag !== 'SELECT' && tag !== 'TEXTAREA') return;
            var s = null, en = null;
            try { s = a.selectionStart; en = a.selectionEnd; } catch (x) { }
            saved = { id: a.id, value: a.value, live: a.hasAttribute('data-live-btn'), s: s, e: en };
        });

        // After the new HTML is in place: put focus + caret back (and keep text typed meanwhile)
        m.add_pageLoaded(function () {
            if (window.Qcu && Qcu.initPasswordToggles) Qcu.initPasswordToggles(document);
            if (saved) {
                var el = document.getElementById(saved.id);
                if (el) {
                    if (saved.live && el.value !== saved.value) el.value = saved.value;
                    try {
                        el.focus();
                        if (saved.s !== null && el.setSelectionRange) el.setSelectionRange(saved.s, saved.e);
                    } catch (x) { }
                }
                saved = null;
            }
            afterLoad();      // re-init pop-up widgets, then open / close pop-ups to match the server
        });
    }


    /* ==================================================================
       2b. TOAST MESSAGES
       Upper-right card that slides in, shows a bar counting down 5 seconds, then slides out.
       - Hovering pauses the countdown (so a long message can be read); X closes it right away.
       - Called from C# through ShowMessage(): the server queues { m: text, e: isError } in
         window.qcuQueue and calls qcuFlush().
       - Can also be called from JavaScript:  qcuToast('Saved.', false)
       ================================================================== */
    var TOAST_MS = 5000;           // how long a message stays
    var TOAST_MAX = 4;             // never stack more than this many
    var toastStack = null;
    var TOAST_ICONS = {
        error: '<svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><line x1="12" y1="5" x2="12" y2="13"/><line x1="12" y1="18.5" x2="12.01" y2="18.5"/></svg>',
        success: '<svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12.5l4.5 4.5L19 7.5"/></svg>'
    };

    function getToastStack() {
        if (!toastStack || !document.body.contains(toastStack)) {
            toastStack = document.createElement('div');
            toastStack.className = 'qcu-toast-stack';
            document.body.appendChild(toastStack);
        }
        return toastStack;
    }

    function removeToast(t) { if (t && t.parentNode) t.parentNode.removeChild(t); }

    function dismissToast(t) {
        if (!t || t.classList.contains('is-leaving')) return;
        t.classList.add('is-leaving');                          // slide out to the right
        t.addEventListener('animationend', function (e) { if (e.target === t) removeToast(t); });
        setTimeout(function () { removeToast(t); }, 450);       // safety net (e.g. reduced motion)
    }

    window.qcuToast = function (message, isError, title) {
        message = (message == null) ? '' : String(message);
        if (!message) return;
        var kind = isError ? 'error' : 'success';
        var stack = getToastStack();

        // the same text already showing: replace it instead of stacking a copy
        var old = stack.querySelectorAll('.qcu-toast');
        for (var i = 0; i < old.length; i++) {
            if (old[i].getAttribute('data-msg') === message) removeToast(old[i]);
        }
        while (stack.children.length >= TOAST_MAX) removeToast(stack.firstChild);

        var t = document.createElement('div');
        t.className = 'qcu-toast qcu-toast-' + kind;
        t.setAttribute('role', isError ? 'alert' : 'status');
        t.setAttribute('data-msg', message);
        t.innerHTML =
            '<div class="qcu-toast-icon" aria-hidden="true">' + TOAST_ICONS[kind] + '</div>' +
            '<div class="qcu-toast-body"><div class="qcu-toast-title"></div><div class="qcu-toast-msg"></div></div>' +
            '<button type="button" class="qcu-toast-close" aria-label="Dismiss message">&times;</button>' +
            '<div class="qcu-toast-bar"><span></span></div>';
        t.querySelector('.qcu-toast-title').textContent = title || (isError ? 'Error' : 'Success');
        t.querySelector('.qcu-toast-msg').textContent = message;      // textContent: never interpreted as HTML

        var bar = t.querySelector('.qcu-toast-bar > span');
        bar.style.animationDuration = TOAST_MS + 'ms';
        bar.addEventListener('animationend', function () { dismissToast(t); });   // 5 s are up
        t.querySelector('.qcu-toast-close').addEventListener('click', function () { dismissToast(t); });

        stack.appendChild(t);
    };

    // Shows everything the server queued (full page loads AND UpdatePanel refreshes)
    window.qcuFlush = function () {
        var q = window.qcuQueue;
        if (!q || !q.length) return;
        window.qcuQueue = [];
        for (var i = 0; i < q.length; i++) window.qcuToast(q[i].m, !!q[i].e);
    };

    /* ==================================================================
       2c. DISABLED UNTIL READY
       <asp:Button ... data-qcu-needs="[id$=_ddlTarget]|[id$=_txtReason]"
                       data-qcu-needs-hint="Choose a room and enter a reason first." />
           The button stays disabled until every control matched by those CSS selectors (separated
           by | ) is filled in (drop-down: a real choice, text box: not blank, tick box: ticked).
           Used for buttons that open a confirmation pop-up and need typed / chosen values.
       <asp:Button ... ValidationGroup="Room" data-qcu-gate="Room" />
           The Save button of an Add / Edit pop-up stays disabled until every REQUIRED field of that
           validation group is filled in. (Format rules such as e-mail or a number range still
           show their message when Save is pressed.)
       The server re-checks everything, so this is only a convenience.
       ================================================================== */
    function isFilled(el) {
        if (!el) return true;                                   // control not on the page: nothing to wait for
        if (el.type === 'checkbox' || el.type === 'radio') return !!el.checked;
        return String(el.value == null ? '' : el.value).trim() !== '';
    }

    function needsMet(btn) {
        var list = (btn.getAttribute('data-qcu-needs') || '').split('|');
        for (var i = 0; i < list.length; i++) {
            var sel = list[i].trim();
            if (!sel) continue;
            var el = null;
            try { el = document.querySelector(sel); } catch (x) { el = null; }
            if (el && !isFilled(el)) return false;
        }
        return true;
    }

    function gateMet(btn) {
        var group = btn.getAttribute('data-qcu-gate');
        var vals = window.Page_Validators;
        if (!vals || !group) return true;
        for (var i = 0; i < vals.length; i++) {
            var v = vals[i];
            if (!v || v.validationGroup !== group || v.enabled === false) continue;
            var fn = v.evaluationfunction;
            if (typeof fn === 'string') fn = window[fn];                       // not converted yet on first load
            if (typeof fn !== 'function' || fn !== window.RequiredFieldValidatorEvaluateIsValid) continue;   // required-field checks only
            var ok = true;
            try { ok = fn(v); } catch (x) { ok = true; }
            if (!ok) return false;
        }
        return true;
    }

    function updateGates() {
        var list = document.querySelectorAll('[data-qcu-needs], [data-qcu-gate]');
        for (var i = 0; i < list.length; i++) {
            var b = list[i];
            if ((b.value || '') === 'Please wait...') continue;                // a save is in progress
            var ok = true;
            if (b.hasAttribute('data-qcu-needs')) ok = needsMet(b);
            if (ok && b.hasAttribute('data-qcu-gate')) ok = gateMet(b);
            b.disabled = !ok;
            b.setAttribute('data-qcu-gated', '1');
            if (ok) b.removeAttribute('title');
            else b.title = b.getAttribute('data-qcu-needs-hint') || 'Fill in all the required fields first.';
        }
    }

    document.addEventListener('input', updateGates);
    document.addEventListener('change', updateGates);
    window.addEventListener('load', updateGates);       // validators are fully set up by then

    // Safety net: make sure every password box has its show/hide eye (safe to repeat; qcu-site.js skips wrapped boxes)
    function ensureEyes() { if (window.Qcu && Qcu.initPasswordToggles) Qcu.initPasswordToggles(document); }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', ensureEyes);
    else ensureEyes();

    /* ==================================================================
       4. CONTENT MODAL (reusable in-page loader)
       Any link/button with  data-qcu-load  opens the target page inside a modal
       instead of a new browser tab. The page's .report (or its main content) is
       fetched and shown, with a Print button that prints the real report page
       through a hidden iframe (so the printed output keeps its own print styles).

         <a href="PrintBooking.aspx?id=12" data-qcu-load="1"
            data-qcu-load-title="Reservation #12">Print</a>
         <a href="PrintBooking.aspx?id=12" data-qcu-print="1">Print</a>   <- prints directly
       ================================================================== */
    var contentOverlay = null, contentBody = null, contentTitle = null;
    var contentPrintUrl = '', contentLastFocus = null;

    function buildContent() {
        if (contentOverlay) return;
        contentOverlay = document.createElement('div');
        contentOverlay.className = 'qcu-content-overlay';
        contentOverlay.hidden = true;
        contentOverlay.innerHTML =
            '<div class="qcu-content-modal" role="dialog" aria-modal="true" aria-labelledby="qcuContentTitle" tabindex="-1">' +
            '<div class="qcu-content-head">' +
            '<h3 class="qcu-content-title" id="qcuContentTitle"></h3>' +
            '<button type="button" class="qcu-form-close qcu-content-x" aria-label="Close">&times;</button>' +
            '</div>' +
            '<div class="qcu-content-body"></div>' +
            '<div class="qcu-content-foot">' +
            '<button type="button" class="btn btn-outline-secondary qcu-content-close">Close</button>' +
            '<button type="button" class="btn btn-success qcu-content-print">Print</button>' +
            '</div></div>';
        document.body.appendChild(contentOverlay);

        contentBody = contentOverlay.querySelector('.qcu-content-body');
        contentTitle = contentOverlay.querySelector('.qcu-content-title');
        contentOverlay.querySelector('.qcu-content-x').addEventListener('click', closeContent);
        contentOverlay.querySelector('.qcu-content-close').addEventListener('click', closeContent);
        contentOverlay.querySelector('.qcu-content-print').addEventListener('click', function () { printUrl(contentPrintUrl); });
        contentOverlay.addEventListener('mousedown', function (e) { if (e.target === contentOverlay) closeContent(); });
    }

    function openContent(url, title) {
        buildContent();
        contentLastFocus = document.activeElement;
        contentTitle.textContent = title || 'Loading...';
        contentPrintUrl = url;
        contentBody.innerHTML = '<div class="qcu-content-loading">Loading...</div>';
        contentOverlay.hidden = false;
        document.documentElement.classList.add('qcu-modal-open');
        window.requestAnimationFrame(function () { contentOverlay.classList.add('show'); });
        setTimeout(function () { try { contentOverlay.querySelector('.qcu-content-modal').focus(); } catch (x) { } }, 30);

        fetch(url, { credentials: 'same-origin' })
            .then(function (r) { if (!r.ok) throw new Error(r.status); return r.text(); })
            .then(function (html) {
                var doc = new DOMParser().parseFromString(html, 'text/html');
                var report = doc.querySelector('.report');
                if (report) {
                    contentBody.innerHTML = report.outerHTML;
                } else {
                    contentBody.innerHTML = '<div class="alert alert-warning mb-0">This report is not available. ' +
                        'It may have been removed, or it belongs to another account.</div>';
                }
            })
            .catch(function () {
                contentBody.innerHTML = '<div class="alert alert-danger mb-0">Sorry, this could not be loaded here. ' +
                    '<a href="' + url + '" target="_blank" rel="noopener">Open it in a new tab</a> instead.</div>';
            });
    }

    function closeContent() {
        if (!contentOverlay || contentOverlay.hidden) return;
        contentOverlay.classList.remove('show');
        setTimeout(function () { if (contentOverlay) contentOverlay.hidden = true; }, 180);
        if (contentLastFocus && contentLastFocus.focus) { try { contentLastFocus.focus(); } catch (x) { } }
        syncFormModals();   // keep the scroll lock if a form pop-up is still open behind this
    }

    // Prints a page through a hidden iframe so only that page is printed (keeps its print CSS)
    function printUrl(url) {
        if (!url) return;
        var f = document.createElement('iframe');
        f.setAttribute('aria-hidden', 'true');
        f.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;';
        f.onload = function () {
            try {
                f.contentWindow.focus();
                f.contentWindow.print();
            } catch (e) {
                window.open(url, '_blank');
            }
            setTimeout(function () { if (f.parentNode) f.parentNode.removeChild(f); }, 60000);
        };
        f.src = url;
        document.body.appendChild(f);
    }

    // One delegated click handler for both data-qcu-load and data-qcu-print
    document.addEventListener('click', function (e) {
        var t = e.target;
        if (!t || !t.closest) return;

        var load = t.closest('[data-qcu-load]');
        if (load) {
            var url = load.getAttribute('data-qcu-load');
            if (!url || url === '1') url = load.getAttribute('href') || load.getAttribute('data-href');
            if (url && url !== '#') {
                e.preventDefault();
                openContent(url, load.getAttribute('data-qcu-load-title'));
            }
            return;
        }

        var pr = t.closest('[data-qcu-print]');
        if (pr) {
            var purl = pr.getAttribute('data-qcu-print');
            if (!purl || purl === '1') purl = pr.getAttribute('href');
            if (purl && purl !== '#') { e.preventDefault(); printUrl(purl); }
        }
    });

    // Esc closes the content modal; Tab stays inside it. Capture phase so it runs first.
    document.addEventListener('keydown', function (e) {
        if (!contentOverlay || contentOverlay.hidden) return;
        if (e.key === 'Escape' || e.key === 'Esc') {
            e.preventDefault(); e.stopPropagation(); closeContent(); return;
        }
        if (e.key === 'Tab') {
            var dlg = contentOverlay.querySelector('.qcu-content-modal');
            var list = Array.prototype.filter.call(
                dlg.querySelectorAll('button, [href], input, select, textarea'),
                function (n) { return !n.disabled && n.offsetParent !== null; });
            if (!list.length) { e.preventDefault(); return; }
            var first = list[0], last = list[list.length - 1];
            if (e.shiftKey && (document.activeElement === first || document.activeElement === dlg)) {
                e.preventDefault(); last.focus();
            } else if (!e.shiftKey && document.activeElement === last) {
                e.preventDefault(); first.focus();
            }
        }
    }, true);

    /* ==================================================================
       3. FORM POP-UPS
       Markup (see Rooms / Time Slots / Students / Admin Accounts):
         <div class="qcu-form-overlay<%= ModalOpenCss %>">   <- server adds " is-open"
           <div class="qcu-form-modal" role="dialog" ...>
             ... any element with  data-qcu-close  closes the pop-up (X and Cancel)
       Opening is done by the server (Add / Edit buttons post back inside an UpdatePanel),
       so a validation error simply renders the pop-up open again, and a successful save
       renders it closed. This code only adds the behaviour around that.
       ================================================================== */
    var FOCUSABLE = 'button, input, select, textarea, a[href]';

    function openFormOverlay() { return document.querySelector('.qcu-form-overlay.is-open'); }
    function confirmShowing() { return !!document.querySelector('.qcu-modal-overlay.show'); }

    function visibleFocusables(dlg) {
        return Array.prototype.filter.call(dlg.querySelectorAll(FOCUSABLE), function (n) {
            return !n.disabled && n.type !== 'hidden' && n.offsetParent !== null;
        });
    }

    function firstField(dlg) {
        var list = dlg.querySelectorAll('input, select, textarea');
        for (var i = 0; i < list.length; i++) {
            var n = list[i];
            if (n.disabled || n.type === 'hidden' || n.type === 'checkbox' || n.type === 'radio' || n.offsetParent === null) continue;
            return n;
        }
        return null;
    }

    function syncFormModals() {
        var open = openFormOverlay();
        if (open) {
            document.documentElement.classList.add('qcu-modal-open');           // lock page scroll
            var dlg = open.querySelector('.qcu-form-modal');
            if (dlg && !dlg.contains(document.activeElement)) {
                var f = firstField(dlg) || dlg;
                setTimeout(function () { try { f.focus(); } catch (x) { } }, 30);
            }
        } else if (!confirmShowing()) {
            document.documentElement.classList.remove('qcu-modal-open');
        }
    }

    function closeFormOverlay(ov) {
        if (!ov) return;
        ov.classList.remove('is-open');
        syncFormModals();
    }

    // X and Cancel (any element inside a pop-up with data-qcu-close)
    document.addEventListener('click', function (e) {
        var t = e.target;
        var c = (t && t.closest) ? t.closest('[data-qcu-close]') : null;
        if (!c) return;
        var ov = c.closest('.qcu-form-overlay');
        if (ov) { e.preventDefault(); closeFormOverlay(ov); }
    });

    // Esc closes; Tab stays inside the pop-up. (Clicking the dimmed area does NOT close it,
    // so a half-typed form is never lost by accident.)
    document.addEventListener('keydown', function (e) {
        if (contentOverlay && !contentOverlay.hidden) return;   // the content modal handles its own keys
        var ov = openFormOverlay();
        if (!ov || confirmShowing()) return;
        if (e.key === 'Escape' || e.key === 'Esc') {
            e.preventDefault(); closeFormOverlay(ov); return;
        }
        if (e.key === 'Tab') {
            var dlg = ov.querySelector('.qcu-form-modal');
            if (!dlg) return;
            var list = visibleFocusables(dlg);
            if (!list.length) { e.preventDefault(); return; }
            var first = list[0], last = list[list.length - 1];
            if (e.shiftKey && (document.activeElement === first || document.activeElement === dlg)) {
                e.preventDefault(); last.focus();
            } else if (!e.shiftKey && document.activeElement === last) {
                e.preventDefault(); first.focus();
            }
        }
    });

    // Web Forms' "default button" handler on the pop-up panel would press Save when Enter is hit on the
    // Cancel / X / eye buttons. Stop Enter from bubbling out of any real <button> inside a pop-up, so
    // Enter on those buttons only does what the button itself does. (Enter in a text box still saves.)
    document.addEventListener('keypress', function (e) {
        var t = e.target;
        if (e.key === 'Enter' && t && t.tagName === 'BUTTON' && t.closest && t.closest('.qcu-form-modal')) {
            e.stopPropagation();
        }
    }, true);

    // Middle initial boxes (data-qcu-mi="1"): letters only, upper-case
    document.addEventListener('input', function (e) {
        var t = e.target;
        if (t && t.hasAttribute && t.hasAttribute('data-qcu-mi')) {
            var v = t.value.replace(/[^A-Za-z]/g, '').toUpperCase();
            if (v !== t.value) t.value = v;
        }
    });

    // Password strength meters inside pop-ups are re-created by the server on every refresh,
    // so they must be wired up again (qcu-site.js only does the first page load).
    function initStrengthBoxes(root) {
        if (!window.Qcu || !Qcu.attachStrength) return;
        var boxes = (root || document).querySelectorAll('[data-qcu-strength]');
        for (var i = 0; i < boxes.length; i++) {
            if (!boxes[i].classList.contains('qcu-strength')) Qcu.attachStrength(boxes[i]);
        }
    }

    /* Student / ID number box with the Register-page behaviour:
         <asp:TextBox data-qcu-idtype="<requester-type select id>" data-qcu-idstrict="1"
                      data-qcu-idlabel="<label id>" data-qcu-idhint="<hint id>" />
       strict = 1 (Add): Student -> digits only, 23-0001 auto-dash, max 7.  strict = 0 (Edit): left alone,
       so older IDs (e.g. SAMPLE-001) can still be edited. */
    function initIdFields(root) {
        if (!window.Qcu) return;
        var list = (root || document).querySelectorAll('input[data-qcu-idtype]');
        for (var i = 0; i < list.length; i++) {
            (function (input) {
                if (input.getAttribute('data-qcu-idinit') === '1') return;
                var type = document.getElementById(input.getAttribute('data-qcu-idtype'));
                if (!type) return;
                input.setAttribute('data-qcu-idinit', '1');
                if (input.getAttribute('data-qcu-idstrict') !== '1') return;     // Edit: no formatting

                var label = document.getElementById(input.getAttribute('data-qcu-idlabel') || '');
                var hint = document.getElementById(input.getAttribute('data-qcu-idhint') || '');

                function apply() {
                    var t = type.value;
                    if (t === 'Student') {
                        if (label) label.textContent = 'Student Number';
                        input.placeholder = '23-0001';
                        input.maxLength = 7;
                        input.setAttribute('inputmode', 'numeric');
                        if (hint) hint.textContent = 'Format: 23-0001 (the dash is added for you).';
                        input.value = Qcu.formatDashed(input.value.replace(/[^\d-]/g, ''));
                    } else {
                        if (label) label.textContent = (t === 'Faculty') ? 'Faculty ID' : (t === 'Visitor') ? 'ID Number' : 'ID / Student Number';
                        input.placeholder = (t === 'Faculty') ? 'Faculty ID' : (t === 'Visitor') ? 'ID number' : '23-0001';
                        input.maxLength = 20;
                        input.setAttribute('inputmode', 'text');
                        if (hint) hint.textContent = (t === '') ? 'Choose a requester type first.' : '';
                    }
                }
                type.addEventListener('change', apply);
                Qcu.attachDash(input, {
                    enabled: function () { return type.value === 'Student'; },
                    digitsOnly: function () { return type.value === 'Student'; }
                });
                apply();
            })(list[i]);
        }
    }

    // CustomValidator for that box: ClientValidationFunction="qcuValidateModalId"
    // (the server re-checks everything when the form is saved)
    window.qcuValidateModalId = function (source, args) {
        var input = document.getElementById(source.controltovalidate);
        var type = input ? document.getElementById(input.getAttribute('data-qcu-idtype') || '') : null;
        var strict = input && input.getAttribute('data-qcu-idstrict') === '1';
        var v = (args.Value || '').trim();
        if (strict && type && type.value === 'Student') {
            args.IsValid = /^\d{2}-\d{4}$/.test(v);
            source.innerHTML = 'Student number must look like 23-0001.';
        } else {
            args.IsValid = /^[A-Za-z0-9\-]{3,20}$/.test(v);
            source.innerHTML = 'Use 3-20 letters, numbers, or dashes only.';
        }
    };

    function afterLoad() {
        initStrengthBoxes(document);
        initIdFields(document);
        syncFormModals();
        updateGates();      // buttons that wait for required fields
        restoreLiveFocus(); // put the caret back after an MVC live-filter submit
        qcuFlushSafe();     // any toast the server queued
    }

    function qcuFlushSafe() { if (window.qcuFlush) window.qcuFlush(); }

    // Sys is loaded by the ScriptManager before this script runs; wait for DOM to be safe
    function boot() { initAsync(); afterLoad(); }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
    else boot();
})();
