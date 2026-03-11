using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

internal static class DropdownWindow
{
    private const int WindowWidth = 90;
    private const int WindowHeight = 28;
    private const int LeftColumnWidth = 45;
    private const int DefaultCuisineIndex = 0;
    private const int DefaultDietaryIndex = 0;
    private const int DefaultSpiceIndex = 1;
    private const int DefaultBudgetIndex = 1;
    private const int DefaultServingsIndex = 2;

    public static Window Create(ConsoleWindowSystem ws)
    {
        var summaryMarkup = Controls.Markup()
            .AddLines("[bold]Your Meal Plan[/]", "", "[dim]Configure your preferences on the left[/]")
            .WithMargin(1, 1, 1, 0)
            .Build();

        var header = Controls.Markup("[bold yellow]  Meal Planner[/]")
            .StickyTop()
            .Build();

        var statusBar = Controls.Markup("[dim]Use \u2191\u2193 to browse | Type to search | Esc: Close[/]")
            .StickyBottom()
            .Build();

        var cuisineLabel = Controls.Markup("[bold]Cuisine Type[/]").WithMargin(1, 1, 1, 0).Build();
        var cuisine = Controls.Dropdown("Choose cuisine...")
            .AddItem(new DropdownItem("Japanese", "\u25cf", Color.Red) { Tag = "Japanese" })
            .AddItem(new DropdownItem("Italian", "\u25cf", Color.Green) { Tag = "Italian" })
            .AddItem(new DropdownItem("Mexican", "\u25cf", Color.Orange1) { Tag = "Mexican" })
            .AddItem(new DropdownItem("Indian", "\u25cf", Color.Yellow) { Tag = "Indian" })
            .AddItem(new DropdownItem("Thai", "\u25cf", Color.Magenta1) { Tag = "Thai" })
            .AddItem(new DropdownItem("Chinese", "\u25cf", Color.Red) { Tag = "Chinese" })
            .AddItem(new DropdownItem("French", "\u25cf", Color.Blue) { Tag = "French" })
            .AddItem(new DropdownItem("Korean", "\u25cf", Color.Cyan1) { Tag = "Korean" })
            .AddItem(new DropdownItem("Mediterranean", "\u25cf", Color.Green) { Tag = "Mediterranean" })
            .AddItem(new DropdownItem("Greek", "\u25cf", Color.Cyan1) { Tag = "Greek" })
            .AddItem(new DropdownItem("Vietnamese", "\u25cf", Color.Yellow) { Tag = "Vietnamese" })
            .AddItem(new DropdownItem("American", "\u25cf", Color.Blue) { Tag = "American" })
            .SelectedIndex(DefaultCuisineIndex)
            .WithMargin(1, 0, 1, 1)
            .Build();

        var dietaryLabel = Controls.Markup("[bold]Dietary Restriction[/]").WithMargin(1, 0, 1, 0).Build();
        var dietary = Controls.Dropdown("Choose diet...")
            .AddItems("None", "Vegetarian", "Vegan", "Gluten-Free", "Keto", "Paleo", "Pescatarian")
            .SelectedIndex(DefaultDietaryIndex)
            .WithMargin(1, 0, 1, 1)
            .Build();

        var spiceLabel = Controls.Markup("[bold]Spice Level[/]").WithMargin(1, 0, 1, 0).Build();
        var spice = Controls.Dropdown("Choose spice...")
            .AddItem(new DropdownItem("Mild", "\u25cf", Color.Green))
            .AddItem(new DropdownItem("Medium", "\u25cf", Color.Yellow))
            .AddItem(new DropdownItem("Hot", "\u25cf", Color.Orange1))
            .AddItem(new DropdownItem("Extra Hot", "\u25cf", Color.Red))
            .AddItem(new DropdownItem("Inferno", "\u25cf", Color.DarkRed))
            .SelectedIndex(DefaultSpiceIndex)
            .WithMargin(1, 0, 1, 1)
            .Build();

        var budgetLabel = Controls.Markup("[bold]Budget[/]").WithMargin(1, 0, 1, 0).Build();
        var budget = Controls.Dropdown("Choose budget...")
            .AddItems("$ Budget", "$$ Moderate", "$$$ Premium", "$$$$ Luxury")
            .SelectedIndex(DefaultBudgetIndex)
            .WithMargin(1, 0, 1, 1)
            .Build();

        var servingsLabel = Controls.Markup("[bold]Servings[/]").WithMargin(1, 0, 1, 0).Build();
        var servings = Controls.Dropdown("Choose servings...")
            .AddItems("1 person", "2 people", "3 people", "4 people", "6 people", "8 people")
            .SelectedIndex(DefaultServingsIndex)
            .WithMargin(1, 0, 1, 1)
            .Build();

        void UpdateSummary(object? s, object? e)
        {
            string cuisineText = GetSelectedText(cuisine);
            string dietText = GetSelectedText(dietary);
            string spiceText = GetSelectedText(spice);
            string budgetText = GetSelectedText(budget);
            string servingsText = GetSelectedText(servings);

            string cuisineKey = GetCuisineKey(cuisine);
            var meals = GetSuggestedMeals(cuisineKey, dietText);

            summaryMarkup.SetContent(new List<string>
            {
                "[bold cyan]Your Meal Plan[/]",
                "",
                $"[dim]Cuisine:[/]     {cuisineText}",
                $"[dim]Dietary:[/]     {dietText}",
                $"[dim]Spice:[/]       {spiceText}",
                $"[dim]Budget:[/]      {budgetText}",
                $"[dim]Servings:[/]    {servingsText}",
                "",
                "[bold yellow]Suggested Meals[/]",
                "",
                $"  [green]\u2022[/] {meals[0]}",
                $"  [green]\u2022[/] {meals[1]}",
                $"  [green]\u2022[/] {meals[2]}",
            });
        }

        cuisine.SelectedIndexChanged += (s, _) => UpdateSummary(s, null);
        dietary.SelectedIndexChanged += (s, _) => UpdateSummary(s, null);
        spice.SelectedIndexChanged += (s, _) => UpdateSummary(s, null);
        budget.SelectedIndexChanged += (s, _) => UpdateSummary(s, null);
        servings.SelectedIndexChanged += (s, _) => UpdateSummary(s, null);

        // Set initial summary
        UpdateSummary(null, null);

        var leftPanel = Controls.ScrollablePanel()
            .AddControl(cuisineLabel).AddControl(cuisine)
            .AddControl(dietaryLabel).AddControl(dietary)
            .AddControl(spiceLabel).AddControl(spice)
            .AddControl(budgetLabel).AddControl(budget)
            .AddControl(servingsLabel).AddControl(servings)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        var rightPanel = Controls.ScrollablePanel()
            .AddControl(summaryMarkup)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        var grid = Controls.HorizontalGrid()
            .Column(col => col.Width(LeftColumnWidth).Add(leftPanel))
            .Column(col => col.Flex().Add(rightPanel))
            .WithSplitterAfter(0)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("Meal Planner")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .AddControls(header, grid, statusBar)
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow((Window)sender!);
                    e.Handled = true;
                }
            })
            .BuildAndShow();
    }

    private static string GetSelectedText(DropdownControl dropdown)
    {
        var item = dropdown.SelectedItem;
        return item?.Text ?? "None";
    }

    private static string GetCuisineKey(DropdownControl dropdown)
    {
        var item = dropdown.SelectedItem;
        return item?.Tag as string ?? "Japanese";
    }

    private static string[] GetSuggestedMeals(string cuisine, string diet)
    {
        bool isVeg = diet is "Vegetarian" or "Vegan";
        bool isSeafood = diet == "Pescatarian";

        return cuisine switch
        {
            "Japanese" when isVeg => new[] { "Vegetable Tempura", "Edamame Rice Bowl", "Miso Soup Set" },
            "Japanese" when isSeafood => new[] { "Salmon Sashimi", "Shrimp Tempura", "Tuna Poke Bowl" },
            "Japanese" => new[] { "Tonkotsu Ramen", "Chicken Katsu Curry", "Sushi Platter" },
            "Italian" when isVeg => new[] { "Margherita Pizza", "Pasta Primavera", "Risotto ai Funghi" },
            "Italian" => new[] { "Carbonara", "Osso Buco", "Lasagna Bolognese" },
            "Mexican" when isVeg => new[] { "Bean Burrito Bowl", "Veggie Quesadilla", "Elote Salad" },
            "Mexican" => new[] { "Carne Asada Tacos", "Chicken Enchiladas", "Birria Stew" },
            "Indian" when isVeg => new[] { "Paneer Tikka Masala", "Dal Makhani", "Chana Masala" },
            "Indian" => new[] { "Butter Chicken", "Lamb Biryani", "Tandoori Prawns" },
            "Thai" when isVeg => new[] { "Green Curry Tofu", "Pad Thai (Veggie)", "Tom Kha Soup" },
            "Thai" => new[] { "Pad Thai", "Massaman Curry", "Tom Yum Soup" },
            "Chinese" when isVeg => new[] { "Mapo Tofu", "Veggie Fried Rice", "Hot and Sour Soup" },
            "Chinese" => new[] { "Kung Pao Chicken", "Peking Duck", "Dim Sum Platter" },
            "French" when isVeg => new[] { "Ratatouille", "French Onion Soup", "Mushroom Galette" },
            "French" => new[] { "Coq au Vin", "Beef Bourguignon", "Duck Confit" },
            "Korean" when isVeg => new[] { "Bibimbap (Veggie)", "Kimchi Fried Rice", "Tofu Stew" },
            "Korean" => new[] { "Korean BBQ Beef", "Kimchi Jjigae", "Japchae" },
            "Mediterranean" when isVeg => new[] { "Falafel Plate", "Greek Salad", "Hummus Platter" },
            "Mediterranean" => new[] { "Lamb Kofta", "Chicken Shawarma", "Grilled Sea Bass" },
            "Greek" when isVeg => new[] { "Spanakopita", "Stuffed Peppers", "Gigantes Plaki" },
            "Greek" => new[] { "Moussaka", "Souvlaki Plate", "Gyro Platter" },
            "Vietnamese" when isVeg => new[] { "Pho Chay", "Banh Mi Tofu", "Spring Rolls" },
            "Vietnamese" => new[] { "Pho Bo", "Banh Mi", "Bun Cha" },
            "American" when isVeg => new[] { "Veggie Burger", "Mac and Cheese", "Southwest Salad" },
            "American" => new[] { "Classic Cheeseburger", "BBQ Ribs", "Fried Chicken" },
            _ => new[] { "Chef's Special", "Daily Recommendation", "House Favorite" },
        };
    }
}
