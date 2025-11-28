-- Create Email Templates Table for Birthday, Anniversary, and Custom Emails
USE dev_Restaurant;
GO

-- Create Email Templates table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_EmailTemplates')
BEGIN
    CREATE TABLE tbl_EmailTemplates (
        EmailTemplateID INT IDENTITY(1,1) PRIMARY KEY,
        TemplateName NVARCHAR(100) NOT NULL,
        TemplateType NVARCHAR(50) NOT NULL CHECK (TemplateType IN ('Birthday', 'Anniversary', 'Custom', 'Promotional')),
        Subject NVARCHAR(500) NOT NULL,
        BodyHtml NVARCHAR(MAX) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        IsDefault BIT NOT NULL DEFAULT 0,
        CreatedBy INT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedBy INT NULL,
        UpdatedAt DATETIME2 NULL,
        
        CONSTRAINT FK_EmailTemplates_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(Id),
        CONSTRAINT FK_EmailTemplates_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES Users(Id)
    );
    
    PRINT 'Table tbl_EmailTemplates created successfully';
END
ELSE
BEGIN
    PRINT 'Table tbl_EmailTemplates already exists';
END
GO

-- Create indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EmailTemplates_TemplateType' AND object_id = OBJECT_ID('tbl_EmailTemplates'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_EmailTemplates_TemplateType 
    ON tbl_EmailTemplates(TemplateType, IsActive);
    PRINT 'Index IX_EmailTemplates_TemplateType created';
END
GO

-- Create Email Campaign History table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_EmailCampaignHistory')
BEGIN
    CREATE TABLE tbl_EmailCampaignHistory (
        CampaignHistoryID INT IDENTITY(1,1) PRIMARY KEY,
        CampaignType NVARCHAR(50) NOT NULL,
        GuestId INT NOT NULL,
        GuestName NVARCHAR(255) NOT NULL,
        GuestEmail NVARCHAR(255) NOT NULL,
        EmailSubject NVARCHAR(500) NOT NULL,
        EmailBody NVARCHAR(MAX) NOT NULL,
        SentAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        Status NVARCHAR(20) NOT NULL CHECK (Status IN ('Success', 'Failed')),
        ErrorMessage NVARCHAR(MAX) NULL,
        ProcessingTimeMs INT NULL,
        SentBy INT NULL,
        
        CONSTRAINT FK_EmailCampaignHistory_SentBy FOREIGN KEY (SentBy) REFERENCES Users(Id)
    );
    
    PRINT 'Table tbl_EmailCampaignHistory created successfully';
END
ELSE
BEGIN
    PRINT 'Table tbl_EmailCampaignHistory already exists';
END
GO

-- Create indexes for campaign history
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EmailCampaignHistory_CampaignType' AND object_id = OBJECT_ID('tbl_EmailCampaignHistory'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_EmailCampaignHistory_CampaignType 
    ON tbl_EmailCampaignHistory(CampaignType, SentAt DESC);
    PRINT 'Index IX_EmailCampaignHistory_CampaignType created';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EmailCampaignHistory_GuestEmail' AND object_id = OBJECT_ID('tbl_EmailCampaignHistory'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_EmailCampaignHistory_GuestEmail 
    ON tbl_EmailCampaignHistory(GuestEmail, SentAt DESC);
    PRINT 'Index IX_EmailCampaignHistory_GuestEmail created';
END
GO

-- Insert default email templates
-- Birthday Template
IF NOT EXISTS (SELECT 1 FROM tbl_EmailTemplates WHERE TemplateType = 'Birthday' AND IsDefault = 1)
BEGIN
    INSERT INTO tbl_EmailTemplates (TemplateName, TemplateType, Subject, BodyHtml, IsActive, IsDefault)
    VALUES (
        'Default Birthday Template',
        'Birthday',
        '🎉 Happy Birthday {GuestName}! Special Gift from {RestaurantName}',
        N'<html>
<head>
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }
        .content { background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }
        .gift-box { background: white; border: 2px dashed #667eea; padding: 20px; margin: 20px 0; text-align: center; border-radius: 8px; }
        .footer { text-align: center; margin-top: 20px; font-size: 12px; color: #666; }
        .btn { display: inline-block; padding: 12px 30px; background: #667eea; color: white; text-decoration: none; border-radius: 5px; margin-top: 15px; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1 style="margin: 0; font-size: 32px;">🎂 Happy Birthday!</h1>
            <p style="margin: 10px 0 0 0; font-size: 18px;">Dear {GuestName}</p>
        </div>
        <div class="content">
            <p>We hope your special day is filled with happiness, laughter, and wonderful moments!</p>
            
            <div class="gift-box">
                <h2 style="color: #667eea; margin-top: 0;">🎁 Birthday Special Gift</h2>
                <p style="font-size: 18px; font-weight: bold; color: #764ba2;">Get 20% OFF on your next visit!</p>
                <p style="font-size: 14px; color: #666;">Valid for 7 days from your birthday</p>
                <p style="font-size: 12px; margin-top: 15px; color: #999;">Use code: <strong style="color: #667eea;">BDAY{Year}</strong></p>
            </div>
            
            <p>Thank you for being a valued guest at {RestaurantName}. We look forward to celebrating with you!</p>
            
            <p style="margin-top: 25px;">
                <strong>Warmest wishes,</strong><br>
                The {RestaurantName} Team
            </p>
            
            <div class="footer">
                <p>This email was sent from {RestaurantName}<br>
                If you wish to unsubscribe, please contact us.</p>
            </div>
        </div>
    </div>
</body>
</html>',
        1,
        1
    );
    PRINT 'Default Birthday template created';
END
GO

-- Anniversary Template
IF NOT EXISTS (SELECT 1 FROM tbl_EmailTemplates WHERE TemplateType = 'Anniversary' AND IsDefault = 1)
BEGIN
    INSERT INTO tbl_EmailTemplates (TemplateName, TemplateType, Subject, BodyHtml, IsActive, IsDefault)
    VALUES (
        'Default Anniversary Template',
        'Anniversary',
        '💝 Happy Anniversary {GuestName}! Celebrate with Us',
        N'<html>
<head>
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .header { background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }
        .content { background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }
        .offer-box { background: white; border: 2px solid #f5576c; padding: 20px; margin: 20px 0; text-align: center; border-radius: 8px; }
        .footer { text-align: center; margin-top: 20px; font-size: 12px; color: #666; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1 style="margin: 0; font-size: 32px;">💝 Happy Anniversary!</h1>
            <p style="margin: 10px 0 0 0; font-size: 18px;">Dear {GuestName}</p>
        </div>
        <div class="content">
            <p>Wishing you a wonderful anniversary filled with love and joy!</p>
            
            <div class="offer-box">
                <h2 style="color: #f5576c; margin-top: 0;">🌹 Anniversary Special</h2>
                <p style="font-size: 18px; font-weight: bold; color: #f093fb;">Complimentary Dessert for Two!</p>
                <p style="font-size: 14px; color: #666;">When you dine with us this week</p>
                <p style="font-size: 12px; margin-top: 15px; color: #999;">Mention this offer when making your reservation</p>
            </div>
            
            <p>Celebrate your special day at {RestaurantName}. We'd be honored to be part of your celebration!</p>
            
            <p style="margin-top: 25px;">
                <strong>With warm regards,</strong><br>
                The {RestaurantName} Team
            </p>
            
            <div class="footer">
                <p>This email was sent from {RestaurantName}<br>
                If you wish to unsubscribe, please contact us.</p>
            </div>
        </div>
    </div>
</body>
</html>',
        1,
        1
    );
    PRINT 'Default Anniversary template created';
END
GO

PRINT 'Email Templates setup completed successfully';
