# TEST EVIDENCE INDEX - Phase 18

Status values: PASS / FAIL / NOT RUN. Nothing below has been executed except the static checks marked "Static done".
Save evidence under `audit/evidence/` using the names in the last column.

## Static done
| ID | Check | Result |
|---|---|---|
| S-01 | 25 source pages mapped (Phase 01 matrix) | Static done - 25/25 mapped |
| S-02 | All 35 `[HttpPost]` actions carry an anti-forgery attribute near the action (heuristic scan) | Static done - 0 missing. Does not prove the view emits the token or that AJAX posts send it |
| S-03 | Print ownership filter in `ReportService.PrintBooking` | Static done - present; runtime NOT RUN |
| S-04 | Student print Back route exists | Static done - found defect D18-02, fixed |

## Build and database (NOT RUN)
| ID | Test | Steps | Expected | Evidence file |
|---|---|---|---|---|
| TC-BLD-01 | Debug build | see BUILD_LOG | 0 errors | build-debug.log |
| TC-BLD-02 | Release build | see BUILD_LOG | 0 errors | build-release.log |
| TC-DB-01 | Schema on a disposable server | `sqlcmd -S localhost\SQLEXPRESS -E -i QCU-CRAM_Schema.sql` then `-i QCU-CRAM_Seed.sql` | `QCU-CRAM_DB` has 9 tables; 6 rooms, 9 slots, 5 settings | db-schema.txt |
| TC-DB-02 | Constraint checks | `-i QCU-CRAM_TestChecks.sql -o audit\database-reconciliation\testchecks.txt` | all checks pass | testchecks.txt |
| TC-DB-03 | Old-to-new copy on a disposable copy of the old DB | run `QCU-CRAM_CopyFromOldDb.sql`; keep its `#CopyCheck` output | per-table old count = new count; old DB unchanged (compare row counts before/after) | copy-check.txt |
| TC-DB-04 | Connection target | start app, open any page, query `sys.dm_exec_sessions` for `QCU-CRAM_DB` | sessions use `QCU-CRAM_DB`, none use `Guades_QCUCram_DB` | conn-check.txt |

## Workflows, security, print (NOT RUN)
| ID | Test | Expected | Evidence file |
|---|---|---|---|
| TC-AUTH-01 | Signed-in user opens `/logout` (GET and POST) | session cleared, cookie expired, redirect to login with `loggedout` notice; Back button shows no stale page | logout.mp4 / notes |
| TC-RTE-01 | `/` as guest, student, admin | login / student dashboard / admin dashboard | routes.csv |
| TC-RTE-02 | Open every Admin Setup page from the nav | no 404 | routes.csv |
| TC-RTE-03 | `/admin/admindashboard`, `/student/studentdashboard`, `/admin/adminrestrictions` | redirect to canonical page | routes.csv |
| TC-WF-01 | Guest -> register -> login -> reserve -> admin approve -> check-in -> notification -> cancel path -> reports | same outcomes and DB rows as source | wf-*.png, SQL dumps |
| TC-SEC-01 | Student requests admin URLs directly | redirect/denied as in source | sec.csv |
| TC-SEC-02 | POST without anti-forgery token | rejected | sec.csv |
| TC-SEC-03 | Student A opens print sheet for Student B's reservation id | "could not be found" message | sec.csv |
| TC-SEC-04 | Session expiry (30 min) then POST | redirected to login, no write | sec.csv |
| TC-PRN-01 | Print preview of print sheet at the same paper size as source | matches source layout | print-*.pdf |
| TC-PRN-02 | Student print sheet "Back" | opens My Reservations (200) | prn.png |
| TC-OPS-01 | Leave app idle 3 minutes with a due reservation | single sweep result, no duplicate history rows | ops.sql |
| TC-VIS-01..25 | One desktop (1440x900) and one mobile (390x844) screenshot per page, source vs MVC, same data | match | `P<nn>_<page>_<source|mvc>_<size>.png` |
