# QCU-CRAM — Database Setup

The database is named **`QCU-CRAM_DB`**. It is created and owned by these scripts, not by
the application: `Global.asax.cs` calls `Database.SetInitializer<QCUCRAMContext>(null)`,
which tells Entity Framework to never create, drop, migrate or seed anything.

Because the name contains a hyphen, it must always be written in brackets in T-SQL —
`[QCU-CRAM_DB]`. The connection string does not need brackets.

## Run order

| # | Script | What it does | Required? |
| --- | --- | --- | --- |
| 1 | `QCU-CRAM_Schema.sql` | Creates `[QCU-CRAM_DB]` **only if it does not already exist**, aborts cleanly if the tables already exist, then creates the nine tables, all six CHECK constraints, and the filtered unique index `UX_Res_ActiveSlot` | **Yes** |
| 2 | `QCU-CRAM_Seed.sql` | Six rooms, nine time slots (08:00–17:00), five settings rows. Every insert is guarded, so it is safe to re-run | **Yes** |
| 3 | `QCU-CRAM_CopyFromOldDb.sql` | **Optional.** Copies your data out of the legacy source database into `[QCU-CRAM_DB]`. Reads only — it never modifies or deletes the old database | Only if you have data to keep |
| 4 | `QCU-CRAM_TestChecks.sql` | Asserts the constraints actually exist and bite; safe to re-run | Optional |
| 5 | `QCU-CRAM_Analytics.sql` | The reporting queries behind Phase 9; run ad hoc | Optional |

```
sqlcmd -S localhost\SQLEXPRESS -E -i QCU-CRAM_Schema.sql
sqlcmd -S localhost\SQLEXPRESS -E -i QCU-CRAM_Seed.sql
sqlcmd -S localhost\SQLEXPRESS -E -i QCU-CRAM_CopyFromOldDb.sql   -- only if keeping old data
```

`-E` means Windows authentication, which matches `Trusted_Connection=True` in the
connection string.

## Your old database is left alone

The legacy source database is not touched by any of these scripts except by
`QCU-CRAM_CopyFromOldDb.sql`, which only issues `SELECT`s against it. Nothing drops,
truncates or renames it. You can keep it as a fallback, compare the two, or delete it
yourself later.

If you want to copy the data across, run the schema and seed first, then the copy script.
It preserves identity values so foreign keys still line up, copies parent tables before
child tables, wraps everything in one transaction, and aborts if `Students` already has
rows — so it cannot run twice and duplicate anything. Because the seed script already put
rooms 1–6, slots 1–9 and the five settings in place, the copy script deletes those three
tables first (safe: with `Students` empty nothing can reference them) and then copies the
old rows in with their original IDs. It also re-seeds every identity afterwards, so the
next insert does not reuse an ID that now belongs to a copied row. It finishes by printing
a row count per table so you can confirm the totals match the old database.

If you would rather start clean, skip the copy script entirely.

## The default administrator

`admin` / `admin123` is **not** in the seed script. `Global.asax.cs` calls
`QCUCRAM_AdminUserRepository.EnsureDefaultAdmin()` on `Application_Start`, which creates
that account only if no administrator exists. Change the password after the first login.
If you ran the copy script and your old database already had administrators, they come
across. If the app was opened first, the copy script removes that untouched default
`admin` row (and only that row) before copying; any other existing admin row makes it abort.

## Changing the connection string

The only place the database is configured is `Web.config`:

```xml
<add name="QCU-CRAM_Connection"
     connectionString="Server=localhost\SQLEXPRESS;Database=QCU-CRAM_DB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
     providerName="System.Data.SqlClient" />
```

`QCUCRAMContext` resolves it by name (`base("name=QCU-CRAM_Connection")`), and
`QCUCRAM_Db` reads the same name, so editing this one entry is enough. If your SQL
instance is not `localhost\SQLEXPRESS`, change `Server=` too.

## About the baseline migration

`QCU-CRAM_Migrations/20261118090000_QCUCRAM_Baseline.cs` deliberately contains **no
`CreateTable` calls**. It has no `.Designer.cs` (the `IMigrationMetadata` half EF needs), so
EF does **not** discover or run it: it is inert and is not recorded in `__MigrationHistory`.
The SQL scripts own the schema; `Database.SetInitializer<QCUCRAMContext>(null)` and
`AutomaticMigrationsEnabled = false` mean EF never creates, drops or alters anything.
Do not run `Update-Database` against `QCU-CRAM_DB`. If you later want a real baseline row,
generate it in Visual Studio with `Add-Migration QCUCRAM_Baseline -IgnoreChanges` against a
disposable database first, then review it.
