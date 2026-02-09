-- Adds IsCounterRequired column to dbo.RestaurantSettings if it does not exist

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'RestaurantSettings'
      AND COLUMN_NAME = 'IsCounterRequired'
)
BEGIN
    ALTER TABLE [dbo].[RestaurantSettings]
    ADD [IsCounterRequired] BIT NOT NULL CONSTRAINT DF_RestaurantSettings_IsCounterRequired DEFAULT(0);

    -- Backfill safety
    UPDATE [dbo].[RestaurantSettings]
    SET [IsCounterRequired] = ISNULL([IsCounterRequired], 0);
END
