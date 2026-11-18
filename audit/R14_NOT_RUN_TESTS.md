# Phase R14 — Tests still NOT RUN

**Status:** Static configuration checks only. No MSBuild/Visual Studio build, IIS Express/browser run, SQL Server execution, or source-vs-MVC visual comparison was performed. The referenced `BSIT3D_GUADES_MVC_One_to_One_Conversion_Repair_Runbook.md` was not included; its §4.1 role matrix must be supplied before the role gate can be completed.

## Required tests

| ID | Test | Exact manual procedure | Expected result |
|---|---|---|---|
| R14-BLD-01 | Debug build | Open `BSIT3D_GUADES_Project_MVC.slnx` in a compatible Visual Studio environment; select Debug; Build/Rebuild Solution; save the complete output. | Build succeeds with zero errors. |
| R14-BLD-02 | Release build | Repeat with Release configuration; save the complete output. | Build succeeds with zero errors. |
| R14-DB-01 | Schema and seed | On a disposable SQL Server instance, run `QCU-CRAM_Schema.sql`, then `QCU-CRAM_Seed.sql`. | Target database has expected schema and seed data; no production data touched. |
| R14-DB-02 | Constraints | Run `QCU-CRAM_TestChecks.sql` against the disposable target database. | Every constraint/index assertion passes. |
| R14-DB-03 | Legacy data copy | Use a disposable copy of the source database; run `QCU-CRAM_CopyFromOldDb.sql`; retain its `#CopyCheck` output and compare old/new table counts before and after. | Counts reconcile; legacy source database remains unchanged. |
| R14-DB-04 | Active connection | Start MVC app, open a page, then inspect SQL Server sessions for the application connection. | App connects to `QCU-CRAM_DB`; no app session uses the legacy database. |
| R14-AUTH-01 | Guest route matrix | While signed out, open `/`, student-only URLs, and admin-only URLs; compare redirects/denials with source and runbook §4.1. | Guest receives source-equivalent login/denial behavior; no protected data is exposed. |
| R14-AUTH-02 | Student route/ownership matrix | Sign in as Student A; open own pages and attempt admin URLs and Student B's print-booking ID; test expired session then POST. | Access and not-found/redirect behavior match source; no unauthorized data/write. |
| R14-AUTH-03 | Admin route matrix | Sign in as admin; open every Admin Setup page from navigation; run the Records and Restrictions paths; compare to source. | Pages load without 404; permissions and outcomes match source. |
| R14-WF-01 | End-to-end workflow | Guest register → student login → reserve → admin approve → check-in → student notification → cancellation path → reports; compare UI and DB effects with source. | Same expected state transitions, messages, and database effects. |
| R14-STR-01 | String parity | For each source `.aspx`/`.aspx.cs` page, compare labels, validation messages, toasts, confirms, redirects and empty states with its mapped MVC `.cshtml`/`.cs` implementation; exercise each branch in browser. | No unexplained user-facing wording or behavior differences; document every accepted difference. |
| R14-VIS-01 | Visual parity | Capture each mapped page at 1440x900 and 390x844 using equivalent data; compare source and MVC screenshots. | Layout, tables, badges, empty states and print views match source or have documented accepted differences. |
| R14-PRN-01 | Print regression | Open Daily Schedule and Print Booking; use browser print preview with the same paper size/margins as source. | Only intended report/schedule content prints and layout matches source. |
| R14-OPS-01 | Sweep/logout regression | Leave app idle for three minutes with a due reservation; inspect history for duplicate sweep effects. Then test `/logout` as signed-in student and admin. | One effective sweep; no duplicate history; logout clears session/cookie and returns to login. |

## Evidence to attach

- Debug/Release build logs.
- SQL schema, constraint, copy-count and active-connection outputs.
- Guest/student/admin route results and security checks.
- Source-vs-MVC screenshots and print-preview PDFs.
- Workflow screenshots/DB evidence and updated role matrix from runbook §4.1.

Do not mark a test PASS based on static inspection. Record PASS/FAIL only after execution; otherwise leave NOT RUN.
