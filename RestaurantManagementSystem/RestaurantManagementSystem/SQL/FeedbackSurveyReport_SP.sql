-- =============================================
-- Feedback Survey Report Stored Procedure
-- Retrieves detailed feedback survey data with all ratings and survey responses
-- =============================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'usp_GetFeedbackSurveyReport')
    DROP PROCEDURE usp_GetFeedbackSurveyReport
GO

CREATE PROCEDURE usp_GetFeedbackSurveyReport
    @FromDate DATE = NULL,
    @ToDate DATE = NULL,
    @Location NVARCHAR(100) = NULL,
    @MinRating INT = NULL,
    @MaxRating INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Set default dates if not provided
    IF @FromDate IS NULL
        SET @FromDate = DATEADD(MONTH, -1, GETDATE())
    
    IF @ToDate IS NULL
        SET @ToDate = GETDATE()

    -- Main feedback survey report query
    SELECT 
        gf.Id,
        gf.VisitDate,
        gf.Location,
        gf.IsFirstVisit,
        gf.OverallRating,
        gf.FoodRating,
        gf.ServiceRating,
        gf.CleanlinessRating,
        gf.StaffRating,
        gf.AmbienceRating,
        gf.ValueRating,
        gf.SpeedRating,
        gf.Tags,
        gf.Comments,
        gf.GuestName,
        gf.Email,
        gf.Phone,
    gf.GuestBirthDate,
    gf.AnniversaryDate,
        gf.SurveyJson,
        gf.CreatedAt,
        -- Calculate average of all ratings
        CAST((ISNULL(gf.OverallRating, 0) + 
              ISNULL(gf.FoodRating, 0) + 
              ISNULL(gf.ServiceRating, 0) + 
              ISNULL(gf.CleanlinessRating, 0) + 
              ISNULL(gf.StaffRating, 0) + 
              ISNULL(gf.AmbienceRating, 0) + 
              ISNULL(gf.ValueRating, 0) + 
              ISNULL(gf.SpeedRating, 0)) / 8.0 AS DECIMAL(3,2)) AS AverageRating,
        -- Count how many survey questions were answered
        CASE 
            WHEN gf.SurveyJson IS NOT NULL AND gf.SurveyJson != '' AND gf.SurveyJson != '{}' 
            THEN LEN(gf.SurveyJson) - LEN(REPLACE(gf.SurveyJson, ',', '')) + 1
            ELSE 0
        END AS SurveyQuestionsAnswered
    FROM GuestFeedback gf
    WHERE gf.VisitDate BETWEEN @FromDate AND @ToDate
        AND (@Location IS NULL OR gf.Location = @Location)
        AND (@MinRating IS NULL OR gf.OverallRating >= @MinRating)
        AND (@MaxRating IS NULL OR gf.OverallRating <= @MaxRating)
    ORDER BY gf.VisitDate DESC, gf.CreatedAt DESC;

    -- Summary statistics
    SELECT 
        COUNT(*) AS TotalFeedback,
        COUNT(DISTINCT Location) AS UniqueLocations,
        COUNT(DISTINCT GuestName) AS UniqueGuests,
        SUM(CASE WHEN IsFirstVisit = 1 THEN 1 ELSE 0 END) AS FirstTimeVisitors,
        SUM(CASE WHEN IsFirstVisit = 0 THEN 1 ELSE 0 END) AS ReturningVisitors,
        CAST(AVG(CAST(OverallRating AS FLOAT)) AS DECIMAL(3,2)) AS AvgOverallRating,
        CAST(AVG(CAST(FoodRating AS FLOAT)) AS DECIMAL(3,2)) AS AvgFoodRating,
        CAST(AVG(CAST(ServiceRating AS FLOAT)) AS DECIMAL(3,2)) AS AvgServiceRating,
        CAST(AVG(CAST(CleanlinessRating AS FLOAT)) AS DECIMAL(3,2)) AS AvgCleanlinessRating,
        CAST(AVG(CAST(StaffRating AS FLOAT)) AS DECIMAL(3,2)) AS AvgStaffRating,
        CAST(AVG(CAST(AmbienceRating AS FLOAT)) AS DECIMAL(3,2)) AS AvgAmbienceRating,
        CAST(AVG(CAST(ValueRating AS FLOAT)) AS DECIMAL(3,2)) AS AvgValueRating,
        CAST(AVG(CAST(SpeedRating AS FLOAT)) AS DECIMAL(3,2)) AS AvgSpeedRating,
        SUM(CASE WHEN SurveyJson IS NOT NULL AND SurveyJson != '' AND SurveyJson != '{}' THEN 1 ELSE 0 END) AS FeedbackWithSurvey,
        MIN(VisitDate) AS EarliestFeedback,
        MAX(VisitDate) AS LatestFeedback
    FROM GuestFeedback
    WHERE VisitDate BETWEEN @FromDate AND @ToDate
        AND (@Location IS NULL OR Location = @Location)
        AND (@MinRating IS NULL OR OverallRating >= @MinRating)
        AND (@MaxRating IS NULL OR OverallRating <= @MaxRating);

    -- Rating distribution
    SELECT 
        OverallRating AS Rating,
        COUNT(*) AS Count,
        CAST(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM GuestFeedback WHERE VisitDate BETWEEN @FromDate AND @ToDate) AS DECIMAL(5,2)) AS Percentage
    FROM GuestFeedback
    WHERE VisitDate BETWEEN @FromDate AND @ToDate
        AND (@Location IS NULL OR Location = @Location)
        AND (@MinRating IS NULL OR OverallRating >= @MinRating)
        AND (@MaxRating IS NULL OR OverallRating <= @MaxRating)
    GROUP BY OverallRating
    ORDER BY OverallRating DESC;

    -- Top tags
    SELECT TOP 10
        value AS Tag,
        COUNT(*) AS Count
    FROM GuestFeedback
    CROSS APPLY STRING_SPLIT(Tags, ',')
    WHERE VisitDate BETWEEN @FromDate AND @ToDate
        AND Tags IS NOT NULL 
        AND Tags != ''
        AND (@Location IS NULL OR Location = @Location)
        AND (@MinRating IS NULL OR OverallRating >= @MinRating)
        AND (@MaxRating IS NULL OR OverallRating <= @MaxRating)
    GROUP BY value
    ORDER BY COUNT(*) DESC;
END
GO

PRINT 'Stored Procedure usp_GetFeedbackSurveyReport created successfully'
GO
