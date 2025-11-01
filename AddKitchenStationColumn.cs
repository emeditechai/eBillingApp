using System;
using Microsoft.Data.SqlClient;

class AddKitchenStationColumn
{
    static void Main(string[] args)
    {
        string connectionString = "Server=tcp:198.38.81.123,1433;Database=dev_Restaurant;User Id=sa;Password=asdf@1234;Encrypt=False;TrustServerCertificate=False;Connection Timeout=60;";
        
        string sql = @"
-- Add KitchenStation column to KitchenTickets if not exists
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.KitchenTickets') AND name = 'KitchenStation')
BEGIN
    ALTER TABLE dbo.KitchenTickets 
    ADD KitchenStation VARCHAR(50) NULL;
    
    PRINT 'Added KitchenStation column to KitchenTickets table';
    
    -- Update existing KOT tickets to have 'KITCHEN' station
    UPDATE dbo.KitchenTickets
    SET KitchenStation = 'KITCHEN'
    WHERE TicketNumber LIKE 'KOT-%';
    
    PRINT 'Updated existing KOT tickets with KITCHEN station';
END
ELSE
BEGIN
    PRINT 'KitchenStation column already exists in KitchenTickets table';
END

-- Create index for faster filtering by KitchenStation
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_KitchenTickets_KitchenStation' AND object_id = OBJECT_ID('dbo.KitchenTickets'))
BEGIN
    CREATE INDEX IX_KitchenTickets_KitchenStation ON dbo.KitchenTickets(KitchenStation);
    PRINT 'Created index IX_KitchenTickets_KitchenStation';
END
ELSE
BEGIN
    PRINT 'Index IX_KitchenTickets_KitchenStation already exists';
END

PRINT 'KitchenStation field setup complete';
";

        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                Console.WriteLine("Connected to database successfully.");
                
                // Split by GO statements and execute each batch
                string[] batches = sql.Split(new[] { "\nGO\n", "\nGO\r\n", "\r\nGO\r\n", "\r\nGO\n" }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (string batch in batches)
                {
                    if (string.IsNullOrWhiteSpace(batch))
                        continue;
                        
                    using (SqlCommand command = new SqlCommand(batch, connection))
                    {
                        command.InfoMessage += (sender, e) => Console.WriteLine(e.Message);
                        connection.InfoMessage += (sender, e) => Console.WriteLine(e.Message);
                        
                        command.ExecuteNonQuery();
                    }
                }
                
                Console.WriteLine("\n✓ KitchenStation column added successfully!");
                Console.WriteLine("✓ Existing KOT tickets updated with KITCHEN station");
                Console.WriteLine("✓ Index created for better performance");
                Console.WriteLine("\nNow BOT tickets will be generated with prefix 'BOT-' and KitchenStation='BAR'");
                Console.WriteLine("KOT tickets will continue with prefix 'KOT-' and KitchenStation='KITCHEN'");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
