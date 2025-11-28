-- Insert Anniversary Template
USE dev_Restaurant;
GO

IF NOT EXISTS (SELECT 1 FROM tbl_EmailTemplates WHERE TemplateType = 'Anniversary' AND IsDefault = 1)
BEGIN
    INSERT INTO tbl_EmailTemplates (TemplateName, TemplateType, Subject, BodyHtml, IsActive, IsDefault)
    VALUES (
        'Default Anniversary Template',
        'Anniversary',
        'Happy Anniversary {GuestName}! Celebrate with Us',
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
            <h1 style="margin: 0; font-size: 32px;">Happy Anniversary!</h1>
            <p style="margin: 10px 0 0 0; font-size: 18px;">Dear {GuestName}</p>
        </div>
        <div class="content">
            <p>Wishing you a wonderful anniversary filled with love and joy!</p>
            
            <div class="offer-box">
                <h2 style="color: #f5576c; margin-top: 0;">Anniversary Special</h2>
                <p style="font-size: 18px; font-weight: bold; color: #f093fb;">Complimentary Dessert for Two!</p>
                <p style="font-size: 14px; color: #666;">When you dine with us this week</p>
                <p style="font-size: 12px; margin-top: 15px; color: #999;">Mention this offer when making your reservation</p>
            </div>
            
            <p>Celebrate your special day at {RestaurantName}. We would be honored to be part of your celebration!</p>
            
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
    PRINT 'Default Anniversary template created successfully';
END
ELSE
BEGIN
    PRINT 'Anniversary template already exists';
END
GO

-- Verify both templates exist
SELECT TemplateName, TemplateType, IsDefault, IsActive 
FROM tbl_EmailTemplates 
ORDER BY TemplateType;
