# QCU-CRAM Phase 2 Notes

- Date: 2026-11-18
- Scope: the data layer — EF 6 context, entity configurations, the ADO.NET helper, the
  repository set, and the result-code contract shared by every layer above it.
- This note records the data layer **as it actually shipped**, which differs in two
  places from the folder layout printed in the conversion guide. Both deviations are
  deliberate and are explained at the bottom.

## What Phase 2 delivered

| File | Role |
| --- | --- |
| `QCU-CRAM_Data/QCUCRAMContext.cs` | `DbContext` over the `QCU-CRAM_Connection` connection string; nine `DbSet`s; `OnModelCreating` applies every configuration in `QCU-CRAM_Configurations/` by reflection. |
| `QCU-CRAM_Data/QCUCRAM_Db.cs` | Thin ADO.NET helper used by the repositories: `Query<T>`, `QuerySingle<T>`, `QueryFirstOrDefault<T>`, `Execute`, `Count`, `Any`, `OpenConnection`, `InTransaction<T>`, `RunInTransaction`, static `SafeRollback`, and the static `Exec` / `Scalar` / `ScalarInt` / `Table` / `P(...)` shorthands. |
| `QCU-CRAM_Data/QCUCRAM_Results.cs` | The result-code contract plus `QCUCRAM_ReservationInfo` (including `FormatSlot(TimeSpan, TimeSpan)`), `QCUCRAM_RegisterResult`, and the paging/row shapes the repositories hand back. |
| `QCU-CRAM_Data/QCU-CRAM_Configurations/*.cs` | One `EntityTypeConfiguration<T>` per entity: `QCUCRAM_AdminUserConfiguration`, `QCUCRAM_NotificationConfiguration`, `QCUCRAM_ReservationConfiguration`, `QCUCRAM_ReservationMemberConfiguration`, `QCUCRAM_ReservationStatusHistoryConfiguration`, `QCUCRAM_RoomConfiguration`, `QCUCRAM_SettingConfiguration`, `QCUCRAM_StudentConfiguration`, `QCUCRAM_TimeSlotConfiguration`. |
| `QCU-CRAM_Data/QCUCRAM_AdminUserRepository.cs` | `Login`, `GetById`, `GetByUsername`, `GetAll`, `Count`, `GetPosition`, `Insert`, `Update`, `ChangePassword`, `ResetPassword`, `SetActive`, `Delete`, `EnsureDefaultAdmin`. |
| `QCU-CRAM_Data/QCUCRAM_StudentRepository.cs` | `Register`, `Login`, `LoginWithAnyNumberForm`, `StudentNumberCandidates`, `GetById`, `GetByStudentNumber`, `GetByEmail`, `Search`, `GetExpiredRestrictions`, `CountAll`, `CountRestricted`, `UpdateProfile`, `ChangePassword`, `ResetPassword`, `SetRestriction`, `ResetStrikes`, `IncrementNoShow`, `AutoLift`, `Delete`. |
| `QCU-CRAM_Data/QCUCRAM_ContentRepository.cs` | Rooms, time slots and settings: `GetRooms`, `GetRoom`, `InsertRoom`, `UpdateRoom`, `SetRoomActive`, `DeleteRoom`, `GetTimeSlots`, `GetTimeSlot`, `GetSlotLabel`, `InsertTimeSlot`, `UpdateTimeSlot`, `SetTimeSlotActive`, `DeleteTimeSlot`, `GetAllSettings`, `GetSetting`, `GetSettingInt`, `GetSettingBool`, `SetSetting`, `DeleteSetting`, `BuildRulesHtml`, `GetAvailability`, `IsSlotTaken`, `CountTakenSlots`. |
| `QCU-CRAM_Data/QCUCRAM_ReservationRepository.cs` | Reservation reads and writes, availability counting, status history, and the member join rows. |
| `QCU-CRAM_Data/QCUCRAM_NotificationRepository.cs` | Per-user notification reads, unread counts, mark-read, and insert. |
| `QCU-CRAM_Data/QCUCRAM_ReportRepository.cs` | The aggregate queries behind the reports and the printable booking sheet. |
| `QCU-CRAM_Data/QCU-CRAM_SampleData.cs` | Demonstration data used by the Phase 8 developer tools: `GetCounts`, `GenerateStudents`, `GenerateReservations`, `ClearSampleReservations`, `DeleteSampleStudents`, `ClearAllReservations`. Sample rows are tagged so they can be removed again — student e-mail addresses use `@cram.test` and reservation purposes start with `SAMPLE - `. |

## The result-code contract

Every write method returns an `int` so the caller never has to guess why a save failed:

| Code | Meaning |
| --- | --- |
| `0` | OK |
| `1` | Value already taken (duplicate username, student number, or e-mail) |
| `2` | Row not found |
| `3` | Invalid input rejected by the repository |
| `4` | Guard refused the change (last active administrator, self-disable) |
| `-1` | Foreign-key violation — the row is still referenced and cannot be deleted |

The services above the data layer turn these into user-facing messages; the controllers
turn the messages into `TempData` flashes. Keeping the codes numeric means the same
repository call can be reused from a controller, a background sweep, or a test harness
without dragging presentation concerns down into the data layer.

## Deviations from the guide's folder layout

**`QCU-CRAM_Mappings/` is not used.** The guide's Phase 2 sketch puts the EF
`EntityTypeConfiguration<T>` classes in a top-level `QCU-CRAM_Mappings/` folder. They
shipped instead in `QCU-CRAM_Data/QCU-CRAM_Configurations/`, next to the context that
loads them. The folder is kept (empty, with a `.keep`) so the guide's paths still
resolve, but no code lives there.

**`QCU-CRAM_DbInitializer.cs` is not used.** The guide describes a
`QCUCRAMDbInitializer` that seeds rooms, time slots, settings and the default
administrator on first run. That work is folded into two places instead: the fixed
reference data (rooms, time slots, setting defaults) is applied by
`QCUCRAM_ContentRepository` on demand, and the default administrator is created by
`QCUCRAM_AdminUserRepository.EnsureDefaultAdmin`, which `Global.asax.cs` calls on
`Application_Start`. There is therefore no `Database.SetInitializer` call and no
separate initializer class to register. Phase 1's note already flagged that
`QCU-CRAM_SeedOnStartup` was `false` pending this decision; it stays `false`, because
seeding now happens through the repositories rather than through an initializer.

## Notes for later phases

- `QCUCRAM_Db.P(...)` exists so callers can pass parameters without repeating
  `new SqlParameter(...)` at every call site. Prefer it over string concatenation —
  every repository query in this project is parameterised.
- `DeleteRoom` and `DeleteStudent` translate SQL error 547 into `-1`. Phase 8 relies on
  that to tell the administrator "this room still has reservations" instead of showing a
  raw SQL exception.
- `SetSetting` invalidates `QCUCRAM_Clock`, so changing `DevTimeOffsetMinutes` takes
  effect immediately rather than on the next request.
- `Search` on the student repository filters on student number, last name, first name,
  e-mail and requester type, and orders by last name then first name. It does **not**
  filter by program; callers that need a program filter apply it in memory so the
  ordering is preserved.
