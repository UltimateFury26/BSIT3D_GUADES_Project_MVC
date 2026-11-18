# DEPLOYMENT CHECKLIST - QCU-CRAM (MVC 5 / .NET 4.8 / EF6 / SQL Server)

Tick only what you actually checked on the target server.

## Database
- [ ] Back up any existing data. `Guades_QCUCram_DB` stays untouched (migration source and fallback).
- [ ] Run `QCU-CRAM_Schema.sql`, then `QCU-CRAM_Seed.sql`, on the target SQL Server.
- [ ] If carrying over data: run `QCU-CRAM_CopyFromOldDb.sql` once; confirm every row in its count table matches. It aborts if `Students` already has rows.
- [ ] `Database=QCU-CRAM_DB` in the live connection string; key name stays `QCU-CRAM_Connection`; `MultipleActiveResultSets=True` kept.
- [ ] No password committed in `Web.config`; use a transform or `aspnet_regiis -pe connectionStrings`.

## Configuration
- [ ] Publish with the Release configuration; the published `Web.config` has no `debug="true"`.
- [ ] `QCU-CRAM_ShowErrorDetail` is `false` (current default).
- [ ] `customErrors` is `RemoteOnly` (current). Decide whether to set `On` for production (server console would then also see the friendly page).
- [ ] `httpCookies requireSSL="false"` today. If the site is served over HTTPS, decide on `requireSSL="true"`. Changing it breaks plain-HTTP sign-in.
- [ ] Sweep settings are as intended: `SweepTimerEnabled=true` (60 s), `SweepOnRequest=true` (30 s). See U6.
- [ ] `QCU-CRAM_EnableDevClock` is not read by any code (D18-03); leaving it `true` changes nothing.

## Accounts and access
- [ ] On first start with an empty `AdminUsers` table the app creates `admin` / `admin123` (same as the Web Forms app). Change this password at first sign-in, or insert real admins before first start.
- [ ] Admin DevTools page is admin-only (TC-SEC-01). Decide whether it stays reachable in production (it is in the source).

## Server
- [ ] IIS app pool: Integrated pipeline, .NET CLR v4.0, .NET Framework 4.8 installed.
- [ ] App pool identity can write to `App_Data\logs` (error log files named `QCU-CRAM_yyyy-MM-dd.log`) and can log in to SQL Server.
- [ ] Session is InProc, 30 minutes (same as source): users are signed out when the app pool recycles.
- [ ] Static files are cached for 7 days. After a deploy, confirm the browser picks up new CSS/JS (bundles are versioned; files linked directly are not).
- [ ] Security headers present on a response (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`); `X-Powered-By` and `X-AspNetMvc-Version` absent.
- [ ] `audit/` folder is not deployed.

## Sign-off
- [ ] TC list in `TEST_EVIDENCE_INDEX.md` executed; `FINAL_PARITY_MATRIX.csv` updated to PASS/FAIL per cell; no open Critical/High rows in `DISCREPANCY_LOG.md`.
