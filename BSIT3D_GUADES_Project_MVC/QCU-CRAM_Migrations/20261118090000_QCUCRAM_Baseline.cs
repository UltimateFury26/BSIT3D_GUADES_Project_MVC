namespace BSIT3D_GUADES_Project_MVC.QCUCRAM_Migrations
{
    using System.Data.Entity.Migrations;

    public partial class QCUCRAM_Baseline : DbMigration
    {
        public override void Up()
        {
            // The nine tables already exist (QCU-CRAM_Schema.sql).
            // NOTE: this class has no .Designer.cs (IMigrationMetadata), so EF does not
            // discover or run it; it is inert documentation of the SQL-owned objects below.
            // Every statement is guarded (IF NOT EXISTS) and nothing here creates tables.
            // The objects below cannot be expressed by EF 6 Fluent API, so they
            // are re-asserted here for a database rebuilt from migrations.

            Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Rooms_Capacity')
    ALTER TABLE dbo.Rooms ADD CONSTRAINT CK_Rooms_Capacity
        CHECK (MinCapacity > 0 AND MaxCapacity >= MinCapacity);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TimeSlots_Range')
    ALTER TABLE dbo.TimeSlots ADD CONSTRAINT CK_TimeSlots_Range
        CHECK (EndTime > StartTime);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Students_Type')
    ALTER TABLE dbo.Students ADD CONSTRAINT CK_Students_Type
        CHECK (RequesterType IN ('Student','Faculty','Visitor'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Res_Status')
    ALTER TABLE dbo.Reservations ADD CONSTRAINT CK_Res_Status
        CHECK (Status IN ('Pending','Approved','Reassigned','Declined','Cancelled','CheckedIn','Completed','Forfeited'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Res_Members')
    ALTER TABLE dbo.Reservations ADD CONSTRAINT CK_Res_Members
        CHECK (NumberOfMembers BETWEEN 5 AND 10);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Hist_Role')
    ALTER TABLE dbo.ReservationStatusHistory ADD CONSTRAINT CK_Hist_Role
        CHECK (ChangedByRole IN ('Admin','Student','System'));

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Res_ActiveSlot' AND object_id = OBJECT_ID('dbo.Reservations'))
    CREATE UNIQUE INDEX UX_Res_ActiveSlot
        ON dbo.Reservations (RoomID, ReservationDate, TimeSlotID)
        WHERE Status <> 'Declined' AND Status <> 'Cancelled' AND Status <> 'Forfeited';
");
        }

        public override void Down()
        {
            Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Res_ActiveSlot' AND object_id = OBJECT_ID('dbo.Reservations'))
    DROP INDEX UX_Res_ActiveSlot ON dbo.Reservations;
");
        }
    }
}
