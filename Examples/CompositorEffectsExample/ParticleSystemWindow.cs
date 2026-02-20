using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using System.Numerics;

namespace CompositorEffectsExample;

/// <summary>
/// Type of particle effect.
/// </summary>
public enum ParticleType
{
	Snow,      // Falls slowly with drift
	Rain,      // Falls fast straight down
	Sparks,    // Rise then fall with gravity
	Confetti   // Tumbles with rotation
}

/// <summary>
/// Individual particle in the system.
/// </summary>
public struct Particle
{
	public Vector2 Position;
	public Vector2 Velocity;
	public float Lifetime;      // Seconds remaining
	public float MaxLifetime;   // Total lifespan
	public ParticleType Type;
	public char Character;
	public Color Color;
}

/// <summary>
/// Reusable particle system for weather and decorative effects.
/// </summary>
public class ParticleSystem
{
	private readonly List<Particle> _particles = new();
	private readonly Random _random = new();
	private readonly int _maxParticles;
	private readonly int _spawnWidth;
	private readonly int _spawnHeight;
	private float _spawnRate;  // Particles per second
	private float _spawnAccumulator;
	private ParticleType _particleType;

	public ParticleSystem(int maxParticles, int spawnWidth, int spawnHeight)
	{
		_maxParticles = maxParticles;
		_spawnWidth = spawnWidth;
		_spawnHeight = spawnHeight;
	}

	public void SetSpawnRate(float particlesPerSecond)
	{
		_spawnRate = particlesPerSecond;
	}

	public void SetParticleType(ParticleType type)
	{
		_particleType = type;
	}

	public void Update(float deltaTime)
	{
		// Update existing particles
		for (int i = _particles.Count - 1; i >= 0; i--)
		{
			var p = _particles[i];

			// Update physics
			p.Position += p.Velocity * deltaTime;
			p.Lifetime -= deltaTime;

			// Apply type-specific forces
			switch (p.Type)
			{
				case ParticleType.Snow:
					// Drift left/right
					p.Velocity.X += (float)(_random.NextDouble() - 0.5) * 2.0f;
					p.Velocity.Y += 1.0f * deltaTime; // Slow fall
					// Clamp drift speed
					p.Velocity.X = Math.Clamp(p.Velocity.X, -3f, 3f);
					break;

				case ParticleType.Rain:
					p.Velocity.Y += 30.0f * deltaTime; // Fast fall with acceleration
					break;

				case ParticleType.Sparks:
					p.Velocity.Y += 15.0f * deltaTime; // Gravity
					// Fade color
					byte newG = (byte)Math.Max(0, p.Color.G - 5);
					p.Color = new Color(p.Color.R, newG, 0);
					break;

				case ParticleType.Confetti:
					p.Velocity.Y += 8.0f * deltaTime; // Medium gravity
					p.Velocity.X *= 0.98f; // Air resistance
					// Rotate character
					if (_random.Next(0, 10) == 0)
					{
						p.Character = "+×○●◦•"[_random.Next(6)];
					}
					break;
			}

			// Remove if dead or out of bounds
			if (p.Lifetime <= 0 || p.Position.Y > _spawnHeight + 5)
			{
				_particles.RemoveAt(i);
			}
			else
			{
				_particles[i] = p;
			}
		}

		// Spawn new particles
		_spawnAccumulator += _spawnRate * deltaTime;
		while (_spawnAccumulator >= 1.0f && _particles.Count < _maxParticles)
		{
			SpawnParticle();
			_spawnAccumulator -= 1.0f;
		}
	}

	private void SpawnParticle()
	{
		var type = _particleType;
		_particles.Add(new Particle
		{
			Position = new Vector2(_random.Next(0, _spawnWidth), -1),
			Velocity = type switch
			{
				ParticleType.Snow => new Vector2(0, 2f),
				ParticleType.Rain => new Vector2(0, 10f),
				ParticleType.Sparks => new Vector2(
					(float)(_random.NextDouble() - 0.5) * 8f,
					-15f
				),
				ParticleType.Confetti => new Vector2(
					(float)(_random.NextDouble() - 0.5) * 6f,
					0f
				),
				_ => Vector2.Zero
			},
			Lifetime = _random.Next(2, 6),
			MaxLifetime = 6f,
			Type = type,
			Character = type switch
			{
				ParticleType.Snow => '*',
				ParticleType.Rain => '|',
				ParticleType.Sparks => '.',
				ParticleType.Confetti => '○',
				_ => '.'
			},
			Color = type switch
			{
				ParticleType.Snow => Color.White,
				ParticleType.Rain => new Color(173, 216, 230),  // Light blue
				ParticleType.Sparks => new Color(255, 165, 0),  // Orange
				ParticleType.Confetti => new Color(
					(byte)_random.Next(100, 256),
					(byte)_random.Next(100, 256),
					(byte)_random.Next(100, 256)
				),
				_ => Color.White
			}
		});
	}

	public void Render(CharacterBuffer buffer)
	{
		foreach (var p in _particles)
		{
			int x = (int)p.Position.X;
			int y = (int)p.Position.Y;

			if (x >= 0 && x < buffer.Width && y >= 0 && y < buffer.Height)
			{
				var existing = buffer.GetCell(x, y);
				// Don't overwrite non-space characters (preserve UI)
				if (existing.Character == ' ')
				{
					buffer.SetCell(x, y, p.Character, p.Color, existing.Background);
				}
			}
		}
	}

	public int ParticleCount => _particles.Count;
}

/// <summary>
/// Demonstrates PostBufferPaint with physics-based particle system.
/// Shows snow, rain, sparks, and confetti effects as overlays.
/// </summary>
public class ParticleSystemWindow : Window
{
	private readonly ParticleSystem _particles;
	private System.Timers.Timer? _updateTimer;
	private DateTime _lastUpdate;
	private ParticleType _currentType = ParticleType.Snow;

	public ParticleSystemWindow(ConsoleWindowSystem windowSystem) : base(windowSystem)
	{
		Title = "Particle System (PostBufferPaint)";
		Width = 75;
		Height = 28;

		_particles = new ParticleSystem(maxParticles: 200, Width, Height);
		_particles.SetSpawnRate(8.0f);
		_particles.SetParticleType(_currentType);
		_lastUpdate = DateTime.Now;


		// Add info section
		AddControl(new MarkupControl(new List<string>
		{
			"[bold magenta]╔════════════════════════════════════════════════════╗[/]",
			"[bold magenta]║    PARTICLE SYSTEM - PostBufferPaint Demo         ║[/]",
			"[bold magenta]╚════════════════════════════════════════════════════╝[/]",
			"",
			"[white]Physics-based particle system with multiple effect types.[/]",
			"[white]Particles rendered as overlay, preserving UI elements.[/]"
		}));

		AddControl(new MarkupControl(new List<string> { "" }));

		// Type selection buttons
		var snowButton = new ButtonControl { Text = "* Snow Effect", Width = 60 };
		snowButton.Click += (s, e) => ChangeParticleType(ParticleType.Snow);
		AddControl(snowButton);

		AddControl(new MarkupControl(new List<string> { "" }));

		var rainButton = new ButtonControl { Text = "| Rain Effect", Width = 60 };
		rainButton.Click += (s, e) => ChangeParticleType(ParticleType.Rain);
		AddControl(rainButton);

		AddControl(new MarkupControl(new List<string> { "" }));

		var sparksButton = new ButtonControl { Text = ". Sparks Effect", Width = 60 };
		sparksButton.Click += (s, e) => ChangeParticleType(ParticleType.Sparks);
		AddControl(sparksButton);

		AddControl(new MarkupControl(new List<string> { "" }));

		var confettiButton = new ButtonControl { Text = "+ Confetti Effect", Width = 60 };
		confettiButton.Click += (s, e) => ChangeParticleType(ParticleType.Confetti);
		AddControl(confettiButton);

		AddControl(new MarkupControl(new List<string>
		{
			"",
			"[dim]• Each type has unique physics behavior[/]",
			"[dim]• Particles respect UI elements (no overwrite)[/]",
			"[dim]• Efficient pooling and lifetime management[/]",
			"",
			"[yellow]Press Esc to close this window[/]"
		}));

		// Hook into PostBufferPaint AFTER all controls are added
		PostBufferPaint += RenderParticles;

		// Start animation (exactly like FadeInWindow)
		_updateTimer = new System.Timers.Timer(16); // ~60 FPS
		_updateTimer.AutoReset = true;
		_updateTimer.Elapsed += (sender, e) =>
		{
			var now = DateTime.Now;
			float deltaTime = (float)(now - _lastUpdate).TotalSeconds;
			_lastUpdate = now;

			_particles.Update(deltaTime);
			this.Invalidate(redrawAll: true);
		};
		_updateTimer.Start();

		// Cleanup on window close
		OnClosing += (sender, e) =>
		{
			_updateTimer?.Stop();
			_updateTimer?.Dispose();

			PostBufferPaint -= RenderParticles;
		};
	}

	private void ChangeParticleType(ParticleType type)
	{
		_currentType = type;
		_particles.SetParticleType(type);
	}

	private void RenderParticles(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
	{
		_particles.Render(buffer);
	}

}
