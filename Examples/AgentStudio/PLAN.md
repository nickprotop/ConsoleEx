# AgentStudio - OpenCode-Inspired TUI Showcase for SharpConsoleUI

## Goal

Create a visually impressive terminal UI showcase application inspired by OpenCode's beautiful interface to demonstrate SharpConsoleUI's advanced capabilities. **This is a DEMO/SHOWCASE, not a real AI coding agent.**

---

## Inspiration: OpenCode UI/UX

Based on research, OpenCode features:
- Clean dark theme with excellent visual hierarchy
- Multi-panel layouts with resizable splitters
- Beautiful tool call visualization with syntax highlighting
- Modal dialogs and command palette
- Side-by-side diff rendering
- Rich status bars with live information

**Sources:**
- [OpenCode GitHub](https://github.com/opencode-ai/opencode)
- [OpenCode TUI Docs](https://opencode.ai/docs/tui/)

---

## Application Overview: AgentStudio

A mock AI coding assistant interface showcasing SharpConsoleUI controls.

### Main Layout

```
┌──────────────────────────────────────────────────────────────────┐
│ AgentStudio                                      [ESC: Quit]     │ <- Title
├──────────────────────────────────────────────────────────────────┤
│ Mode: Build        Session: demo-1                   12:34:56   │ <- Status
├────────────┬─────────────────────────────────────────────────────┤
│ Project    │ Conversation & Tool Outputs (Scrollable)           │
│ Explorer   │                                                     │
│ (Tree)     │ [User] Analyze auth system                         │
│            │                                                     │
│ src/       │ [AI] Reading files...                              │
│ ├─ auth/   │                                                     │
│ │  ├─login │ ┌─ Tool Call: Read File ───────────────────────┐  │
│ │  └─jwt   │ │ File: src/auth/login.cs                      │  │
│ ├─ api/    │ │ ┌──────────────────────────────────────────┐ │  │
│ └─ models/ │ │ │ using System.Security;                   │ │  │
│            │ │ │ public class LoginService { ... }        │ │  │
│ tests/     │ │ └──────────────────────────────────────────┘ │  │
│ └─ auth/   │ └──────────────────────────────────────────────┘  │
│            │                                                     │
│ docs/      │ [AI] Found 3 security issues:                      │
│            │ ┌─ Analysis Results ──────────────────────────┐   │
│   [↑↓]     │ │ ⚠ High: Plain text passwords (line 23)      │   │
│            │ │ ⚠ Med: No rate limiting (line 45)          │   │
├────────────┴─────────────────────────────────────────────────────┤
│ > @auth/login.cs Fix the issues              [Type @ or /]     │ <- Input
├──────────────────────────────────────────────────────────────────┤
│ Tokens: 1.2k  |  Ctrl+N:New  Ctrl+S:Session  Tab:Mode          │ <- Bottom
└──────────────────────────────────────────────────────────────────┘
```

---

## SharpConsoleUI Features Demonstrated

| Feature | Usage in AgentStudio |
|---------|---------------------|
| **HorizontalGridControl** | Main 2-column layout (explorer + conversation) |
| **SplitterControl** | Resizable divider between panels |
| **TreeControl** | Project file explorer with expand/collapse |
| **ScrollablePanelControl** | Scrollable conversation history |
| **MarkupControl** | Rich text for messages, tool outputs, code |
| **MultilineEditControl** | User input area with command hints |
| **SpectreRenderableControl** | Tables, panels for tool results |
| **ListControl** | File picker, session manager (in modals) |
| **RuleControl** | Section separators |
| **WindowBuilder** | Fluent window creation with modal support |
| **Modal Windows** | File picker, session manager, command palette |
| **StickyPosition** | Status bars (top/bottom), input area |
| **Async Patterns** | Thinking animation, typing simulation, live clock |
| **Theme System** | Consistent dark grayscale palette |
| **Event Handlers** | Keyboard shortcuts, mouse interactions |

---

## UI Components Design

### 1. Project Explorer (Left Panel - 30 chars)

**Control:** `TreeControl`

**Features:**
- Mock project structure with folders and files
- Color-coded by file type:
  - Folders: `Cyan1`
  - C# files: `Green`
  - Test files: `Magenta`
  - Docs: `Grey70`
- Expand/collapse with arrows
- Selection highlighting

**Mock Structure:**
```
MyProject/
├── src/
│   ├── auth/
│   │   ├── login.cs
│   │   ├── jwt.cs
│   │   └── password.cs
│   ├── api/
│   │   ├── users.cs
│   │   └── products.cs
│   └── models/
│       ├── User.cs
│       └── Product.cs
├── tests/
│   ├── auth/LoginTests.cs
│   └── api/UserApiTests.cs
└── docs/
    └── README.md
```

### 2. Conversation Panel (Right Panel - Remainder)

**Control:** `ScrollablePanelControl` with nested `MarkupControl`s

**Message Types:**

**User Messages:**
- Background: `Grey19`
- Format: `[silver]User[/] [grey50]HH:mm:ss[/]`
- Margin: `(1, 0, 1, 1)`

**AI Messages:**
- Background: `Grey15`
- Format: `[grey78]AI[/] [grey50]HH:mm:ss • X.Xs[/]`
- Margin: `(1, 0, 1, 1)`

**Tool Call Panels:**
```
┌─ Tool Call: Read File ────────────────────────────┐
│ File: src/auth/login.cs | Status: ✓ Complete     │
│ ┌──────────────────────────────────────────────┐ │
│ │ [magenta1]using[/] System.Security;          │ │
│ │ [magenta1]public[/] [magenta1]class[/] ...   │ │
│ └──────────────────────────────────────────────┘ │
│ [grey50]Execution time: 0.5s[/]                  │
└────────────────────────────────────────────────────┘
```

**Analysis Panels:**
```
┌─ Analysis Results ─────────────────────────────────┐
│ [red]⚠ High[/]: Plain text passwords (line 23)    │
│ [yellow]⚠ Med[/]: No rate limiting (line 45)      │
│ [yellow]⚠ Low[/]: Weak JWT secret (line 12)       │
└────────────────────────────────────────────────────┘
```

### 3. Input Area (Bottom, Sticky)

**Control:** `MultilineEditControl`

- Background: `Grey19` (unfocused) / `Grey27` (focused)
- Prompt: `> ` with hint `[grey50][Type @ or /][/]`
- Height: 3 lines
- Sticky position: Bottom

**Command Recognition:**
- `@filename` → Opens file picker modal
- `/command` → Opens command palette modal

### 4. Status Bars

**Top Status Bar** (Sticky Top):
```
Mode: Build        Session: demo-1                   12:34:56
```
- Mode: Switches with Tab key
- Session: Click to open session manager
- Clock: Updates every second (async)

**Bottom Status Bar** (Sticky Bottom):
```
Tokens: 1.2k  |  Ctrl+N:New  Ctrl+S:Session  Tab:Mode
```
- Mock metrics
- Keyboard shortcuts reference

### 5. Modal Windows

**File Picker Modal:**
- `ListControl` with fuzzy search
- Shows project files
- Filter as you type
- Enter to select, ESC to cancel

**Session Manager Modal:**
- `ListControl` showing saved sessions
- Format: `demo-1 (3 messages, 5m ago)`
- Navigate with arrows, Enter to load

**Command Palette Modal:**
- `ListControl` with commands:
  - `/analyze` - Run security analysis
  - `/diff` - Show code diff
  - `/test` - Run tests
  - `/explain` - Explain code

---

## Dark Theme Color Palette

```csharp
// Backgrounds
WindowBg = Color.Grey11          // Main window
PanelBg = Color.Grey15           // Message panels
UserMsgBg = Color.Grey19         // User messages
ToolCallBg = Color.Grey23        // Tool panels
InputBg = Color.Grey19           // Input area
InputFocusBg = Color.Grey27      // Focused input

// Text
PrimaryText = Color.Grey93       // Main text
SecondaryText = Color.Grey70     // Dimmed text
AccentText = Color.Cyan1         // Highlights

// Status & Severity
Success = Color.Green
Warning = Color.Yellow
Error = Color.Red
Info = Color.Cyan1

// Syntax-style (for code display)
Keyword = Color.Magenta1
String = Color.Green
Comment = Color.Grey50
Function = Color.Yellow
```

---

## Mock Interactions & Scenarios

### Scenario 1: Security Analysis

1. User types: `/analyze @auth/login.cs`
2. AI shows thinking animation (`. .. ... .`)
3. Tool Call Panel appears: "Read File" with code preview
4. Analysis Panel appears: Security findings with severity colors
5. AI message: Summary recommendations

### Scenario 2: Code Diff

1. User types: `/diff`
2. AI shows thinking
3. Tool Call Panel: Side-by-side diff
   - Left: Original code (red highlights)
   - Right: Modified code (green highlights)
   - Line numbers shown
4. AI message: Summary of changes

### Scenario 3: Test Execution

1. User types: `/test`
2. Tool Panel: Test Discovery (lists tests found)
3. Tool Panel: Test Execution with progress
4. Results Panel: Pass/Fail with details
5. AI message: Test summary

### Interactive Features

**Keyboard Shortcuts:**
- `Tab` - Switch mode (Build ↔ Plan)
- `Ctrl+N` - New session
- `Ctrl+S` - Session manager modal
- `Ctrl+P` - Command palette modal
- `Ctrl+F` - File picker modal
- `ESC` - Close modal or quit
- `Enter` - Send message
- `@` - Triggers file reference
- `/` - Triggers command

**Mouse Interactions:**
- Click tree items → Highlight selection
- Drag splitter → Resize panels
- Scroll wheel → Scroll conversation

**Animations:**
- AI thinking: Animated dots
- Typing simulation: Gradual text appearance
- Live clock: Updates every second

---

## File Structure

```
Examples/
└── OpenCodeShowcase/
    ├── Program.cs                      # Entry point
    ├── OpenCodeShowcase.csproj         # Project file
    ├── AgentStudioWindow.cs            # Main window orchestrator
    │
    ├── Models/
    │   ├── Message.cs                  # Chat message record
    │   ├── ToolCall.cs                 # Tool execution data
    │   └── ProjectNode.cs              # Tree node data
    │
    ├── Components/
    │   ├── ConversationPanel.cs        # Message renderer
    │   ├── ProjectExplorer.cs          # File tree component
    │   ├── InputArea.cs                # Input component
    │   ├── ToolCallPanel.cs            # Tool visualization
    │   └── StatusBars.cs               # Status components
    │
    ├── Services/
    │   ├── MockAiService.cs            # Simulated AI responses
    │   ├── MockToolService.cs          # Simulated tool execution
    │   └── ConversationManager.cs      # State management
    │
    ├── Modals/
    │   ├── FilePickerModal.cs          # File selection
    │   ├── SessionManagerModal.cs      # Session management
    │   └── CommandPaletteModal.cs      # Command selection
    │
    └── Data/
        ├── SampleProject.cs            # Mock project structure
        ├── SampleConversations.cs      # Pre-scripted scenarios
        └── ToolCallTemplates.cs        # Tool output templates
```

---

## Implementation Plan

### Phase 1: Basic Layout (Foundation)
1. Create project structure and AgentStudioWindow.cs
2. Build main layout with HorizontalGridControl
3. Add SplitterControl between panels
4. Create top and bottom status bars (sticky)
5. Apply dark theme colors
6. **Set window to fullscreen mode** (maximize to fill entire terminal)

**Files:** Program.cs, AgentStudioWindow.cs, Models/Message.cs

**Window Configuration:**
- Use fullscreen/maximized mode to utilize entire terminal space
- No fixed width/height - should adapt to terminal size
- Use `WindowBuilder` to create maximized window

### Phase 2: Project Explorer
1. Create ProjectExplorer component with TreeControl
2. Build mock project structure data
3. Implement color-coded file types
4. Add expand/collapse functionality

**Files:** Components/ProjectExplorer.cs, Data/SampleProject.cs

### Phase 3: Conversation Panel
1. Create ConversationPanel with ScrollablePanelControl
2. Implement message rendering (User/AI)
3. Add ToolCallPanel component with nested layout
4. Create AnalysisPanel component
5. Add message separators

**Files:** Components/ConversationPanel.cs, Components/ToolCallPanel.cs

### Phase 4: Input & Commands
1. Create InputArea with MultilineEditControl
2. Add command recognition (@, /)
3. Implement input hints and styling

**Files:** Components/InputArea.cs

### Phase 5: Mock AI Service
1. Create MockAiService with pre-scripted responses
2. Create MockToolService for tool simulation
3. Implement thinking animation
4. Add typing simulation (gradual text reveal)
5. Wire up command handlers

**Files:** Services/MockAiService.cs, Services/MockToolService.cs, Data/SampleConversations.cs

### Phase 6: Modal Dialogs
1. Create FilePickerModal with ListControl
2. Create SessionManagerModal
3. Create CommandPaletteModal
4. Wire up keyboard shortcuts (Ctrl+F, Ctrl+S, Ctrl+P)

**Files:** Modals/FilePickerModal.cs, Modals/SessionManagerModal.cs, Modals/CommandPaletteModal.cs

### Phase 7: Polish & Animations
1. Implement live clock in status bar
2. Add keyboard shortcut handlers
3. Refine visual spacing and margins
4. Add final color touches
5. Test all interactions

**Files:** Components/StatusBars.cs, AgentStudioWindow.cs

---

## Critical Reference Files

**For Layout Patterns:**
- `/home/nick/source/ConsoleEx/Examples/DemoApp/ComprehensiveLayoutWindow.cs` - Complex multi-panel layout with splitters, menus, toolbars

**For Chat UI Patterns:**
- `/home/nick/source/ConsoleEx/Examples/AiAgentChatExample/Program.cs` - Dark theme messages, scrolling, input, animations

**For Tree Control:**
- `/home/nick/source/ConsoleEx/SharpConsoleUI/Controls/TreeControl.cs` - Tree implementation details
- `/home/nick/source/ConsoleEx/Examples/DemoApp/Program.cs` - File explorer example

**For Grid Layouts:**
- `/home/nick/source/ConsoleEx/SharpConsoleUI/Controls/HorizontalGridControl.cs` - Multi-column layouts
- `/home/nick/source/ConsoleEx/SharpConsoleUI/Controls/SplitterControl.cs` - Resizable dividers

**For Modals:**
- `/home/nick/source/ConsoleEx/SharpConsoleUI/Builders/WindowBuilder.cs` - Modal window creation

---

## Verification & Testing

### Build Command
```bash
dotnet build Examples/OpenCodeShowcase/OpenCodeShowcase.csproj
```

### Run Command
```bash
dotnet run --project Examples/OpenCodeShowcase
```

### Test Checklist

**Layout & Components:**
- [ ] Window opens with correct title
- [ ] 2-panel layout with splitter
- [ ] Splitter is draggable and resizes panels
- [ ] Top and bottom status bars visible
- [ ] Project tree shows on left
- [ ] Conversation area shows on right

**Project Explorer:**
- [ ] Tree shows mock project structure
- [ ] Files color-coded by type
- [ ] Folders can expand/collapse
- [ ] Selection highlighting works

**Conversation Area:**
- [ ] User messages display correctly
- [ ] AI messages display with timestamp
- [ ] Tool call panels have borders and formatting
- [ ] Analysis panels show with severity colors
- [ ] Scrolling works with mouse wheel

**Input & Commands:**
- [ ] Input area accepts text
- [ ] Typing `@` shows hint
- [ ] Typing `/` shows hint
- [ ] Enter sends message

**Mock AI:**
- [ ] Thinking animation cycles (. .. ...)
- [ ] AI responses appear after delay
- [ ] Tool panels render with code
- [ ] Commands trigger appropriate scenarios

**Modals:**
- [ ] Ctrl+F opens file picker
- [ ] Ctrl+S opens session manager
- [ ] Ctrl+P opens command palette
- [ ] ESC closes modals
- [ ] Modal navigation with arrows works

**Status & Animations:**
- [ ] Clock updates every second
- [ ] Mode displays correctly
- [ ] Tab switches mode
- [ ] Token count shows

**Visual Polish:**
- [ ] Dark theme consistent throughout
- [ ] Colors readable and professional
- [ ] Spacing and margins look clean
- [ ] No rendering glitches

---

## Success Criteria

✅ **Visually Impressive** - Professional dark theme, excellent hierarchy
✅ **Feature-Rich** - Showcases 10+ SharpConsoleUI controls
✅ **Interactive** - Keyboard shortcuts, modals, animations work smoothly
✅ **Clean Code** - Well-organized, maintainable architecture
✅ **Clear Purpose** - Obviously a showcase, not a real agent
✅ **Modern .NET** - Uses async/await, records, fluent builders
✅ **Smooth Performance** - No lag or rendering issues

---

## What This Is NOT

- ❌ Not a real AI coding agent
- ❌ Not connected to actual LLMs
- ❌ Not performing real file operations
- ❌ Not executing real shell commands
- ❌ Not analyzing real code

## What This IS

- ✅ A beautiful TUI showcase
- ✅ An OpenCode-inspired demo
- ✅ A SharpConsoleUI feature demonstration
- ✅ A modern .NET example project
- ✅ A starting point for real applications
