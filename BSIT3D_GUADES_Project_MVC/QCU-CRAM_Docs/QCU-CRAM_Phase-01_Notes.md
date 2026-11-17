# QCU-CRAM Phase 1 Notes

- Date: 2026-11-18
- Connection string name: `QCU-CRAM_Connection` (server `localhost\SQLEXPRESS`, DB `QCU-CRAM_DB`) - edit in Web.config if your instance differs.
- `QCU-CRAM_SeedOnStartup` = false (Phase 2/3 only).
- `Database.SetInitializer` and `QCUCRAMDbInitializer` calls in Global.asax.cs are commented out until Phase 2, because `QCUCRAMContext` does not exist yet.
- Delete each `.keep` once its folder has a real file.
