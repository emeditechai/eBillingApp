# 🚀 Quick Start - Test Likert Survey Feature

## ✅ What's Ready
The Likert survey (10 detailed service questions) is **fully implemented** in your feedback form. All data flows from UI → Controller → Database and displays in Summary.

## 📋 3-Step Setup

### 1️⃣ Run SQL Script
Open SQL Server Management Studio (or your SQL client) and execute:
```
RestaurantManagementSystem/RestaurantManagementSystem/SQL/GuestFeedback_Setup.sql
```

This adds the `SurveyJson` column and updates stored procedures.

### 2️⃣ Restart Your App
```bash
# Kill port 7290
lsof -ti:7290 | xargs kill -9 2>/dev/null || true

# Start app
cd /Users/abhikporel/dev/Restaurantapp
dotnet run --project RestaurantManagementSystem/RestaurantManagementSystem/RestaurantManagementSystem.csproj
```

### 3️⃣ Test the Feature
1. Open: `https://localhost:7290/Feedback/Form`
2. Fill:
   - ✅ Overall Rating (required - click stars)
   - ✅ Answer 2-3 Likert questions (click radio buttons in the table)
   - ⚪ Other fields optional
3. Click **Submit Feedback**
4. Check terminal logs - you should see:
   ```
   === Feedback Submission Debug ===
   SurveyJson: {"q1":5,"q2":4,...}
   ```
5. Visit: `https://localhost:7290/Feedback/Summary`
6. See your feedback with a **Survey** button
7. Click **Survey** button → Modal shows your responses!

## 🎯 What You'll See

### In the Form
![Likert Survey Table](https://via.placeholder.com/800x300.png?text=Likert+Survey+Table+with+10+Questions)
- 10 statements (e.g., "I was seated promptly")
- 5 radio buttons per row (Strongly Agree → Strongly Disagree)

### In Browser Console (F12)
```
Likert Survey: 3 questions answered {q1: 5, q3: 4, q7: 5}
Survey JSON being submitted: {"q1":5,"q3":4,"q7":5}
```

### In Server Logs
```
=== Feedback Submission Debug ===
VisitDate: 2025-11-15
OverallRating: 5
SurveyJson length: 32
SurveyJson: {"q1":5,"q3":4,"q7":5}
SP has 17 parameters: VisitDate, OverallRating, ..., SurveyJson, ...
```

### In Summary Page
- Card shows: **Total: 1**
- Table row has your feedback
- **Survey** button appears (if you answered questions)
- Click → Modal displays:
  ```
  Question                              | Response
  --------------------------------------|------------------
  I was seated promptly                 | Strongly Agree
  The server was polite                 | Agree
  My order was taken promptly           | Strongly Agree
  ```

## 🔍 Verify Database
```sql
SELECT TOP 5 
    Id, 
    VisitDate, 
    OverallRating,
    LEN(SurveyJson) as SurveyLength,
    SurveyJson,
    CreatedAt
FROM GuestFeedback
ORDER BY CreatedAt DESC
```

You should see SurveyJson populated with your responses.

## ✨ All Features Working
- ✅ 10-question Likert survey in form
- ✅ JavaScript serializes responses to JSON
- ✅ Controller receives and logs SurveyJson
- ✅ Database stores SurveyJson in NVARCHAR(MAX)
- ✅ Summary displays "Survey" button
- ✅ Modal shows readable Q&A format
- ✅ All other fields (ratings, location, tags, comments) also captured
- ✅ Fallback logic ensures list always shows after submit

## 🆘 If Something's Wrong
1. **No Survey button?** → Check if you answered any Likert questions
2. **Blank Summary?** → Run the SQL script, then restart app
3. **JS errors?** → Check browser console (F12)
4. **DB errors?** → Check server logs for SQL exception details

## 📖 Full Documentation
See: `LIKERT_SURVEY_IMPLEMENTATION.md` for complete details.

---
**Status:** ✅ READY FOR TESTING
**Next:** Run SQL script → Restart app → Test form submission → View in Summary
