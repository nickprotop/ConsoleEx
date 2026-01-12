# AI Agent Chat Example

A modern console-based chat interface demonstrating SharpConsoleUI's fluent builder pattern with a sophisticated dark grayscale theme.

## Features

- **Dark Grayscale Theme**: Professional color scheme using various shades of gray (Grey11-Grey93)
- **Fluent Builder Pattern**: Modern window creation with method chaining
- **Async Message Handling**: Non-blocking AI responses with simulated thinking delays
- **Scrollable Message History**: Smooth scrolling with mouse wheel support
- **Mock AI Agent**: Keyword-based contextual responses
- **Modern C# Features**: Records, async/await, nullable reference types

## Dark Theme Color Palette

| Element | Color | Purpose |
|---------|-------|---------|
| Window Background | Grey11 | Very dark, almost black background |
| Window Text | Grey93 | Light gray for readability |
| **User Message Background** | **Grey19** | **Slightly lighter bubble for user messages** |
| User Label | Silver | Bright indicator for user messages |
| User Text | Grey93 | Standard text color |
| **AI Message Background** | **Grey15** | **Slightly darker bubble for AI messages** |
| AI Label | Grey78 | Medium-light for AI identifier |
| AI Text | Grey89 | Slightly dimmer than user text |
| System Messages | Grey50 | Mid-tone for system notifications |
| Timestamps | Grey50 | Subtle time indicators |
| Input (Focused) | Grey27 bg / White text | Highlighted input area |
| Input (Unfocused) | Grey19 bg / Grey70 text | Dimmed when not active |

## Running the Example

### Build and Run
```bash
# From the repository root
dotnet run --project Examples/AiAgentChatExample

# Or navigate to the example directory
cd Examples/AiAgentChatExample
dotnet run
```

### Build Only
```bash
dotnet build Examples/AiAgentChatExample/AiAgentChatExample.csproj
```

## Usage

### Keyboard Shortcuts
- **Ctrl+Enter**: Send the message
- **Ctrl+L**: Clear the input field
- **ESC**: Close the chat window and exit

### Mouse Support
- **Mouse Wheel**: Scroll through message history
- **Click**: Click buttons (Send/Clear)

### Chat Features

1. **Send Messages**: Type your message and press Ctrl+Enter or click the Send button
2. **Keyword Detection**: The AI recognizes keywords and responds contextually:
   - **Greetings**: "hello", "hi", "hey" → Friendly welcome
   - **Help**: "help", "?" → Usage instructions
   - **Technical**: "code", "programming", "debug" → Tech responses
   - **Questions**: "what", "how", "why" → Thoughtful answers
   - **Thanks**: "thank", "thanks" → Appreciation responses
   - **Farewell**: "bye", "goodbye" → Polite closing
3. **AI Thinking**: Shows "AI is thinking..." while processing (500-1500ms delay)
4. **Message History**: All messages are displayed with timestamps

## Project Structure

```
AiAgentChatExample/
├── AiAgentChatExample.csproj  # .NET 9.0 project file
├── Program.cs                  # Main application with UI
├── ChatMessage.cs             # Message data model (record)
├── MockAiAgent.cs             # AI response generator
└── README.md                  # This file
```

## Architecture

### Controls Used
- **ScrollablePanelControl**: Scrollable message history container
- **MarkupControl**: Rich text formatting with Spectre.Console markup
- **MultilineEditControl**: User input (3-line viewport)
- **ButtonControl**: Send and Clear buttons
- **HorizontalGridControl**: Button layout
- **RuleControl**: Visual separator

### Design Patterns
- **Fluent Builder**: `WindowBuilder` for clean window configuration
- **Async/Await**: Non-blocking AI response handling
- **Record Types**: Immutable `ChatMessage` data structure
- **Event-Driven**: Keyboard and button event handlers

## Message Format

### User Messages
```
┌─ User Message (Grey19 background) ─┐
│ User 14:23:15                      │
│ Hello, how are you?                │
└────────────────────────────────────┘
```

### AI Messages
```
┌─ AI Message (Grey15 background) ───┐
│ AI 14:23:16                        │
│ Hello! How can I assist you today? │
└────────────────────────────────────┘
```

### System Messages
```
System: Welcome to AI Agent Chat! Type your message and press Ctrl+Enter to send.
```

**Note**: User and AI messages have subtly different background colors (Grey19 vs Grey15) to make them easily distinguishable while maintaining the dark theme aesthetic.

## Example Interactions

### Greeting
```
User: Hello
AI: Hi there! What can I help you with?
```

### Help Request
```
User: help
AI: I can chat about various topics. Try asking me about programming, general questions, or just have a conversation!
```

### Technical Question
```
User: How do I debug my code?
AI: Debugging can be challenging. Have you tried adding logging statements?
```

### General Conversation
```
User: What do you think about that?
AI: That's a great question! Let me think about that...
```

## Customization

### Changing Colors
Edit `Program.cs` and modify the color values in `CreateChatWindow()`:
```csharp
.WithColors(Color.Grey11, Color.Grey93)  // Window background/foreground
```

Or in `FormatMessage()` for message colors:
```csharp
$"[silver]User[/] [grey50]{timestamp}[/]"  // User label and timestamp
```

### Adding Response Keywords
Edit `MockAiAgent.cs` and add entries to `_keywordResponses`:
```csharp
[new[] { "your", "keywords" }] = new[]
{
    "Response 1",
    "Response 2"
}
```

### Adjusting Window Size
Modify in `CreateChatWindow()`:
```csharp
.WithSize(90, 32)  // Width, Height in characters
```

## Requirements

- **.NET 9.0** or later
- **SharpConsoleUI** library (included via project reference)
- Terminal with **Unicode support** (for proper rendering)

## Notes

- The AI responses are **mock/simulated** - no actual AI model is used
- Responses are selected randomly from predefined pools based on keyword matching
- The thinking delay (500-1500ms) simulates realistic AI processing time
- No data is persisted - messages are lost when the application closes
