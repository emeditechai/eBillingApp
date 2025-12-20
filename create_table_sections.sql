-- Creates a master list of table sections/areas used by Reservation/TableForm dropdown.
-- Run this once on your SQL Server database.

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TableSections')
BEGIN
    CREATE TABLE TableSections (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL UNIQUE,
        SortOrder INT NOT NULL CONSTRAINT DF_TableSections_SortOrder DEFAULT 0,
        IsActive BIT NOT NULL CONSTRAINT DF_TableSections_IsActive DEFAULT 1,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_TableSections_CreatedAt DEFAULT GETDATE()
    );
END;

-- Optional seed from existing Tables.Section values
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Tables')
AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Tables' AND COLUMN_NAME = 'Section')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM TableSections)
    BEGIN
        INSERT INTO TableSections (Name, SortOrder, IsActive)
        SELECT DISTINCT LEFT(LTRIM(RTRIM(Section)), 50), 0, 1
        FROM Tables
        WHERE ISNULL(LTRIM(RTRIM(Section)),'') <> '';

        IF NOT EXISTS (SELECT 1 FROM TableSections WHERE Name = 'Main')
            INSERT INTO TableSections (Name, SortOrder, IsActive) VALUES ('Main', 0, 1);
    END
END;
