/* ============================================================
   QCU-CRAM - OPTIONAL: copy your existing data across
   ============================================================
   Use this only if you already have data in the old database
   (Guades_QCUCram_DB) and you want it in the new one.

   It does NOT touch or delete the old database. It only reads
   from it. Run QCU-CRAM_Schema.sql first so QCU-CRAM_DB exists.

   Identity values are preserved, so foreign keys keep pointing
   at the same rows. Tables are copied parent-first. The script
   refuses to run twice (it stops if Students already has rows or if AdminUsers holds anything besides the untouched default admin),
   so it cannot duplicate anything.

   Everything below is ONE batch on purpose. A RETURN inside a
   batch stops that batch; if the guards were in their own batch
   separated by GO, a failed guard would still let the copy run.
   ============================================================ */

USE [QCU-CRAM_DB];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'Guades_QCUCram_DB')
BEGIN
    RAISERROR('The old database Guades_QCUCram_DB was not found on this server. Nothing to copy.', 16, 1);
    RETURN;
END

IF EXISTS (SELECT 1 FROM dbo.Students)
BEGIN
    RAISERROR('QCU-CRAM_DB already has data in Students. Copy aborted so nothing is duplicated.', 16, 1);
    RETURN;
END

/* AdminUsers guard. Application_Start runs EnsureDefaultAdmin(), so if the app was
   opened before this script the table holds ONE untouched default account
   (admin / Library Assistant). That row would collide with the copied admin IDs and
   usernames, so it - and only it - is removed below. Any other admin row means real
   data is present: abort instead of guessing. */
IF EXISTS (SELECT 1 FROM dbo.AdminUsers)
   AND NOT (    (SELECT COUNT(*) FROM dbo.AdminUsers) = 1
            AND EXISTS (SELECT 1 FROM dbo.AdminUsers WHERE Username = 'admin' AND FullName = 'Library Assistant'))
BEGIN
    RAISERROR('QCU-CRAM_DB already has administrator accounts other than the untouched default. Copy aborted so nothing is overwritten.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

/* Remove the auto-created default admin (see guard above). Old admins are copied in step 4. */
DELETE FROM dbo.AdminUsers;

/* ---- Clear the rows QCU-CRAM_Seed.sql put there ----
   The seed script inserts rooms 1-6, slots 1-9 and the five settings, which
   collide with the identity values coming across from the old database. Since
   Students is empty, nothing can reference these rows yet, so removing them is
   safe. This is what makes the copy script re-runnable after a seed. */
DELETE FROM dbo.Reservations;
DELETE FROM dbo.Rooms;
DELETE FROM dbo.TimeSlots;
DELETE FROM dbo.Settings;

/* ---- 1. Rooms ---- */
SET IDENTITY_INSERT dbo.Rooms ON;
INSERT INTO dbo.Rooms (RoomID, RoomName, MinCapacity, MaxCapacity, IsActive)
SELECT RoomID, RoomName, MinCapacity, MaxCapacity, IsActive
FROM Guades_QCUCram_DB.dbo.Rooms;
SET IDENTITY_INSERT dbo.Rooms OFF;

/* ---- 2. TimeSlots ---- */
SET IDENTITY_INSERT dbo.TimeSlots ON;
INSERT INTO dbo.TimeSlots (TimeSlotID, StartTime, EndTime, IsActive)
SELECT TimeSlotID, StartTime, EndTime, IsActive
FROM Guades_QCUCram_DB.dbo.TimeSlots;
SET IDENTITY_INSERT dbo.TimeSlots OFF;

/* ---- 3. Students ---- */
SET IDENTITY_INSERT dbo.Students ON;
INSERT INTO dbo.Students
    (StudentID, StudentNumber, LastName, FirstName, MI, Email, Program,
     RequesterType, PasswordHash, NoShowCount, IsRestricted, RestrictionLiftDate, CreatedAt)
SELECT StudentID, StudentNumber, LastName, FirstName, MI, Email, Program,
       RequesterType, PasswordHash, NoShowCount, IsRestricted, RestrictionLiftDate, CreatedAt
FROM Guades_QCUCram_DB.dbo.Students;
SET IDENTITY_INSERT dbo.Students OFF;

/* ---- 4. AdminUsers ---- */
SET IDENTITY_INSERT dbo.AdminUsers ON;
INSERT INTO dbo.AdminUsers (AdminID, Username, PasswordHash, FullName, IsActive)
SELECT AdminID, Username, PasswordHash, FullName, IsActive
FROM Guades_QCUCram_DB.dbo.AdminUsers;
SET IDENTITY_INSERT dbo.AdminUsers OFF;

/* ---- 5. Reservations ---- */
SET IDENTITY_INSERT dbo.Reservations ON;
INSERT INTO dbo.Reservations
    (ReservationID, StudentID, RoomID, TimeSlotID, ReservationDate, Purpose,
     NumberOfMembers, Status, CreatedAt, CheckedInAt)
SELECT ReservationID, StudentID, RoomID, TimeSlotID, ReservationDate, Purpose,
       NumberOfMembers, Status, CreatedAt, CheckedInAt
FROM Guades_QCUCram_DB.dbo.Reservations;
SET IDENTITY_INSERT dbo.Reservations OFF;

/* ---- 6. ReservationMembers ---- */
SET IDENTITY_INSERT dbo.ReservationMembers ON;
INSERT INTO dbo.ReservationMembers (MemberID, ReservationID, LastName, FirstName, MI)
SELECT MemberID, ReservationID, LastName, FirstName, MI
FROM Guades_QCUCram_DB.dbo.ReservationMembers;
SET IDENTITY_INSERT dbo.ReservationMembers OFF;

/* ---- 7. ReservationStatusHistory ---- */
SET IDENTITY_INSERT dbo.ReservationStatusHistory ON;
INSERT INTO dbo.ReservationStatusHistory
    (HistoryID, ReservationID, OldStatus, NewStatus, ChangedByRole, ChangedBy, ChangedAt, Notes)
SELECT HistoryID, ReservationID, OldStatus, NewStatus, ChangedByRole, ChangedBy, ChangedAt, Notes
FROM Guades_QCUCram_DB.dbo.ReservationStatusHistory;
SET IDENTITY_INSERT dbo.ReservationStatusHistory OFF;

/* ---- 8. Notifications ---- */
SET IDENTITY_INSERT dbo.Notifications ON;
INSERT INTO dbo.Notifications
    (NotificationID, StudentID, ReservationID, Message, IsRead, CreatedAt)
SELECT NotificationID, StudentID, ReservationID, Message, IsRead, CreatedAt
FROM Guades_QCUCram_DB.dbo.Notifications;
SET IDENTITY_INSERT dbo.Notifications OFF;

/* ---- 9. Settings ---- */
INSERT INTO dbo.Settings (SettingKey, SettingValue, Description)
SELECT s.SettingKey, s.SettingValue, s.Description
FROM Guades_QCUCram_DB.dbo.Settings s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Settings t WHERE t.SettingKey = s.SettingKey);

/* ---- Re-seed every identity so new inserts do not collide ----
   After SET IDENTITY_INSERT the current identity value stays where it was,
   which would make the next INSERT reuse an ID that now belongs to a copied
   row. DBCC CHECKIDENT with no reseed value moves it to MAX(id). */
DBCC CHECKIDENT ('dbo.Rooms', RESEED) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.TimeSlots', RESEED) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.Students', RESEED) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.AdminUsers', RESEED) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.Reservations', RESEED) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.ReservationMembers', RESEED) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.ReservationStatusHistory', RESEED) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.Notifications', RESEED) WITH NO_INFOMSGS;

COMMIT TRANSACTION;

/* ---- Verify: new vs old row counts (Match must be 1 for every table) ---- */
SELECT 'Rooms' AS TableName,
       (SELECT COUNT(*) FROM dbo.Rooms) AS NewCount,
       (SELECT COUNT(*) FROM Guades_QCUCram_DB.dbo.Rooms) AS OldCount
INTO #CopyCheck
UNION ALL SELECT 'TimeSlots', (SELECT COUNT(*) FROM dbo.TimeSlots), (SELECT COUNT(*) FROM Guades_QCUCram_DB.dbo.TimeSlots)
UNION ALL SELECT 'Students', (SELECT COUNT(*) FROM dbo.Students), (SELECT COUNT(*) FROM Guades_QCUCram_DB.dbo.Students)
UNION ALL SELECT 'AdminUsers', (SELECT COUNT(*) FROM dbo.AdminUsers), (SELECT COUNT(*) FROM Guades_QCUCram_DB.dbo.AdminUsers)
UNION ALL SELECT 'Reservations', (SELECT COUNT(*) FROM dbo.Reservations), (SELECT COUNT(*) FROM Guades_QCUCram_DB.dbo.Reservations)
UNION ALL SELECT 'ReservationMembers', (SELECT COUNT(*) FROM dbo.ReservationMembers), (SELECT COUNT(*) FROM Guades_QCUCram_DB.dbo.ReservationMembers)
UNION ALL SELECT 'ReservationStatusHistory', (SELECT COUNT(*) FROM dbo.ReservationStatusHistory), (SELECT COUNT(*) FROM Guades_QCUCram_DB.dbo.ReservationStatusHistory)
UNION ALL SELECT 'Notifications', (SELECT COUNT(*) FROM dbo.Notifications), (SELECT COUNT(*) FROM Guades_QCUCram_DB.dbo.Notifications)
UNION ALL SELECT 'Settings', (SELECT COUNT(*) FROM dbo.Settings), (SELECT COUNT(*) FROM Guades_QCUCram_DB.dbo.Settings);

SELECT TableName, NewCount, OldCount,
       CASE WHEN NewCount = OldCount THEN 1 ELSE 0 END AS Match
FROM #CopyCheck;
DROP TABLE #CopyCheck;
GO
