// -----------------------------------------------------------------------
// SampleProject - Mock project structure data
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using Spectre.Console;
using TreeNode = SharpConsoleUI.Controls.TreeNode;

namespace AgentStudio.Data;

/// <summary>
/// Provides mock project structure for the tree control
/// </summary>
public static class SampleProject
{
    public static void PopulateProjectTree(TreeControl tree)
    {
        var root = tree.AddRootNode("📁 MyProject");
        root.TextColor = Color.Cyan1;
        root.IsExpanded = true;

        // src/ folder
        var src = root.AddChild("📁 src/");
        src.TextColor = Color.Cyan1;
        src.IsExpanded = true;

        // src/auth/
        var auth = src.AddChild("📁 auth/");
        auth.TextColor = Color.Cyan1;
        auth.AddChild("📄 login.cs").TextColor = Color.Green;
        auth.AddChild("📄 jwt.cs").TextColor = Color.Green;
        auth.AddChild("📄 password.cs").TextColor = Color.Green;

        // src/api/
        var api = src.AddChild("📁 api/");
        api.TextColor = Color.Cyan1;
        api.AddChild("📄 users.cs").TextColor = Color.Green;
        api.AddChild("📄 products.cs").TextColor = Color.Green;

        // src/models/
        var models = src.AddChild("📁 models/");
        models.TextColor = Color.Cyan1;
        models.AddChild("📄 User.cs").TextColor = Color.Green;
        models.AddChild("📄 Product.cs").TextColor = Color.Green;

        // tests/ folder
        var tests = root.AddChild("📁 tests/");
        tests.TextColor = Color.Cyan1;
        tests.IsExpanded = true;

        var authTests = tests.AddChild("📁 auth/");
        authTests.TextColor = Color.Cyan1;
        authTests.AddChild("🧪 LoginTests.cs").TextColor = Color.Magenta1;

        tests.AddChild("🧪 UserApiTests.cs").TextColor = Color.Magenta1;

        // docs/ folder
        var docs = root.AddChild("📁 docs/");
        docs.TextColor = Color.Cyan1;
        docs.AddChild("📝 README.md").TextColor = Color.Grey70;
    }
}
