-- Quick Verification Script for SurveyJson Feature
-- Run this after submitting feedback to verify data flow

PRINT '=== CHECKING GUESTFEEDBACK TABLE STRUCTURE ==='
GO

-- Check if SurveyJson column exists
IF COL_LENGTH('GuestFeedback', 'SurveyJson') IS NOT NULL
BEGIN
    PRINT '✓ SurveyJson column EXISTS'
    
    SELECT 
        COLUMN_NAME,
        DATA_TYPE,
        CHARACTER_MAXIMUM_LENGTH,
        IS_NULLABLE
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'GuestFeedback' 
    AND COLUMN_NAME = 'SurveyJson'
END
ELSE
BEGIN
    PRINT '✗ SurveyJson column DOES NOT EXIST - Run GuestFeedback_Setup.sql!'
END
GO

PRINT ''
PRINT '=== CHECKING STORED PROCEDURE PARAMETERS ==='
GO

-- Check if usp_SubmitGuestFeedback has SurveyJson parameter
IF EXISTS (
    SELECT 1 
    FROM sys.parameters 
    WHERE object_id = OBJECT_ID('dbo.usp_SubmitGuestFeedback')
    AND name = '@SurveyJson'
)
BEGIN
    PRINT '✓ usp_SubmitGuestFeedback HAS @SurveyJson parameter'
    
    -- Show all parameters
    SELECT 
        REPLACE(name, '@', '') as ParameterName,
        TYPE_NAME(user_type_id) as DataType,
        max_length
    FROM sys.parameters
    WHERE object_id = OBJECT_ID('dbo.usp_SubmitGuestFeedback')
    ORDER BY parameter_id
END
ELSE
BEGIN
    PRINT '✗ usp_SubmitGuestFeedback MISSING @SurveyJson parameter - Run GuestFeedback_Setup.sql!'
END
GO

PRINT ''
PRINT '=== CHECKING RECENT FEEDBACK DATA ==='
GO

-- Show recent feedback with SurveyJson status
SELECT TOP 10
    Id,
    VisitDate,
    OverallRating,
    CASE 
        WHEN SurveyJson IS NULL THEN '✗ NULL'
        WHEN SurveyJson = '' THEN '✗ EMPTY'
        WHEN LEN(SurveyJson) < 3 THEN '✗ TOO SHORT'
        WHEN SurveyJson = '{}' THEN '⚠ EMPTY JSON'
        ELSE '✓ HAS DATA'
    END as SurveyJsonStatus,
    LEN(SurveyJson) as JsonLength,
    LEFT(SurveyJson, 100) as SurveyJsonPreview,
    Location,
    CASE WHEN IsFirstVisit = 1 THEN 'Yes' WHEN IsFirstVisit = 0 THEN 'No' ELSE '' END as FirstVisit,
    Tags,
    LEFT(Comments, 50) as CommentsPreview,
    GuestName,
    CreatedAt
FROM GuestFeedback
ORDER BY CreatedAt DESC
GO

PRINT ''
PRINT '=== SUMMARY STATISTICS ==='
GO

SELECT 
    COUNT(*) as TotalFeedback,
    SUM(CASE WHEN SurveyJson IS NOT NULL AND LEN(SurveyJson) > 2 THEN 1 ELSE 0 END) as WithSurveyData,
    SUM(CASE WHEN SurveyJson IS NULL OR LEN(SurveyJson) <= 2 THEN 1 ELSE 0 END) as WithoutSurveyData,
    AVG(CAST(OverallRating AS FLOAT)) as AvgOverallRating
FROM GuestFeedback
GO

PRINT ''
PRINT '=== SAMPLE SURVEY RESPONSE (Latest with data) ==='
GO

SELECT TOP 1
    Id,
    VisitDate,
    SurveyJson
FROM GuestFeedback
WHERE SurveyJson IS NOT NULL 
AND LEN(SurveyJson) > 2
AND SurveyJson <> '{}'
ORDER BY CreatedAt DESC
GO

PRINT ''
PRINT '=== VERIFICATION COMPLETE ==='
PRINT ''
PRINT 'Expected Results:'
PRINT '✓ SurveyJson column should exist (NVARCHAR(MAX))'
PRINT '✓ SP parameter @SurveyJson should exist'
PRINT '✓ Recent feedback should show "✓ HAS DATA" for SurveyJsonStatus'
PRINT '✓ SurveyJsonPreview should show JSON like {"q1":5,"q2":4,...}'
PRINT ''
PRINT 'If any checks failed, run: GuestFeedback_Setup.sql'
PRINT 'Then restart the application and submit new feedback'
GO
