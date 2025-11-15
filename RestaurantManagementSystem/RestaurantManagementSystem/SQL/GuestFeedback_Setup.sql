-- Guest Feedback Setup Script
-- Creates GuestFeedback table if not exists
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GuestFeedback')
BEGIN
    CREATE TABLE [dbo].[GuestFeedback] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [VisitDate] DATE NOT NULL DEFAULT CAST(GETDATE() AS DATE),
        [OverallRating] TINYINT NOT NULL CHECK ([OverallRating] BETWEEN 1 AND 5),
        [FoodRating] TINYINT NULL CHECK ([FoodRating] BETWEEN 1 AND 5),
        [ServiceRating] TINYINT NULL CHECK ([ServiceRating] BETWEEN 1 AND 5),
        [CleanlinessRating] TINYINT NULL CHECK ([CleanlinessRating] BETWEEN 1 AND 5),
        [StaffRating] TINYINT NULL CHECK ([StaffRating] BETWEEN 1 AND 5),
        [AmbienceRating] TINYINT NULL CHECK ([AmbienceRating] BETWEEN 1 AND 5),
        [ValueRating] TINYINT NULL CHECK ([ValueRating] BETWEEN 1 AND 5),
        [SpeedRating] TINYINT NULL CHECK ([SpeedRating] BETWEEN 1 AND 5),
    [Location] NVARCHAR(100) NULL,
    [IsFirstVisit] BIT NULL,
    [SurveyJson] NVARCHAR(MAX) NULL,
        [Tags] NVARCHAR(200) NULL, -- Comma-separated quick tag selections
        [Comments] NVARCHAR(1000) NULL,
        [GuestName] NVARCHAR(100) NULL,
        [Email] NVARCHAR(150) NULL,
        [Phone] NVARCHAR(30) NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE()
    );
    PRINT 'GuestFeedback table created.';
END
ELSE
BEGIN
    PRINT 'GuestFeedback table already exists.';
    -- Add new rating columns if missing
    IF COL_LENGTH('GuestFeedback','AmbienceRating') IS NULL
        ALTER TABLE GuestFeedback ADD [AmbienceRating] TINYINT NULL CHECK ([AmbienceRating] BETWEEN 1 AND 5);
    IF COL_LENGTH('GuestFeedback','ValueRating') IS NULL
        ALTER TABLE GuestFeedback ADD [ValueRating] TINYINT NULL CHECK ([ValueRating] BETWEEN 1 AND 5);
    IF COL_LENGTH('GuestFeedback','SpeedRating') IS NULL
        ALTER TABLE GuestFeedback ADD [SpeedRating] TINYINT NULL CHECK ([SpeedRating] BETWEEN 1 AND 5);
    IF COL_LENGTH('GuestFeedback','Location') IS NULL
        ALTER TABLE GuestFeedback ADD [Location] NVARCHAR(100) NULL;
    IF COL_LENGTH('GuestFeedback','IsFirstVisit') IS NULL
        ALTER TABLE GuestFeedback ADD [IsFirstVisit] BIT NULL;
    IF COL_LENGTH('GuestFeedback','SurveyJson') IS NULL
        ALTER TABLE GuestFeedback ADD [SurveyJson] NVARCHAR(MAX) NULL;
END
GO

-- Submit Feedback Procedure
IF OBJECT_ID('usp_SubmitGuestFeedback','P') IS NOT NULL
    DROP PROCEDURE usp_SubmitGuestFeedback;
GO
CREATE PROCEDURE [dbo].[usp_SubmitGuestFeedback]
    @VisitDate DATE = NULL,
    @OverallRating TINYINT,
    @FoodRating TINYINT = NULL,
    @ServiceRating TINYINT = NULL,
    @CleanlinessRating TINYINT = NULL,
    @StaffRating TINYINT = NULL,
    @AmbienceRating TINYINT = NULL,
    @ValueRating TINYINT = NULL,
    @SpeedRating TINYINT = NULL,
    @Location NVARCHAR(100) = NULL,
    @IsFirstVisit BIT = NULL,
    @SurveyJson NVARCHAR(MAX) = NULL,
    @Tags NVARCHAR(200) = NULL,
    @Comments NVARCHAR(1000) = NULL,
    @GuestName NVARCHAR(100) = NULL,
    @Email NVARCHAR(150) = NULL,
    @Phone NVARCHAR(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @OverallRating NOT BETWEEN 1 AND 5
    BEGIN
        RAISERROR('OverallRating must be 1-5',16,1);
        RETURN;
    END
    INSERT INTO GuestFeedback(
        VisitDate, OverallRating, FoodRating, ServiceRating, CleanlinessRating, StaffRating,
        AmbienceRating, ValueRating, SpeedRating,
        Location, IsFirstVisit, SurveyJson,
        Tags, Comments, GuestName, Email, Phone)
    VALUES(
        ISNULL(@VisitDate, CAST(GETDATE() AS DATE)), @OverallRating, @FoodRating, @ServiceRating, @CleanlinessRating, @StaffRating,
        @AmbienceRating, @ValueRating, @SpeedRating,
        @Location, @IsFirstVisit, @SurveyJson,
        @Tags, @Comments, @GuestName, @Email, @Phone);
    SELECT SCOPE_IDENTITY() AS NewId;
END
GO

-- Feedback Summary Procedure (aggregates + latest items)
IF OBJECT_ID('usp_GetGuestFeedbackSummary','P') IS NOT NULL
    DROP PROCEDURE usp_GetGuestFeedbackSummary;
GO
CREATE PROCEDURE [dbo].[usp_GetGuestFeedbackSummary]
    @FromDate DATE = NULL,
    @ToDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Start DATE = ISNULL(@FromDate, DATEADD(DAY,-30,CAST(GETDATE() AS DATE)));
    DECLARE @End DATE = ISNULL(@ToDate, CAST(GETDATE() AS DATE));

    -- Aggregated ratings
    SELECT 
        TotalFeedback = COUNT(*),
        AvgOverall = ROUND(AVG(CAST(OverallRating AS FLOAT)),2),
        AvgFood = ROUND(AVG(CAST(FoodRating AS FLOAT)),2),
        AvgService = ROUND(AVG(CAST(ServiceRating AS FLOAT)),2),
        AvgCleanliness = ROUND(AVG(CAST(CleanlinessRating AS FLOAT)),2),
        AvgStaff = ROUND(AVG(CAST(StaffRating AS FLOAT)),2),
        AvgAmbience = ROUND(AVG(CAST(AmbienceRating AS FLOAT)),2),
        AvgValue = ROUND(AVG(CAST(ValueRating AS FLOAT)),2),
        AvgSpeed = ROUND(AVG(CAST(SpeedRating AS FLOAT)),2)
    FROM GuestFeedback
    WHERE VisitDate BETWEEN @Start AND @End;

    -- Latest 50 entries
    SELECT TOP 50 Id, VisitDate, OverallRating, FoodRating, ServiceRating, CleanlinessRating, StaffRating,
        AmbienceRating, ValueRating, SpeedRating,
        Location, IsFirstVisit, SurveyJson,
        Tags, Comments, GuestName, CreatedAt
    FROM GuestFeedback
    WHERE VisitDate BETWEEN @Start AND @End
    ORDER BY CreatedAt DESC;
END
GO
