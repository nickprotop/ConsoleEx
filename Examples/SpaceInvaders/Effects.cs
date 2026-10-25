// -----------------------------------------------------------------------
// SpaceInvaders — game-feel effects.
//
// Particles (pixel sparks/debris with drag + gravity, coloured along a
// ColorGradient over their lifetime), floating score pop-ups, screen shake,
// a full-window colour flash (applied by a PostBufferPaint compositor hook
// in Program) and hit-stop slow motion. Everything is simulated in the
// game's pixel space and only read by the renderer.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Helpers;

using SharpConsoleUI.Drawing;

namespace SpaceInvaders;

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
	private const int MaxParticles = 1500;

	public static readonly ColorGradient Fire = ColorGradient.FromColors(
		new Color(255, 255, 230), new Color(255, 210, 90), new Color(255, 110, 30), new Color(150, 30, 20), new Color(40, 10, 10));
	public static readonly ColorGradient Plasma = ColorGradient.FromColors(
		new Color(240, 255, 255), new Color(90, 230, 255), new Color(40, 90, 220), new Color(20, 20, 70));
	/// <summary>Cyclic rainbow (first and last stops match) for colour cycling.</summary>
	public static readonly ColorGradient Rainbow = ColorGradient.FromColors(
		new Color(255, 60, 60), new Color(255, 170, 40), new Color(255, 240, 70), new Color(70, 255, 120),
		new Color(60, 220, 255), new Color(110, 90, 255), new Color(255, 70, 220), new Color(255, 60, 60));
	public static readonly ColorGradient Toxic = ColorGradient.FromColors(
		new Color(230, 255, 210), new Color(120, 255, 90), new Color(30, 140, 50), new Color(10, 40, 20));

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
	public void Explode(float x, float y, int count, ColorGradient palette, float speed, float life = 0.8f, float gravity = 25f, bool glow = true)
	{
		for (int i = 0; i < count; i++)
		{
			double angle = _rng.NextDouble() * Math.PI * 2;
			float v = speed * (0.25f + (float)_rng.NextDouble() * 0.75f);
			Spawn(new Particle
			{
				X = x, Y = y,
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

	/// <summary>Particles shed from a sprite's own pixels, so the shape visibly shatters.</summary>
	public void Shatter(PixelMask sprite, int x, int y, ColorGradient palette, float speed)
	{
		float cx = x + sprite.Width / 2f, cy = y + sprite.Height / 2f;
		for (int sy = 0; sy < sprite.Height; sy++)
			for (int sx = 0; sx < sprite.Width; sx++)
			{
				if (!sprite[sx, sy]) continue;
				float dx = x + sx - cx, dy = y + sy - cy;
				float len = MathF.Max(0.5f, MathF.Sqrt(dx * dx + dy * dy));
				float v = speed * (0.6f + (float)_rng.NextDouble() * 0.8f);
				Spawn(new Particle
				{
					X = x + sx, Y = y + sy,
					Vx = dx / len * v + (float)(_rng.NextDouble() - 0.5) * 8,
					Vy = dy / len * v * 0.8f - 6,
					MaxLife = 0.45f + (float)_rng.NextDouble() * 0.5f,
					Drag = 3f,
					Gravity = 40f,
					Palette = palette,
				});
			}
	}

	/// <summary>A short upward jet, e.g. muzzle flash or ground splash.</summary>
	public void Jet(float x, float y, int count, ColorGradient palette, float vy, float spread)
	{
		for (int i = 0; i < count; i++)
		{
			Spawn(new Particle
			{
				X = x, Y = y,
				Vx = (float)(_rng.NextDouble() - 0.5) * spread,
				Vy = vy * (0.5f + (float)_rng.NextDouble() * 0.5f),
				MaxLife = 0.18f + (float)_rng.NextDouble() * 0.2f,
				Drag = 4f,
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
		FlashIntensity = MathF.Max(0, FlashIntensity - realDt * 1.8f);
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
