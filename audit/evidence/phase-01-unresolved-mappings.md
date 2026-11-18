# Phase 01 — Unresolved mappings and findings (static evidence only)

All items are **static observations from the two archives**. Nothing here was built or run (RuntimeStatus = NOT RUN). No application code was changed.

## Coverage result
- 25/25 source `.aspx` pages are mapped to a specific MVC route/action/view (matrix rows 1–25). Site.Master is covered in `shared-responsibility-map.csv`.
- 71 controller actions parsed; every action maps to a matrix row (`action-inventory.csv`).
- 36/36 Razor views classified (`view-inventory.csv`); every view has at least one reference or framework role.
- Classic `.csproj` vs disk: every `.cs`/`.cshtml` on disk is included, none missing.

## Open items (for the phase named)
| # | Item | Severity | Phase |
|---|---|---|---|
| U1 | **Logout bounce**: `AccountController.Logout` has no `[QCUCRAM_RequireLogin]`, so `QCUCRAM_GuestController` redirects a signed-in user to the dashboard before `SignOut` runs (nav link has no `?msg`). | HIGH (needs runtime proof) | 07 |
| U2 | **AdminSetup routing**: no `[RoutePrefix]`/`[Route]`; namespace `Controllers` is outside the namespace-restricted `admin/{controller}/{action}` route. Nav-generated URL may be `/admin/adminsetup/...` and 404; real URL resolves as `/adminsetup/...`. | HIGH (hypothesis) | 16 (confirm in 05) |
| U3 | **Duplicate password-change entry points**: `Account/ChangePassword` and `POST /student/profile/password`. Source has one page. | MEDIUM | 07 / 10 |
| U4 | **Extra target URLs not in source**: `/availability/day/{id}`, `?room=&slot=` on Availability, `?month=`, `?review=`, `?reassign=`, `/admin/records/{id}/detail`, `/admin/records/export`, `ReturnUrl`/`role` on Login. Need approve-or-remove decision. | MEDIUM | 09, 10, 15, 16, 07 |
| U5 | **Alias controllers**: `AdminDashboardController`, `StudentDashboardController` (redirect-only) and `AdminRestrictionsController` (still linked from nav). Keep until call sites proven. | LOW | 15–16 |
| U6 | **Sweep triggers**: sweep runs from `QCUCRAMSweepTimer`, `QCUCRAM_BaseController`, global `QCUCRAMSweepOnRequestAttribute`, and `QCUCRAM_MaintenanceService.StartBackgroundTimer` exists. Source DevTools disables on-load sweep. Check duplicates/opt-out. | MEDIUM | 05, 16 |
| U7 | **Print ownership**: confirm a student cannot print another student's booking (source filters by `@sid`). | MEDIUM | 17 |
| U8 | **Duplicate route registration** for print-booking (attribute + conventional). | LOW | 17 |
| U9 | **Error pages**: source has no error `.aspx`; compare source `Web.config` customErrors. | LOW | 18 |

## Corrections to the earlier `page-parity-matrix.csv` found in the target ZIP
The file previously present did not match this archive and was replaced:
- Row 2: guard was recorded as `[AllowAnonymous]`; actual is `[QCUCRAM_RequireLogin]` on `Index` and `Day` (matches the source SecurePage). The "access deviation" finding is withdrawn.
- Row 1: guest redirect recorded as `?msg=login`; actual is `~/login` with no query string (matches source).
- X1/X2/X7-type rows cited `Views/AdminDashboard`, `Views/StudentDashboard`, `Views/Home/*`, `Shared/_Layout.cshtml`, `Shared/Error.cshtml`, `Views/Error/Index.cshtml`; none of these exist in this archive.
- Dashboard nav now links `Url.Action("Dashboard", ...)`, not the alias controllers.
- Phase numbers cited (up to 36) came from a different plan; replaced with runbook Phase 02–18 numbers.
