# Testing the Conversational Profile Feature

This guide helps you test the AI-powered conversational profile building feature.

## Prerequisites

Before testing, ensure:
1. Azure Functions project is running (`func start`)
2. Blazor client is running (`dotnet run`)
3. OpenAI API key is configured in `local.settings.json`
4. Tailwind CSS has been built (`npm run css:build`)

## Quick Start

1. **Navigate to Profile Wizard**
   - Open browser to `http://localhost:5000`
   - Click "Get Started" or "Create Profile"
   - Complete Steps 1-3 (Basic Info, Academic, Demographics)

2. **Access Conversational Mode**
   - On Step 4 (Activities & Interests), click the **"Chat with AI ✨"** tab
   - You should see a welcome message from the AI

3. **Have a Conversation**
   - Try the example conversation below
   - Type naturally as if talking to a friend
   - Watch extracted data appear in the preview box

## Example Test Conversation

### Test Case 1: Basic Profile Building

**User Messages:**
```
1. "I'm on the debate team and I'm the president of the environmental club"
2. "I'm really interested in climate change and environmental policy"
3. "I want to become an environmental lawyer and work on climate legislation"
```

**Expected Results:**
- AI should ask follow-up questions
- Extracted data should show:
  - Activities: "Debate Team", "Environmental Club (President)"
  - Interests: "Climate Change", "Environmental Policy"
  - Career Goals: "Environmental Lawyer", "Climate Legislation"
- Data should appear in the green preview box

### Test Case 2: All-in-One Message

**User Message:**
```
"I'm involved in Nonprofit government, play varsity basketball, and volunteer at the local animal shelter. I'm passionate about social justice and animal rights. After college, I want to become a civil rights attorney."
```

**Expected Results:**
- AI should acknowledge and extract all information
- Activities: "Nonprofit Government", "Varsity Basketball", "Animal Shelter Volunteer"
- Interests: "Social Justice", "Animal Rights"
- Career Goals: "Civil Rights Attorney"

### Test Case 3: Gradual Information Gathering

**Conversation Flow:**
```
AI: "What activities or clubs are you involved in?"
User: "I'm in the chess club"

AI: "Great! Any other activities?"
User: "I also tutor elementary school kids in math"

AI: "What are you passionate about?"
User: "Mathematics and education"

AI: "What are your career goals?"
User: "I want to be a math teacher"
```

**Expected Results:**
- Each response should build on the profile
- AI should ask logical follow-up questions
- Final extracted data should include all mentioned items

## Testing UI Features

### 1. Mode Toggle
- [ ] Can switch between "Manual Input" and "Chat with AI"
- [ ] Manual mode shows form inputs
- [ ] Chat mode shows chat interface
- [ ] Data persists when switching modes

### 2. Chat Interface
- [ ] Welcome message displays on first load
- [ ] User messages appear on the right (blue background)
- [ ] AI messages appear on the left (white background)
- [ ] Typing indicator shows while processing
- [ ] Timestamps display correctly
- [ ] Chat auto-scrolls to bottom

### 3. Data Extraction
- [ ] Preview box appears when data is extracted
- [ ] Shows activities, interests, and career goals
- [ ] "Apply to Profile" button is clickable
- [ ] Data merges correctly when applied
- [ ] No duplicates when applying multiple times

### 4. Profile Display
- [ ] Added items show in colored badges below chat
- [ ] Activities in blue badges
- [ ] Interests in purple badges
- [ ] Career goals in green badges
- [ ] Can see all applied data

### 5. Error Handling
- [ ] Shows error message if API fails
- [ ] Can retry after error
- [ ] Doesn't break UI on error
- [ ] Input disabled during processing

## API Testing

### Test Direct API Call

Use this curl command to test the conversation endpoint directly:

```bash
curl -X POST http://localhost:7071/api/conversation \
  -H "Content-Type: application/json" \
  -d '{
    "NonprofitId": "00000000-0000-0000-0000-000000000000",
    "message": "I am on the debate team and play soccer",
    "history": []
  }'
```

**Expected Response:**
```json
{
  "reply": "Great! Debate and soccer are excellent activities...",
  "updatedHistory": [...],
  "extractedData": {
    "extracurricularActivities": ["Debate Team", "Soccer"],
    "interests": [],
    "careerGoals": []
  },
  "profileComplete": false
}
```

## Performance Testing

### Response Time
- [ ] AI responses arrive within 3-5 seconds
- [ ] No timeout errors
- [ ] UI remains responsive during processing

### Token Usage
Check Azure Functions logs for token usage:
```
Processing conversation
Tokens used: ~150-300 per exchange
```

### Long Conversations
- [ ] Can handle 10+ message exchanges
- [ ] History doesn't grow too large
- [ ] Performance stays consistent

## Edge Cases to Test

### 1. Empty Messages
- Type empty message and press Send
- **Expected**: Button should be disabled, nothing happens

### 2. Very Long Message
```
"I'm involved in debate team, Nonprofit government, chess club, basketball, volunteering at three different places including the food bank, animal shelter, and tutoring center, I play piano, I'm learning Spanish and French, I like science especially biology and chemistry but also physics, I want to be a doctor or maybe a scientist or perhaps an engineer, I'm interested in climate change, social justice, animal rights, education reform, healthcare, technology, artificial intelligence, space exploration, renewable energy, and sustainable development."
```
- **Expected**: AI should parse and extract key items

### 3. Off-Topic Messages
```
User: "What's the weather like today?"
User: "Tell me a joke"
```
- **Expected**: AI should redirect to profile-related questions

### 4. Incomplete Information
```
User: "I like stuff"
```
- **Expected**: AI should ask clarifying questions

### 5. Multiple Sessions
- Complete a conversation
- Refresh the page
- Start new conversation
- **Expected**: History resets, new session starts fresh

## Debugging Tips

### Check Browser Console
```javascript
// Should see no errors
// Look for API calls to /api/conversation
```

### Check Azure Functions Logs
```
[2025-01-15 10:30:00] Processing conversation
[2025-01-15 10:30:02] OpenAI response received
[2025-01-15 10:30:02] Extracted data: {...}
```

### Common Issues

**Issue**: AI doesn't respond
- Check OpenAI API key in `local.settings.json`
- Verify Functions app is running
- Check browser network tab for errors

**Issue**: Data not extracting
- Check OpenAI response format in logs
- Verify JSON parsing is working
- Ensure system prompt is configured

**Issue**: UI not updating
- Check `StateHasChanged()` is called
- Verify event handlers are bound correctly
- Check for JavaScript console errors

## Success Criteria

A successful test should demonstrate:
- ✅ Natural conversation flow with AI
- ✅ Accurate data extraction from messages
- ✅ Real-time preview of extracted information
- ✅ Seamless data application to profile
- ✅ Smooth UI transitions and updates
- ✅ Error handling and recovery
- ✅ Mobile-responsive design
- ✅ Professional, friendly AI tone

## Next Steps After Testing

If all tests pass:
1. Document any bugs or issues found
2. Test on different browsers (Chrome, Firefox, Edge, Safari)
3. Test on mobile devices
4. Consider adding more sophisticated AI prompts
5. Optimize for token usage and cost
6. Add analytics to track conversation patterns

## Feedback Collection

When testing with real users:
- Ask: "Was the AI helpful?"
- Ask: "Did it extract your information correctly?"
- Ask: "Would you prefer this over manual forms?"
- Track: Completion rates (chat vs manual)
- Track: Number of messages per profile
- Track: Most common conversation patterns

## Cost Monitoring

Monitor OpenAI API costs:
- Each conversation turn: ~$0.001-0.003
- 100 profiles: ~$0.50-1.50
- 1,000 profiles: ~$5.00-15.00

Consider implementing:
- Rate limiting per user
- Caching common responses
- Token usage alerts
