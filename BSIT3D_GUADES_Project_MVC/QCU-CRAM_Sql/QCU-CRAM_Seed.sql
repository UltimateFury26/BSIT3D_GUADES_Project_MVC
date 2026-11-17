/* ============================================================
   QCU-CRAM - seed data
   Six rooms, nine time slots (08:00-17:00) and the five settings.
   Safe to re-run: every insert is guarded, so running it twice
   will not create duplicates or throw a unique-key error.
   ============================================================ */

USE [QCU-CRAM_DB];
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Rooms)
BEGIN
    INSERT INTO dbo.Rooms (RoomName, MinCapacity, MaxCapacity, IsActive) VALUES
     ('Collab Room 1', 5, 10, 1),
     ('Collab Room 2', 5, 10, 1),
     ('Collab Room 3', 5, 10, 1),
     ('Collab Room 4', 5, 10, 1),
     ('Collab Room 5', 5, 10, 1),
     ('Collab Room 6', 5, 10, 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.TimeSlots)
BEGIN
    INSERT INTO dbo.TimeSlots (StartTime, EndTime, IsActive) VALUES
     ('08:00','09:00',1),('09:00','10:00',1),('10:00','11:00',1),
     ('11:00','12:00',1),('12:00','13:00',1),('13:00','14:00',1),
     ('14:00','15:00',1),('15:00','16:00',1),('16:00','17:00',1);
END
GO

/* Settings are inserted one key at a time so a partially populated table
   still ends up complete without overwriting a value you changed by hand. */
IF NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE SettingKey = 'GraceMinutes')
    INSERT INTO dbo.Settings (SettingKey, SettingValue, Description) VALUES
     ('GraceMinutes','15','Minutes after slot start before an unclaimed booking is forfeited');

IF NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE SettingKey = 'NoShowThreshold')
    INSERT INTO dbo.Settings (SettingKey, SettingValue, Description) VALUES
     ('NoShowThreshold','3','Forfeitures before a student is restricted');

IF NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE SettingKey = 'RestrictionDays')
    INSERT INTO dbo.Settings (SettingKey, SettingValue, Description) VALUES
     ('RestrictionDays','7','Days a restriction lasts');

IF NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE SettingKey = 'AdvanceBookingDays')
    INSERT INTO dbo.Settings (SettingKey, SettingValue, Description) VALUES
     ('AdvanceBookingDays','1','Booking must be exactly this many days ahead');

IF NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE SettingKey = 'DevTimeOffsetMinutes')
    INSERT INTO dbo.Settings (SettingKey, SettingValue, Description) VALUES
     ('DevTimeOffsetMinutes','0','TESTING ONLY: shifts system clock by this many minutes');
GO
