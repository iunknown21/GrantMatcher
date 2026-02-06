# Conversational Profile Building

## Overview

The conversational profile building feature allows Nonprofits to create their Grant profiles through a natural chat conversation instead of filling out traditional forms. This AI-powered feature makes profile creation more engaging and accessible.

## Features

### 1. Chat Interface
- Modern, user-friendly chat UI with message bubbles
- Real-time typing indicators
- Timestamp display for messages
- Smooth scrolling and animations

### 2. AI-Powered Extraction
- Uses OpenAI's GPT-4o-mini to understand natural language
- Extracts structured data from conversational text
- Identifies:
  - Extracurricular activities
  - Personal interests
  - Career goals
  - Academic information (GPA, major)

### 3. Dual Mode Input
Nonprofits can switch between two modes in Step 4 of the profile wizard:

**Manual Input Mode:**
- Traditional form-based input
- Add/remove items with buttons
- Quick tips and suggestions

**Conversational Mode:**
- Chat with AI assistant
- Natural language conversation
- Real-time data extraction preview
- Profile information display

## How It Works

### User Flow

1. **Start Conversation**
   - Nonprofit navigates to Step 4 (Activities & Interests) in the profile wizard
   - Clicks "Chat with AI ✨" tab to enter conversational mode
   - AI greets them with a welcome message and initial question

2. **Chat Exchange**
   - Nonprofit types messages naturally (e.g., "I'm on the debate team and volunteer at the local food bank")
   - AI asks follow-up questions to gather more information
   - Extracted information appears in a preview box

3. **Data Application**
   - Nonprofit can click "Apply to Profile" to add extracted data
   - Or data auto-applies when conversation is complete
   - Nonprofit can switch to manual mode to review/edit

4. **Completion**
   - Once enough information is gathered, AI indicates completion
   - Nonprofit proceeds to next wizard step

### Technical Architecture

```
┌─────────────────┐
│ StepActivities  │
│   (Razor)       │
└────────┬────────┘
         │
         ├─── Manual Mode
         │    └─── Add/Remove Lists
         │
         └─── Conversational Mode
              └─── ConversationalInput.razor
                   │
                   └─── ApiClient.ProcessConversationAsync()
                        │
                        └─── Azure Function: ProcessConversation
                             │
                             └─── OpenAIService.ProcessConversationAsync()
                                  │
                                  └─── OpenAI API (GPT-4o-mini)
```

## API Integration

### ConversationRequest
```json
{
  "NonprofitId": "uuid",
  "message": "I'm on the debate team and play soccer",
  "history": [
    {
      "role": "assistant",
      "content": "What activities are you involved in?",
      "timestamp": "2025-01-15T10:30:00Z"
    }
  ]
}
```

### ConversationResponse
```json
{
  "reply": "Great! Debate and soccer are excellent activities. What are you passionate about learning?",
  "updatedHistory": [...],
  "extractedData": {
    "extracurricularActivities": ["Debate Team", "Soccer"],
    "interests": [],
    "careerGoals": []
  },
  "profileComplete": false
}
```

## AI Prompt Design

The system uses a carefully crafted prompt to guide the AI:

**Key Instructions:**
- Ask friendly, conversational questions
- Extract specific fields: GPA, major, activities, interests, career goals
- Return valid JSON with structured data
- Determine when profile is complete
- Only include fields that have been discussed

**Response Format:**
The AI always responds with JSON containing:
- `reply`: The conversational message to display
- `extractedData`: Structured profile information
- `profileComplete`: Boolean indicating if enough data is collected

## Component Details

### ConversationalInput.razor

**Props:**
- `Profile`: NonprofitProfile - The profile being built
- `OnProfileUpdated`: EventCallback - Triggered when data is applied

**Features:**
- Chat message display with role-based styling
- Message history tracking
- Typing indicators during AI processing
- Extracted data preview box
- Apply button to merge data into profile
- Error handling and display

**State Management:**
- `messages`: List of conversation messages
- `currentMessage`: User's input text
- `isProcessing`: Loading state during API calls
- `extractedData`: Latest extracted profile information
- `errorMessage`: Error display

### StepActivities.razor (Updated)

**New Features:**
- Mode toggle buttons (Manual/Conversational)
- Conditional rendering based on selected mode
- Profile information display with colored badges
- Seamless switching between modes

## Usage Examples

### Example Conversation Flow

```
AI: Hi! What activities or clubs are you involved in at school?

Nonprofit: I'm the president of the debate club and I volunteer at the food bank every weekend.

AI: That's wonderful! Leadership and community service are great. What subjects or topics are you most interested in?

Nonprofit: I'm really into environmental science and climate change issues.

AI: Excellent! What do you hope to do after college?

Nonprofit: I want to become an environmental lawyer to help fight climate change.

AI: Perfect! I've captured your information. You can review and continue to the next step.

[Extracted Data]
- Activities: Debate Club (President), Food Bank Volunteer
- Interests: Environmental Science, Climate Change
- Career Goals: Environmental Lawyer
```

## Configuration

### Required Settings

In `local.settings.json` (Functions project):
```json
{
  "Values": {
    "OpenAI:ApiKey": "sk-your-api-key",
    "OpenAI:ChatModel": "gpt-4o-mini",
    "OpenAI:EmbeddingModel": "text-embedding-3-small"
  }
}
```

### API Endpoint

The conversation endpoint is:
```
POST /api/conversation
```

Authorization Level: Function (requires function key in production)

## Benefits

### For Nonprofits
1. **More Natural**: Talk naturally instead of filling forms
2. **Less Intimidating**: Conversational UI feels friendly
3. **Guided**: AI asks questions to help remember activities
4. **Flexible**: Can switch between chat and manual mode

### For Matching
1. **Rich Context**: Conversational data provides deeper insights
2. **Natural Language**: AI can understand nuanced descriptions
3. **Better Summaries**: Can generate compelling profile summaries
4. **Improved Embeddings**: Natural language creates better semantic vectors

## Best Practices

### For Development
1. **Test Different Conversation Flows**: Try various ways Nonprofits might describe themselves
2. **Handle Edge Cases**: Empty responses, very long messages, off-topic content
3. **Monitor API Costs**: GPT-4o-mini calls can add up with many users
4. **Cache Responses**: Consider caching common patterns
5. **Rate Limiting**: Implement to prevent abuse

### For Users
1. **Be Natural**: Type as if talking to a friend
2. **Be Specific**: "President of Debate Club" vs "debate"
3. **Multiple Items**: Can mention several things in one message
4. **Review Data**: Always check extracted information before proceeding
5. **Switch Modes**: Use manual mode to add/edit as needed

## Error Handling

The system handles:
- Network errors during API calls
- Invalid API responses
- Empty or malformed messages
- OpenAI API errors
- Rate limiting

All errors display friendly messages to the user without breaking the UI.

## Future Enhancements

Potential improvements:
1. **Voice Input**: Allow Nonprofits to speak instead of type
2. **Multi-Language**: Support conversations in multiple languages
3. **Suggested Responses**: Quick reply buttons for common questions
4. **Photo Upload**: Extract activities from photos/certificates
5. **Resume Parsing**: Upload resume and extract information
6. **Conversation Restart**: Clear history and start over
7. **Save Draft**: Resume conversation later
8. **Personality Options**: Choose AI personality (formal, casual, encouraging)

## Testing Checklist

- [ ] Welcome message displays correctly
- [ ] User can send messages
- [ ] AI responds appropriately
- [ ] Extracted data appears in preview
- [ ] Apply button merges data into profile
- [ ] Mode toggle switches correctly
- [ ] Profile badges display added items
- [ ] Error messages show for failures
- [ ] Typing indicator appears during processing
- [ ] Messages scroll smoothly
- [ ] Works on mobile devices
- [ ] API calls include auth headers
- [ ] Long conversations don't break UI
- [ ] Can complete profile through conversation
- [ ] Manual mode shows conversational data

## Troubleshooting

### AI Not Responding
- Check OpenAI API key in settings
- Verify API endpoint is correct
- Check function app is running
- Review logs for errors

### Data Not Extracting
- Ensure system prompt is properly configured
- Verify JSON parsing in response
- Check OpenAI model has JSON mode enabled
- Review conversation history for context

### UI Issues
- Clear browser cache
- Check console for JavaScript errors
- Verify Tailwind classes are compiling
- Test in different browsers

## Performance Considerations

- **API Latency**: OpenAI calls take 1-3 seconds
- **Token Usage**: Long conversations use more tokens
- **Message History**: Limit history to last 10-15 messages to reduce token cost
- **Caching**: Consider caching embeddings for extracted data
- **Async Operations**: All API calls are async to prevent UI blocking

## Security

- Function authorization required for API access
- No PII stored in conversation logs
- API keys stored securely in Azure configuration
- CORS configured for Blazor client domain only
- Rate limiting prevents abuse
- Input sanitization prevents injection attacks
