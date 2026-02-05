// -----------------------------------------------------------------------
// SnakeGame - Classic Snake using ConsoleEx Frame Buffer
// Demonstrates direct CharacterBuffer manipulation for game rendering
// with proper layout using HorizontalGridControl
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

namespace SnakeGame;

enum Direction { Up, Down, Left, Right }
enum GameState { Playing, Paused, GameOver }

class Program
{
    // Window system
    private static ConsoleWindowSystem? _windowSystem;
    private static Window? _gameWindow;

    // Layout
    private static HorizontalGridControl? _mainGrid;
    private static ColumnContainer? _sidebarColumn;
    private static ColumnContainer? _canvasColumn;
    private const int SidebarWidth = 22;

    // Sidebar controls (we'll update these)
    private static MarkupControl? _scoreDisplay;
    private static MarkupControl? _highScoreDisplay;
    private static MarkupControl? _speedDisplay;
    private static MarkupControl? _stateDisplay;

    // Game state
    private static List<(int X, int Y)> _snake = new();
    private static (int X, int Y) _food;
    private static Direction _direction = Direction.Right;
    private static Direction _nextDirection = Direction.Right;
    private static GameState _gameState = GameState.Playing;
    private static int _score = 0;
    private static int _highScore = 0;
    private static Random _random = new();

    // Game timing
    private static System.Timers.Timer? _gameTimer;
    private static int _gameSpeed = 100;

    // Canvas geometry (calculated from layout)
    private static int _canvasWidth;
    private static int _canvasHeight;
    private static int _canvasOffsetX; // Where canvas starts in content buffer

    // Visual settings
    private static readonly Color SnakeHeadColor = Color.Lime;
    private static readonly Color SnakeBodyColor = Color.Green;
    private static readonly Color FoodColor = Color.Red;
    private static readonly Color CanvasColor = Color.Grey11;
    private static readonly Color SidebarBg = Color.Grey19;
    private static readonly char SnakeChar = '█';
    private static readonly char FoodChar = '●';

    static async Task<int> Main(string[] args)
    {
        try
        {
            _windowSystem = new ConsoleWindowSystem(
                new NetConsoleDriver(RenderMode.Buffer),
                options: new ConsoleWindowSystemOptions(
                    StatusBarOptions: new StatusBarOptions(ShowTaskBar: false)
                ));

            _windowSystem.StatusBarStateService.TopStatus = "SNAKE - Arrows/WASD: Move | P: Pause | R: Restart | Esc: Quit";

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                _windowSystem?.Shutdown(0);
            };

            CreateGameWindow();
            await Task.Run(() => _windowSystem.Run());
            return 0;
        }
        catch (Exception ex)
        {
            Console.Clear();
            AnsiConsole.WriteException(ex);
            return 1;
        }
        finally
        {
            _gameTimer?.Stop();
            _gameTimer?.Dispose();
        }
    }

    private static void CreateGameWindow()
    {
        if (_windowSystem == null) return;

        _gameWindow = new WindowBuilder(_windowSystem)
            .WithTitle("SNAKE")
            .Resizable(false)
            .Movable(false)
            .Closable(false)
            .Minimizable(false)
            .Maximizable(false)
            .WithBorderStyle(BorderStyle.Single)
            .Build();

        // Create the two-column layout
        _mainGrid = new HorizontalGridControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Left sidebar (fixed width)
        _sidebarColumn = new ColumnContainer(_mainGrid) { Width = SidebarWidth };
        _sidebarColumn.BackgroundColor = SidebarBg;
        CreateSidebarContent(_sidebarColumn);
        _mainGrid.AddColumn(_sidebarColumn);

        // Right canvas column (fills remaining space)
        _canvasColumn = new ColumnContainer(_mainGrid);
        _canvasColumn.BackgroundColor = CanvasColor;
        // Empty - we render the game directly to the buffer
        _mainGrid.AddColumn(_canvasColumn);

        _gameWindow.AddControl(_mainGrid);

        // Hook into rendering pipeline
        if (_gameWindow.Renderer != null)
        {
            _gameWindow.Renderer.PostBufferPaint += RenderGame;
        }

        // Handle keyboard
        _gameWindow.KeyPressed += HandleKeyPress;

        // Handle resize - restart game!
        _gameWindow.OnResize += (s, e) =>
        {
            // Recalculate canvas geometry and restart
            CalculateCanvasGeometry();
            RestartGame();
        };

        _windowSystem.AddWindow(_gameWindow);
        _gameWindow.State = WindowState.Maximized;

        // Initialize on first activation
        _gameWindow.Activated += (s, e) =>
        {
            if (_snake.Count == 0)
            {
                CalculateCanvasGeometry();
                InitializeGame();
                StartGameLoop();
            }
        };
    }

    private static void CreateSidebarContent(ColumnContainer sidebar)
    {
        // Title
        sidebar.AddContent(new MarkupControl(new List<string>
        {
            "",
            "[bold yellow]   SNAKE[/]",
            "[dim]───────────────────[/]"
        }));

        // Score section
        sidebar.AddContent(new MarkupControl(new List<string> { "", "[white] SCORE[/]" }));
        _scoreDisplay = new MarkupControl(new List<string> { " [bold cyan]0[/]" });
        sidebar.AddContent(_scoreDisplay);

        // High score
        sidebar.AddContent(new MarkupControl(new List<string> { "", "[white] HIGH SCORE[/]" }));
        _highScoreDisplay = new MarkupControl(new List<string> { " [bold yellow]0[/]" });
        sidebar.AddContent(_highScoreDisplay);

        // Speed
        sidebar.AddContent(new MarkupControl(new List<string> { "", "[white] SPEED[/]" }));
        _speedDisplay = new MarkupControl(new List<string> { " [bold green]1[/]" });
        sidebar.AddContent(_speedDisplay);

        // Game state
        sidebar.AddContent(new MarkupControl(new List<string> { "" }));
        _stateDisplay = new MarkupControl(new List<string> { " [bold lime]PLAYING[/]" });
        sidebar.AddContent(_stateDisplay);

        // Controls
        sidebar.AddContent(new MarkupControl(new List<string>
        {
            "",
            "[dim]───────────────────[/]",
            "[white] CONTROLS[/]",
            "",
            " [cyan]↑↓←→[/] Move",
            " [cyan]WASD[/] Move",
            " [cyan]P[/]    Pause",
            " [cyan]R[/]    Restart",
            " [cyan]Esc[/]  Quit"
        }));
    }

    private static void CalculateCanvasGeometry()
    {
        if (_gameWindow == null) return;

        // PostBufferPaint buffer IS the content area (borders excluded)
        // Canvas starts right after the sidebar
        _canvasOffsetX = SidebarWidth;

        // Content area dimensions from window
        // ContentWidth = Width - 2 (borders), ContentHeight = Height - 1 (border, title is inline)
        int contentWidth = _gameWindow.Width - 2;
        int contentHeight = _gameWindow.Height - 1;

        _canvasWidth = contentWidth - SidebarWidth;
        _canvasHeight = contentHeight;
    }

    private static void InitializeGame()
    {
        _snake.Clear();
        _direction = Direction.Right;
        _nextDirection = Direction.Right;
        _gameState = GameState.Playing;
        _score = 0;

        // Start snake in center of canvas
        int startX = _canvasWidth / 2;
        int startY = _canvasHeight / 2;

        _snake.Add((startX, startY));
        _snake.Add((startX - 1, startY));
        _snake.Add((startX - 2, startY));

        SpawnFood();
        UpdateSidebarDisplays();
        _gameWindow?.Invalidate(redrawAll: true);
    }

    private static void RestartGame()
    {
        _gameTimer?.Stop();
        _gameSpeed = 100;
        InitializeGame();
        StartGameLoop();
    }

    private static void StartGameLoop()
    {
        _gameTimer?.Stop();
        _gameTimer?.Dispose();

        _gameTimer = new System.Timers.Timer(_gameSpeed);
        _gameTimer.Elapsed += (s, e) =>
        {
            if (_gameState == GameState.Playing)
            {
                UpdateGame();
                _gameWindow?.Invalidate(redrawAll: true);
            }
        };
        _gameTimer.Start();
    }

    private static void SpawnFood()
    {
        int attempts = 0;
        do
        {
            _food = (_random.Next(0, _canvasWidth), _random.Next(0, _canvasHeight));
            attempts++;
        } while (_snake.Contains(_food) && attempts < 1000);
    }

    private static void UpdateGame()
    {
        if (_snake.Count == 0) return;

        _direction = _nextDirection;

        var head = _snake[0];
        var newHead = _direction switch
        {
            Direction.Up => (head.X, head.Y - 1),
            Direction.Down => (head.X, head.Y + 1),
            Direction.Left => (head.X - 1, head.Y),
            Direction.Right => (head.X + 1, head.Y),
            _ => head
        };

        // Wall collision
        if (newHead.Item1 < 0 || newHead.Item1 >= _canvasWidth ||
            newHead.Item2 < 0 || newHead.Item2 >= _canvasHeight)
        {
            GameOver();
            return;
        }

        // Self collision
        if (_snake.Take(_snake.Count - 1).Contains(newHead))
        {
            GameOver();
            return;
        }

        _snake.Insert(0, newHead);

        // Food collision
        if (newHead == _food)
        {
            _score += 10;
            if (_score > _highScore) _highScore = _score;

            // Speed up every 50 points
            if (_score % 50 == 0 && _gameSpeed > 40)
            {
                _gameSpeed -= 10;
                _gameTimer?.Stop();
                _gameTimer = new System.Timers.Timer(_gameSpeed);
                _gameTimer.Elapsed += (s, e) =>
                {
                    if (_gameState == GameState.Playing)
                    {
                        UpdateGame();
                        _gameWindow?.Invalidate(redrawAll: true);
                    }
                };
                _gameTimer.Start();
            }

            SpawnFood();
            UpdateSidebarDisplays();
        }
        else
        {
            _snake.RemoveAt(_snake.Count - 1);
        }
    }

    private static void GameOver()
    {
        _gameState = GameState.GameOver;
        _gameTimer?.Stop();
        UpdateSidebarDisplays();
    }

    private static void UpdateSidebarDisplays()
    {
        _scoreDisplay?.SetContent(new List<string> { $" [bold cyan]{_score}[/]" });
        _highScoreDisplay?.SetContent(new List<string> { $" [bold yellow]{_highScore}[/]" });

        int speedLevel = (200 - _gameSpeed) / 10;
        _speedDisplay?.SetContent(new List<string> { $" [bold green]{speedLevel}[/]" });

        var stateText = _gameState switch
        {
            GameState.Playing => " [bold lime]PLAYING[/]",
            GameState.Paused => " [bold yellow]PAUSED[/]",
            GameState.GameOver => " [bold red]GAME OVER[/]",
            _ => ""
        };
        _stateDisplay?.SetContent(new List<string> { stateText });
    }

    private static void HandleKeyPress(object? sender, KeyPressedEventArgs e)
    {
        switch (e.KeyInfo.Key)
        {
            case ConsoleKey.UpArrow:
            case ConsoleKey.W:
                if (_direction != Direction.Down)
                    _nextDirection = Direction.Up;
                e.Handled = true;
                break;

            case ConsoleKey.DownArrow:
            case ConsoleKey.S:
                if (_direction != Direction.Up)
                    _nextDirection = Direction.Down;
                e.Handled = true;
                break;

            case ConsoleKey.LeftArrow:
            case ConsoleKey.A:
                if (_direction != Direction.Right)
                    _nextDirection = Direction.Left;
                e.Handled = true;
                break;

            case ConsoleKey.RightArrow:
            case ConsoleKey.D:
                if (_direction != Direction.Left)
                    _nextDirection = Direction.Right;
                e.Handled = true;
                break;

            case ConsoleKey.P:
            case ConsoleKey.Spacebar:
                if (_gameState == GameState.Playing)
                {
                    _gameState = GameState.Paused;
                    _gameTimer?.Stop();
                }
                else if (_gameState == GameState.Paused)
                {
                    _gameState = GameState.Playing;
                    _gameTimer?.Start();
                }
                UpdateSidebarDisplays();
                _gameWindow?.Invalidate(redrawAll: true);
                e.Handled = true;
                break;

            case ConsoleKey.R:
                RestartGame();
                e.Handled = true;
                break;

            case ConsoleKey.Escape:
                _windowSystem?.Shutdown();
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Direct frame buffer rendering - the game canvas
    /// Buffer is the CONTENT area (borders already excluded)
    /// </summary>
    private static void RenderGame(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
    {
        // Clear canvas area only (starts at _canvasOffsetX, goes to end of buffer)
        for (int y = 0; y < buffer.Height; y++)
        {
            for (int x = _canvasOffsetX; x < buffer.Width; x++)
            {
                buffer.SetCell(x, y, ' ', Color.White, CanvasColor);
            }
        }

        // Draw snake
        for (int i = _snake.Count - 1; i >= 0; i--)
        {
            var segment = _snake[i];
            int screenX = segment.X + _canvasOffsetX;
            int screenY = segment.Y;

            if (screenX >= _canvasOffsetX && screenX < buffer.Width &&
                screenY >= 0 && screenY < buffer.Height)
            {
                var color = i == 0 ? SnakeHeadColor : SnakeBodyColor;
                buffer.SetCell(screenX, screenY, SnakeChar, color, CanvasColor);
            }
        }

        // Draw food
        int foodScreenX = _food.X + _canvasOffsetX;
        int foodScreenY = _food.Y;
        if (foodScreenX >= _canvasOffsetX && foodScreenX < buffer.Width &&
            foodScreenY >= 0 && foodScreenY < buffer.Height)
        {
            buffer.SetCell(foodScreenX, foodScreenY, FoodChar, FoodColor, CanvasColor);
        }

        // Draw overlays for pause/game over
        if (_gameState == GameState.Paused)
        {
            int centerX = _canvasOffsetX + _canvasWidth / 2;
            int centerY = buffer.Height / 2;
            DrawCenteredBox(buffer, centerX, centerY, "PAUSED", "Press P to continue", Color.Yellow, Color.DarkBlue);
        }
        else if (_gameState == GameState.GameOver)
        {
            int centerX = _canvasOffsetX + _canvasWidth / 2;
            int centerY = buffer.Height / 2;
            DrawCenteredBox(buffer, centerX, centerY, "GAME OVER", $"Score: {_score} | Press R", Color.White, Color.DarkRed);
        }
    }

    private static void DrawCenteredBox(CharacterBuffer buffer, int centerX, int centerY,
        string line1, string line2, Color fg, Color bg)
    {
        int boxWidth = Math.Max(line1.Length, line2.Length) + 4;
        int boxHeight = 4;
        int boxLeft = centerX - boxWidth / 2;
        int boxTop = centerY - boxHeight / 2;

        // Draw box background
        for (int y = boxTop; y < boxTop + boxHeight; y++)
        {
            for (int x = boxLeft; x < boxLeft + boxWidth; x++)
            {
                if (x >= 0 && x < buffer.Width && y >= 0 && y < buffer.Height)
                    buffer.SetCell(x, y, ' ', fg, bg);
            }
        }

        // Draw text
        DrawText(buffer, centerX - line1.Length / 2, boxTop + 1, line1, fg, bg);
        DrawText(buffer, centerX - line2.Length / 2, boxTop + 2, line2, fg, bg);
    }

    private static void DrawText(CharacterBuffer buffer, int x, int y, string text, Color fg, Color bg)
    {
        for (int i = 0; i < text.Length; i++)
        {
            int px = x + i;
            if (px >= 0 && px < buffer.Width && y >= 0 && y < buffer.Height)
                buffer.SetCell(px, y, text[i], fg, bg);
        }
    }
}
