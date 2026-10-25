// -----------------------------------------------------------------------
// SnakeGame — game-feel effects.
//
// Particles (pixel sparks with drag + gravity, coloured along a
// ColorGradient over their lifetime), floating score pop-ups, screen shake,
// a full-window colour flash (applied by a PostBufferPaint compositor hook
// in Program) and hit-stop slow motion. Everything is simulated in the
// game's pixel space and only read by the renderer.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Helpers;

namespace SnakeGame;

internal struct Particle
{
	public float X, Y, Vx, Vy;
	public float Life, MaxLife;
	public float Drag, Gravity;
	public ColorGradient Palette;
	public bool Glow;
}

internal sealed class Popup
{
	public required string Text { get; init; }
	public required Color Color { get; init; }
	public float X { get; set; }
	public float Y { get; set; }
	public float Life { get; set; }
	public float MaxLife { get; init; } = 1.1f;
}

internal sealed class Effects
{
	private const int MaxParticles = 1200;

	/// <summary>Fruit juice: warm and sweet.</summary>
	public static readonly ColorGradient Juice = ColorGradient.FromColors(
		new Color(255, 250, 220), new Color(255, 190, 70), new Color(240, 90, 60), new Color(90, 20, 30));

	/// <summary>The snake's own colours, shed when it dies.</summary>
	public static readonly ColorGradient Scale = ColorGradient.FromColors(
		new Color(230, 255, 230), new Color(90, 230, 130), new Color(30, 140, 90), new Color(10, 40, 30));

	/// <summary>Cold and electric, for the bonus fruit.</summary>
	public static readonly ColorGradient Spark = ColorGradient.FromColors(
		new Color(240, 255, 255), new Color(120, 230, 255), new Color(60, 110, 230), new Color(20, 20, 70));

	/// <summary>Cyclic rainbow (first and last stops match) for colour cycling.</summary>
	public static readonly ColorGradient Rainbow = ColorGradient.FromColors(
		new Color(255, 60, 60), new Color(255, 170, 40), new Color(255, 240, 70), new Color(70, 255, 120),
		new Color(60, 220, 255), new Color(110, 90, 255), new Color(255, 70, 220), new Color(255, 60, 60));

	private readonly Random _rng;
	private readonly Particle[] _particles = new Particle[MaxParticles];
	private int _count;

	public Effects(Random rng) => _rng = rng;

	public ReadOnlySpan<Particle> Particles => _particles.AsSpan(0, _count);
	public List<Popup> Popups { get; } = new();

	/// <summary>Screen-shake amplitude in pixels; decays over time.</summary>
	public float Shake { get; private set; }

	/// <summary>Full-window flash colour and intensity (0..1).</summary>
	public Color FlashColor { get; private set; } = Color.White;
	public float FlashIntensity { get; private set; }

	/// <summary>World time scale: dips below 1 for a moment of hit-stop.</summary>
	public float TimeScale => _slowMoTimer > 0 ? _slowMoScale : 1f;

	private float _slowMoTimer;
	private float _slowMoScale = 1f;

	public void AddShake(float amount) => Shake = Math.Min(6f, Math.Max(Shake, amount));

	public void Flash(Color color, float intensity)
	{
		if (intensity < FlashIntensity) return;
		FlashColor = color;
		FlashIntensity = Math.Min(1f, intensity);
	}

	public void SlowMotion(float seconds, float scale)
	{
		_slowMoTimer = seconds;
		_slowMoScale = scale;
	}

	/// <summary>Radial burst of particles.</summary>
	public void Explode(float x, float y, int count, ColorGradient palette, float speed,
		float life = 0.8f, float gravity = 25f, bool glow = true)
	{
		for (int i = 0; i < count; i++)
		{
			double angle = _rng.NextDouble() * Math.PI * 2;
			float v = speed * (0.25f + (float)_rng.NextDouble() * 0.75f);
			Spawn(new Particle
			{
				X = x,
				Y = y,
				Vx = (float)Math.Cos(angle) * v,
				Vy = (float)Math.Sin(angle) * v * 0.8f,
				MaxLife = life * (0.5f + (float)_rng.NextDouble() * 0.5f),
				Drag = 2.2f,
				Gravity = gravity,
				Palette = palette,
				Glow = glow,
			});
		}
	}

	/// <summary>A directional spray, e.g. the trail shed behind a turning head.</summary>
	public void Spray(float x, float y, int count, ColorGradient palette, float dirX, float dirY,
		float speed, float spread)
	{
		for (int i = 0; i < count; i++)
		{
			float jitterX = (float)(_rng.NextDouble() - 0.5) * spread;
			float jitterY = (float)(_rng.NextDouble() - 0.5) * spread;
			float v = speed * (0.4f + (float)_rng.NextDouble() * 0.8f);
			Spawn(new Particle
			{
				X = x,
				Y = y,
				Vx = dirX * v + jitterX,
				Vy = dirY * v * 0.8f + jitterY,
				MaxLife = 0.22f + (float)_rng.NextDouble() * 0.3f,
				Drag = 3.4f,
				Gravity = 0,
				Palette = palette,
				Glow = true,
			});
		}
	}

	public void AddPopup(string text, float x, float y, Color color, float life = 1.1f) =>
		Popups.Add(new Popup { Text = text, X = x, Y = y, Color = color, MaxLife = life });

	private void Spawn(in Particle p)
	{
		if (_count >= MaxParticles) return;
		_particles[_count++] = p;
	}

	/// <summary>Advances particle physics (world time) and decays camera effects (real time).</summary>
	public void Update(float worldDt, float realDt)
	{
		for (int i = 0; i < _count;)
		{
			ref var p = ref _particles[i];
			p.Life += worldDt;
			if (p.Life >= p.MaxLife)
			{
				_particles[i] = _particles[--_count];
				continue;
			}
			float drag = MathF.Max(0, 1 - p.Drag * worldDt);
			p.Vx *= drag;
			p.Vy = p.Vy * drag + p.Gravity * worldDt;
			p.X += p.Vx * worldDt;
			p.Y += p.Vy * worldDt;
			i++;
		}

		for (int i = Popups.Count - 1; i >= 0; i--)
		{
			var pop = Popups[i];
			pop.Life += realDt;
			pop.Y -= realDt * 7f;
			if (pop.Life >= pop.MaxLife) Popups.RemoveAt(i);
		}

		Shake = MathF.Max(0, Shake - realDt * 9f);
		// Decays briskly: a flash is a punctuation mark, not a state.
		FlashIntensity = MathF.Max(0, FlashIntensity - realDt * 3.2f);
		if (_slowMoTimer > 0) _slowMoTimer -= realDt;
	}

	public void Clear()
	{
		_count = 0;
		Popups.Clear();
		Shake = 0;
		FlashIntensity = 0;
		_slowMoTimer = 0;
	}
}
