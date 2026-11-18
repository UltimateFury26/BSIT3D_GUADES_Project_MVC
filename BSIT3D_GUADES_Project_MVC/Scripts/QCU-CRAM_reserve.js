/* =====================================================================
   QCU-CRAM  |  Scripts/QCU-CRAM_reserve.js
   ---------------------------------------------------------------------
   Phase 06. Replaces the AutoPostBack on ddlMembers in
   Guades_QCUCram_Student/Guades_QCUCram_Reserve.aspx.

   Two jobs, both purely cosmetic - every rule is re-checked on the server by
   QCUCRAM_ReservationRepository.ValidateAndCreate:

     1. Keep the member table in step with "Number of members (including you)".
        Rows are added or removed and then renumbered, so the posted names stay
        Members[0]..Members[n-1]. A gap in the indexes would break the default
        model binder.

     2. Show a live character count under Purpose.

   No jQuery: the layout already loads Bootstrap 5, and this file is small
   enough to stay dependency free.
   ===================================================================== */
(function () {
    'use strict';

    // ---------------------------------------------------------------
    //  1. Member rows
    // ---------------------------------------------------------------

    function memberRows() {
        var body = document.getElementById('qcuMemberBody');
        if (!body) { return []; }
        return Array.prototype.slice.call(body.querySelectorAll('tr.qcu-member-row'));
    }

    /**
     * Builds one blank member row. The inputs are created with the same
     * maxlength values the server validates against (60 / 60 / 5).
     */
    function buildRow() {
        var tr = document.createElement('tr');
        tr.className = 'qcu-member-row';

        var cells = [
            { name: 'LastName', max: 60, label: 'last name' },
            { name: 'FirstName', max: 60, label: 'first name' },
            { name: 'MI', max: 5, label: 'middle initial' }
        ];

        var numTd = document.createElement('td');
        numTd.className = 'qcu-member-num';
        numTd.textContent = '';
        tr.appendChild(numTd);

        cells.forEach(function (cell) {
            var td = document.createElement('td');
            var input = document.createElement('input');
            input.type = 'text';
            input.className = 'form-control';
            input.maxLength = cell.max;
            input.name = 'Members[0].' + cell.name;   // renumber() fixes this
            input.setAttribute('aria-label', cell.label);
            td.appendChild(input);
            tr.appendChild(td);
        });

        return tr;
    }

    /**
     * Rewrites every name attribute and the "#" column so the indexes run
     * 0..n-1 with no gaps, which is what the model binder needs.
     */
    function renumber() {
        memberRows().forEach(function (tr, index) {
            var num = tr.querySelector('.qcu-member-num');
            if (num) { num.textContent = String(index + 1); }

            ['LastName', 'FirstName', 'MI'].forEach(function (field) {
                var input = tr.querySelector('input[name$=".' + field + '"]');
                if (input) {
                    input.name = 'Members[' + index + '].' + field;
                    input.setAttribute('aria-label', 'Member ' + (index + 1) + ' ' +
                        field.replace('LastName', 'last name')
                             .replace('FirstName', 'first name')
                             .replace('MI', 'middle initial'));
                }
            });
        });
    }

    /**
     * Grows or shrinks the table to exactly `wanted` rows, never dropping a row
     * that already holds a name (the student would lose typing).
     */
    function syncRows(wanted) {
        var body = document.getElementById('qcuMemberBody');
        if (!body || !wanted || wanted < 1) { return; }

        var rows = memberRows();

        while (rows.length < wanted) {
            body.appendChild(buildRow());
            rows = memberRows();
        }

        while (rows.length > wanted) {
            var last = rows[rows.length - 1];

            var hasValue = Array.prototype.some.call(
                last.querySelectorAll('input'),
                function (input) { return input.value.trim() !== ''; });

            // Stop trimming if the last row is already filled in - shrinking
            // past typed data would silently throw it away.
            if (hasValue) { break; }

            body.removeChild(last);
            rows = memberRows();
        }

        renumber();
    }

    var memberSelect = document.querySelector('[data-qcu-members]');

    if (memberSelect) {
        memberSelect.addEventListener('change', function () {
            syncRows(parseInt(memberSelect.value, 10));
        });

        // The server may have rendered fewer rows than the drop-down says
        // (a failed post that lost a row); reconcile on load.
        syncRows(parseInt(memberSelect.value, 10));
    }

    // ---------------------------------------------------------------
    //  2. Guard against a double submit
    // ---------------------------------------------------------------

    var form = document.getElementById('qcuReserveForm');

    if (form) {
        form.addEventListener('submit', function () {
            var button = form.querySelector('button[type="submit"]');
            if (button) {
                button.disabled = true;
                button.innerHTML = 'Submitting...';
            }
        });
    }
})();
