/* ============================================================
   QCU-CRAM - schema
   Creates the QCU-CRAM_DB database and its nine tables.
   Safe to run against a server that already has other databases:
   it only creates QCU-CRAM_DB when it does not already exist.
   ============================================================ */

IF DB_ID('QCU-CRAM_DB') IS NULL
BEGIN
    CREATE DATABASE [QCU-CRAM_DB];
END
GO

USE [QCU-CRAM_DB];
GO

/* Everything from here to the final GO is ONE batch so the RETURN below stops the
   whole script. If the tables already exist nothing is created or changed. */
IF OBJECT_ID('dbo.Rooms', 'U') IS NOT NULL
BEGIN
    RAISERROR('QCU-CRAM_DB already contains the QCU-CRAM tables. Schema script aborted; nothing was changed.', 16, 1);
    RETURN;
END

CREATE TABLE Rooms (
    RoomID        INT IDENTITY(1,1) PRIMARY KEY,
    RoomName      NVARCHAR(50) NOT NULL UNIQUE,
    MinCapacity   INT NOT NULL DEFAULT 5,
    MaxCapacity   INT NOT NULL DEFAULT 10,
    IsActive      BIT NOT NULL DEFAULT 1,
    CONSTRAINT CK_Rooms_Capacity CHECK (MinCapacity > 0 AND MaxCapacity >= MinCapacity)
);

CREATE TABLE TimeSlots (
    TimeSlotID    INT IDENTITY(1,1) PRIMARY KEY,
    StartTime     TIME(0) NOT NULL UNIQUE,
    EndTime       TIME(0) NOT NULL,
    IsActive      BIT NOT NULL DEFAULT 1,
    CONSTRAINT CK_TimeSlots_Range CHECK (EndTime > StartTime)
);

CREATE TABLE Students (
    StudentID           INT IDENTITY(1,1) PRIMARY KEY,
    StudentNumber       NVARCHAR(20)  NOT NULL UNIQUE,
    LastName            NVARCHAR(60)  NOT NULL,
    FirstName           NVARCHAR(60)  NOT NULL,
    MI                  NVARCHAR(5)   NULL,
    Email               NVARCHAR(120) NOT NULL UNIQUE,
    Program             NVARCHAR(100) NOT NULL,
    RequesterType       NVARCHAR(20)  NOT NULL,
    PasswordHash        NVARCHAR(200) NOT NULL,
    NoShowCount         INT NOT NULL DEFAULT 0,
    IsRestricted        BIT NOT NULL DEFAULT 0,
    RestrictionLiftDate DATETIME NULL,
    CreatedAt           DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT CK_Students_Type CHECK (RequesterType IN ('Student','Faculty','Visitor'))
);

CREATE TABLE AdminUsers (
    AdminID       INT IDENTITY(1,1) PRIMARY KEY,
    Username      NVARCHAR(50)  NOT NULL UNIQUE,
    PasswordHash  NVARCHAR(200) NOT NULL,
    FullName      NVARCHAR(120) NOT NULL,
    IsActive      BIT NOT NULL DEFAULT 1
);

CREATE TABLE Reservations (
    ReservationID   INT IDENTITY(1,1) PRIMARY KEY,
    StudentID       INT NOT NULL FOREIGN KEY REFERENCES Students(StudentID),
    RoomID          INT NOT NULL FOREIGN KEY REFERENCES Rooms(RoomID),
    TimeSlotID      INT NOT NULL FOREIGN KEY REFERENCES TimeSlots(TimeSlotID),
    ReservationDate DATE NOT NULL,
    Purpose         NVARCHAR(300) NOT NULL,
    NumberOfMembers INT NOT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    CreatedAt       DATETIME NOT NULL DEFAULT GETDATE(),
    CheckedInAt     DATETIME NULL,
    CONSTRAINT CK_Res_Status  CHECK (Status IN ('Pending','Approved','Reassigned','Declined','Cancelled','CheckedIn','Completed','Forfeited')),
    CONSTRAINT CK_Res_Members CHECK (NumberOfMembers BETWEEN 5 AND 10)
);

-- Safety net against double-booking: only ONE "active" reservation per room/date/slot
CREATE UNIQUE INDEX UX_Res_ActiveSlot
ON Reservations (RoomID, ReservationDate, TimeSlotID)
WHERE Status <> 'Declined' AND Status <> 'Cancelled' AND Status <> 'Forfeited';

CREATE INDEX IX_Res_Date    ON Reservations (ReservationDate);
CREATE INDEX IX_Res_Student ON Reservations (StudentID);

CREATE TABLE ReservationMembers (
    MemberID      INT IDENTITY(1,1) PRIMARY KEY,
    ReservationID INT NOT NULL FOREIGN KEY REFERENCES Reservations(ReservationID) ON DELETE CASCADE,
    LastName      NVARCHAR(60) NOT NULL,
    FirstName     NVARCHAR(60) NOT NULL,
    MI            NVARCHAR(5)  NULL
);

CREATE TABLE ReservationStatusHistory (
    HistoryID     INT IDENTITY(1,1) PRIMARY KEY,
    ReservationID INT NOT NULL FOREIGN KEY REFERENCES Reservations(ReservationID) ON DELETE CASCADE,
    OldStatus     NVARCHAR(20) NULL,
    NewStatus     NVARCHAR(20) NOT NULL,
    ChangedByRole NVARCHAR(10) NOT NULL,
    ChangedBy     INT NULL,
    ChangedAt     DATETIME NOT NULL DEFAULT GETDATE(),
    Notes         NVARCHAR(300) NULL,
    CONSTRAINT CK_Hist_Role CHECK (ChangedByRole IN ('Admin','Student','System'))
);

CREATE TABLE Notifications (
    NotificationID INT IDENTITY(1,1) PRIMARY KEY,
    StudentID      INT NOT NULL FOREIGN KEY REFERENCES Students(StudentID),
    ReservationID  INT NULL FOREIGN KEY REFERENCES Reservations(ReservationID) ON DELETE SET NULL,
    Message        NVARCHAR(400) NOT NULL,
    IsRead         BIT NOT NULL DEFAULT 0,
    CreatedAt      DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE Settings (
    SettingKey   NVARCHAR(50)  PRIMARY KEY,
    SettingValue NVARCHAR(100) NOT NULL,
    Description  NVARCHAR(200) NULL
);
GO