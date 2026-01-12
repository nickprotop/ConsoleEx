using AgentStudio.Models;

namespace AgentStudio.Data;

/// <summary>
/// Pre-scripted conversation scenarios for demo purposes
/// </summary>
public static class SampleConversations
{
    /// <summary>
    /// Creates a demo security analysis conversation
    /// </summary>
    public static List<Message> SecurityAnalysisScenario()
    {
        var now = DateTime.Now;

        return new List<Message>
        {
            new Message(
                MessageRole.Assistant,
                "I'll analyze the authentication code for security issues...",
                now.AddSeconds(-1),
                0.2
            ),

            new Message(
                MessageRole.Assistant,
                "Reading file...",
                now.AddSeconds(-8),
                0.5,
                ToolCall: new ToolCall(
                    ToolType.ReadFile,
                    "Read File",
                    "File: src/auth/login.cs",
                    "[magenta1]using[/] System.Security;\n" +
                    "[magenta1]using[/] System.Text;\n\n" +
                    "[magenta1]public[/] [magenta1]class[/] LoginService\n" +
                    "{\n" +
                    "    [magenta1]public[/] [magenta1]bool[/] ValidateCredentials([magenta1]string[/] username, [magenta1]string[/] password)\n" +
                    "    {\n" +
                    "        [grey50]// TODO: Implement proper password hashing[/]\n" +
                    "        [magenta1]return[/] password == [green]\"admin123\"[/];\n" +
                    "    }\n" +
                    "}",
                    ToolStatus.Complete,
                    0.5
                )
            ),

            new Message(
                MessageRole.Assistant,
                "Analysis complete. Found 3 security issues:",
                now.AddSeconds(-5),
                0.8,
                Findings: new List<Finding>
                {
                    new Finding(
                        Severity.High,
                        "Plain text password comparison",
                        "line 23"
                    ),
                    new Finding(
                        Severity.Medium,
                        "No rate limiting on login attempts",
                        "line 45"
                    ),
                    new Finding(
                        Severity.Low,
                        "Weak default password in example",
                        "line 23"
                    )
                }
            ),

            new Message(
                MessageRole.Assistant,
                "**Recommendations:**\n\n" +
                "1. Implement proper password hashing using BCrypt or Argon2\n" +
                "2. Add rate limiting to prevent brute force attacks\n" +
                "3. Remove hardcoded credentials from the codebase\n" +
                "4. Consider implementing multi-factor authentication",
                now.AddSeconds(-2),
                1.2
            )
        };
    }

    /// <summary>
    /// Creates a demo code diff scenario
    /// </summary>
    public static List<Message> CodeDiffScenario()
    {
        var now = DateTime.Now;

        return new List<Message>
        {
            new Message(
                MessageRole.Assistant,
                "Generating diff for recent changes...",
                now.AddSeconds(-1),
                0.3,
                ToolCall: new ToolCall(
                    ToolType.Diff,
                    "Code Diff",
                    "File: src/auth/password.cs",
                    "[red]-    return password == storedPassword;[/]\n" +
                    "[green]+    return BCrypt.Verify(password, storedPasswordHash);[/]\n\n" +
                    "[grey50]@@ Changes: 1 file, +1 insertion, -1 deletion @@[/]",
                    ToolStatus.Complete,
                    0.4
                )
            ),

            new Message(
                MessageRole.Assistant,
                "Updated password validation to use BCrypt hashing instead of plain text comparison.",
                now.AddSeconds(-5),
                0.5
            )
        };
    }

    /// <summary>
    /// Creates a demo test execution scenario
    /// </summary>
    public static List<Message> TestExecutionScenario()
    {
        var now = DateTime.Now;

        return new List<Message>
        {
            new Message(
                MessageRole.Assistant,
                "Running test suite...",
                now.AddSeconds(-1),
                0.2
            ),

            new Message(
                MessageRole.Assistant,
                "Test execution in progress...",
                now.AddSeconds(-10),
                1.5,
                ToolCall: new ToolCall(
                    ToolType.Test,
                    "Run Tests",
                    "Suite: AuthenticationTests",
                    "[green]✓[/] LoginTests.ValidCredentials_ShouldSucceed\n" +
                    "[green]✓[/] LoginTests.InvalidPassword_ShouldFail\n" +
                    "[green]✓[/] LoginTests.EmptyUsername_ShouldFail\n" +
                    "[red]✗[/] LoginTests.RateLimiting_ShouldBlock\n\n" +
                    "[grey70]Passed: 3 | Failed: 1 | Total: 4[/]",
                    ToolStatus.Complete,
                    1.5
                )
            ),

            new Message(
                MessageRole.Assistant,
                "Test suite completed with 1 failure. The rate limiting test is failing because the feature hasn't been implemented yet.",
                now.AddSeconds(-7),
                0.6,
                Findings: new List<Finding>
                {
                    new Finding(
                        Severity.Medium,
                        "Rate limiting test failing - feature not implemented",
                        "LoginTests.cs:45"
                    )
                }
            )
        };
    }
}
