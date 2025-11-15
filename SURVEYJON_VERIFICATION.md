# ✅ SurveyJson Database Save - Verification Guide

## 🎯 Quick Test Steps

### 1. Open the Feedback Form
Navigate to: `https://localhost:7290/Feedback/Form`

### 2. Fill the Form (Minimum Requirements)
- **Required**: Click "Overall Rating" stars (at least 1 star)
- **Likert Survey**: Answer at least 2-3 questions by clicking radio buttons
- Other fields are optional

### 3. Check Browser Console (F12)
**BEFORE submitting**, open Developer Tools (F12) → Console tab

### 4. Submit the Form
Click "Submit Feedback" button

### 5. Verify Console Output
You should see in the browser console:
```
Feedback form scripts loaded
=== FEEDBACK FORM SUBMISSION ===
Likert Survey: 3 questions answered {q1: 5, q5: 4, q8: 5}
Survey JSON: {"q1":5,"q5":4,"q8":5}
Hidden input value set to: {"q1":5,"q5":4,"q8":5}
Hidden input name: SurveyJson
Form will submit with SurveyJson
```

### 6. Check Server Terminal Logs
In the terminal where you ran `dotnet run`, you should see:
```
=== Feedback Submission Debug ===
VisitDate: 15/11/2025
OverallRating: 5
Location: ...
FirstVisit: ...
SurveyJson length: 32
SurveyJson: {"q1":5,"q5":4,"q8":5}
Tags: ...
Comments length: ...
SP has 17 parameters: VisitDate, OverallRating, ..., SurveyJson, ...
✓ SurveyJson parameter ADDED to SP call
Executing SP with 17 parameters
✓ Feedback saved successfully with ID: X
```

**Key checks:**
- ✅ `SurveyJson length:` should be > 0 (not 0!)
- ✅ `SurveyJson:` should show the JSON string
- ✅ `✓ SurveyJson parameter ADDED to SP call`
- ✅ `✓ Feedback saved successfully with ID: X`

### 7. Verify Database
Run this query in SQL Server:
```sql
SELECT TOP 5 
    Id,
    VisitDate,
    OverallRating,
    LEN(SurveyJson) as SurveyJsonLength,
    SurveyJson,
    CreatedAt
FROM GuestFeedback
ORDER BY CreatedAt DESC
```

**Expected result:**
```
Id  | VisitDate  | OverallRating | SurveyJsonLength | SurveyJson            | CreatedAt
----|------------|---------------|------------------|-----------------------|-------------------
123 | 2025-11-15 | 5             | 32               | {"q1":5,"q5":4,"q8":5}| 2025-11-15 10:30:45
```

- ✅ `SurveyJsonLength` should be > 0
- ✅ `SurveyJson` should contain JSON data

### 8. View in Summary Page
Navigate to: `https://localhost:7290/Feedback/Summary`

- ✅ Your feedback should appear in "Recent Feedback" table
- ✅ If you answered survey questions, you'll see a **Survey** button
- ✅ Click the Survey button → Modal shows your answers in readable format

---

## 🐛 Troubleshooting

### Issue: "SurveyJson length: 0" in server logs

**Possible causes:**
1. **JavaScript not running**
   - Check browser console for errors
   - Verify you see "Feedback form scripts loaded"
   
2. **No survey questions answered**
   - Make sure you click radio buttons in the Likert table
   - Check the badge shows "X/10 survey questions answered"

3. **Form submitting before JS executes**
   - The submit event listener should fire before form posts
   - Check console logs appear before page redirect

### Issue: "SurveyJson parameter NOT supported by SP"

**Solution:** Run the SQL setup script
```bash
RestaurantManagementSystem/RestaurantManagementSystem/SQL/GuestFeedback_Setup.sql
```

The stored procedure needs the `@SurveyJson` parameter.

### Issue: Console shows JSON but server receives empty

**Check:**
1. Hidden input exists: `<input name="SurveyJson" type="hidden" value="" />`
2. Input name matches model property exactly (case-sensitive)
3. Form method is POST
4. No JavaScript errors preventing form submission

### Issue: Database column doesn't exist

**Solution:** Run this in SQL Server:
```sql
-- Check if column exists
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'GuestFeedback' AND COLUMN_NAME = 'SurveyJson'

-- If not exists, add it:
ALTER TABLE GuestFeedback ADD [SurveyJson] NVARCHAR(MAX) NULL;
```

---

## 📊 Visual Indicators

### In Form:
- Badge shows: "3/10 survey questions answered" (turns green as you answer)
- Hidden input populated: Look in browser DevTools → Elements → find `input[name="SurveyJson"]` → value should contain JSON

### In Console:
- ✅ Green checkmarks and detailed JSON output
- ❌ Red error messages if something is wrong

### In Server Logs:
- ✅ `✓ SurveyJson parameter ADDED to SP call`
- ✅ `✓ Feedback saved successfully with ID: X`
- ❌ `✗ SurveyJson parameter NOT supported by SP` → Run SQL script

---

## 🎯 Success Criteria

**All of these must be TRUE:**

1. ✅ Browser console shows: "Survey JSON: {..."
2. ✅ Server logs show: "SurveyJson: {..." (length > 0)
3. ✅ Server logs show: "✓ SurveyJson parameter ADDED"
4. ✅ Database query returns SurveyJson with JSON data
5. ✅ Summary page shows Survey button for the record
6. ✅ Clicking Survey button displays answers in modal

**If ANY of these fail, use the troubleshooting steps above.**

---

## 🔧 Manual Verification Commands

### Check if SP has the parameter:
```sql
SELECT name, system_type_name
FROM sys.dm_exec_describe_first_result_set (N'EXEC usp_SubmitGuestFeedback', NULL, 0)
WHERE name = '@SurveyJson'
```

### Check if table has the column:
```sql
SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'GuestFeedback' 
AND COLUMN_NAME = 'SurveyJson'
```

### Test inserting manually:
```sql
EXEC usp_SubmitGuestFeedback
    @VisitDate = '2025-11-15',
    @OverallRating = 5,
    @SurveyJson = '{"q1":5,"q2":4,"q3":5}'
```

Then verify:
```sql
SELECT TOP 1 Id, SurveyJson FROM GuestFeedback ORDER BY Id DESC
```

---

## 📝 Current Status

**✅ Code Implementation: COMPLETE**
- Form has Likert table with 10 questions
- JavaScript serializes to JSON on submit
- Controller receives SurveyJson parameter
- Database has SurveyJson column
- Summary displays Survey button/modal

**🎯 Next Step: TEST THE FLOW**

Follow steps 1-8 above to verify end-to-end functionality.

---

**Last Updated:** 2025-11-15
**App URL:** https://localhost:7290/Feedback/Form
**Summary URL:** https://localhost:7290/Feedback/Summary
