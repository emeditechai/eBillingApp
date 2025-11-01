using System;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

class Program
{
    static void Main()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("Creating usp_SeatGuests Stored Procedure");
        Console.WriteLine("========================================");
        Console.WriteLine();

        // Read connection string from appsettings
        var basePath = "/Users/abhikporel/dev/Restaurantapp/RestaurantManagementSystem/RestaurantManagementSystem";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Error: Connection string not found in appsettings.json");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"Connection: {connectionString.Split(';')[0]}");
        Console.WriteLine();

        string sql = @"
IF OBJECT_ID('dbo.usp_SeatGuests', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_SeatGuests;

CREATE PROCEDURE dbo.usp_SeatGuests
    @TableId INT,
    @GuestName NVARCHAR(100),
    @PartySize INT,
    @UserId INT,
    @ReservationId INT = NULL,
    @WaitlistId INT = NULL,
    @Notes NVARCHAR(500) = NULL,
    @TargetTurnTimeMinutes INT = 90
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TurnoverId INT;
    
    IF NOT EXISTS (SELECT 1 FROM Tables WHERE Id = @TableId)
    BEGIN
        RAISERROR('Table does not exist.', 16, 1);
        RETURN -1;
    END
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        INSERT INTO TableTurnovers (
            TableId, ReservationId, WaitlistId, GuestName, PartySize,
            SeatedAt, Status, Notes, TargetTurnTimeMinutes
        ) VALUES (
            @TableId, @ReservationId, @WaitlistId, @GuestName, @PartySize,
            GETDATE(), 0, @Notes, @TargetTurnTimeMinutes
        );
        
        SET @TurnoverId = SCOPE_IDENTITY();
        
        UPDATE Tables SET Status = 2, LastOccupiedAt = GETDATE() WHERE Id = @TableId;
        
        IF @ReservationId IS NOT NULL
            UPDATE Reservations SET Status = 2, UpdatedAt = GETDATE() WHERE Id = @ReservationId;
        
        IF @WaitlistId IS NOT NULL
            UPDATE Waitlist SET Status = 2, SeatedAt = GETDATE() WHERE Id = @WaitlistId;
        
        COMMIT TRANSACTION;
        
        SELECT @TurnoverId AS TurnoverId;
        RETURN @TurnoverId;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
";

        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            
            Console.WriteLine("✓ Connected to database");
            Console.WriteLine("✓ Dropping existing procedure...");
            
            // Drop existing procedure first
            string dropSql = "IF OBJECT_ID('dbo.usp_SeatGuests', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_SeatGuests;";
            using (var dropCmd = new SqlCommand(dropSql, connection))
            {
                dropCmd.ExecuteNonQuery();
            }
            
            Console.WriteLine("✓ Creating new procedure...");
            Console.WriteLine();

            // Create new procedure
            string createSql = @"
CREATE PROCEDURE dbo.usp_SeatGuests
    @TableId INT,
    @GuestName NVARCHAR(100),
    @PartySize INT,
    @UserId INT,
    @ReservationId INT = NULL,
    @WaitlistId INT = NULL,
    @Notes NVARCHAR(500) = NULL,
    @TargetTurnTimeMinutes INT = 90
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TurnoverId INT;
    
    IF NOT EXISTS (SELECT 1 FROM Tables WHERE Id = @TableId)
    BEGIN
        RAISERROR('Table does not exist.', 16, 1);
        RETURN;
    END
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        INSERT INTO TableTurnovers (
            TableId, ReservationId, WaitlistId, GuestName, PartySize,
            SeatedAt, Status, Notes, TargetTurnTimeMinutes
        ) VALUES (
            @TableId, @ReservationId, @WaitlistId, @GuestName, @PartySize,
            GETDATE(), 0, @Notes, @TargetTurnTimeMinutes
        );
        
        SET @TurnoverId = SCOPE_IDENTITY();
        
        UPDATE Tables SET Status = 2, LastOccupiedAt = GETDATE() WHERE Id = @TableId;
        
        IF @ReservationId IS NOT NULL
            UPDATE Reservations SET Status = 2, UpdatedAt = GETDATE() WHERE Id = @ReservationId;
        
        IF @WaitlistId IS NOT NULL
            UPDATE Waitlist SET Status = 2, SeatedAt = GETDATE() WHERE Id = @WaitlistId;
        
        COMMIT TRANSACTION;
        
        SELECT @TurnoverId AS TurnoverId;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
";

            using (var createCmd = new SqlCommand(createSql, connection))
            {
                createCmd.ExecuteNonQuery();
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("========================================");
            Console.WriteLine("✅ SUCCESS!");
            Console.WriteLine("========================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Stored procedure 'usp_SeatGuests' created successfully!");
            Console.WriteLine();
            Console.WriteLine("You can now use the Bar Order Create page:");
            Console.WriteLine("→ https://localhost:7290/BOT/BarOrderCreate");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("========================================");
            Console.WriteLine("❌ ERROR");
            Console.WriteLine("========================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine(ex.Message);
            Console.WriteLine();
            Console.WriteLine("Stack trace:");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
