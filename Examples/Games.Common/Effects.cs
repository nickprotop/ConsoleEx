// -----------------------------------------------------------------------
// The example games' game-feel effects.
//
// Particles (pixel sparks and debris with drag and gravity, coloured along
// a ColorGradient over their lifetime), floating score pop-ups, screen
// shake, a full-window colour flash (applied by a PostBufferPaint
// compositor hook) and hit-stop slow motion.
//
// All simulation: a spawn records a position, a velocity and a lifetime,
// and the renderer later reads Particles and draws whatever is there.
// Nothing here touches a surface, which is why it is not a PixelSurface
// concern - a drawing surface that owned a particle pool would need a
// lifetime and a clock, and would stop being a drawing surface.
//
// What is shared is the mechanism. What each game keeps for itself is its
// palettes, which are art direction, and any spawn shape only it needs.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;

namespace Games.Common;

/// <summary>One particle: pixel-space position, velocity and remaining life.</summary>
public struct Particle
{
	public float X, Y, Vx, Vy;
	public float Life, MaxLife;
	public float Drag, Gravity;

	/// <summary>
	/// Sampled at paint time rather than at spawn, so a particle shades along
	/// the gradient as it ages instead of being one flat colour.
	/// </summary>
	public ColorGradient Palette;

	public bool Glow;
}

/// <summary>A short-lived piece of floating text, such as a score.</summary>
public sealed class Popup
{
	public required string Text { get; init; }
	public required Color Color { get; init; }
	public float X { get; set; }
	public float Y { get; set; }
	public float Life { get; set; }
	public float MaxLife { get; init; } = 1.1f;
}

/// <summary>
/// A particle pool and the camera effects that go with it. Fixed capacity,
/// no allocation per spawn: a burst that would overflow is simply dropped,
/// which is the right failure for something decorative.
/// </summary>
public sealed class Effects
{
	private readonly Random _rng;
	private readonly Particle[] _particles;
	private int _count;

	private float _slowMoTimer;
	private float _slowMoScale = 1f;

	/// <param name="rng">The game's random source, so a seeded game stays reproducible.</param>
	/// <param name="maxParticles">
	/// Pool capacity. Each game sizes this to its own busiest moment; past it
	/// spawns are dropped rather than growing the array mid-frame.
	/// </param>
	public Effects(Random rng, int maxParticles = 1500)
	{
		if (maxParticles <= 0) throw new ArgumentOutOfRangeException(nameof(maxParticles));

		_rng = rng ?? throw new ArgumentNullException(nameof(rng));
		_particles = new Particle[maxParticles];
	}

	/// <summary>The live particles, for the renderer to read.</summary>
	public ReadOnlySpan<Particle> Particles => _particles.AsSpan(0, _count);

	/// <summary>The live pop-ups, for the renderer to read.</summary>
	public List<Popup> Popups { get; } = new();

	/// <summary>Screen-shake amplitude in pixels; decays over time.</summary>
	public float Shake { get; private set; }

	/// <summary>Full-window flash colour.</summary>
	public Color FlashColor { get; private set; } = Color.White;

	/// <summary>Full-window flash intensity, 0 to 1.</summary>
	public float FlashIntensity { get; private set; }

	/// <summary>World time scale: dips below 1 for a moment of hit-stop.</summary>
	public float TimeScale => _slowMoTimer > 0 ? _slowMoScale : 1f;

	/// <summary>Shakes the camera, keeping the strongest request rather than summing.</summary>
	public void AddShake(float amount) => Shake = Math.Min(6f, Math.Max(Shake, amount));

	/// <summary>Flashes the window, unless a stronger flash is already running.</summary>
	public void Flash(Color color, float intensity)
	{
		if (intensity < FlashIntensity) return;
		FlashColor = color;
		FlashIntensity = Math.Min(1f, intensity);
	}

	/// <summary>Slows world time for a moment of hit-stop.</summary>
	public void SlowMotion(float seconds, float scale)
	{
		_slowMoTimer = seconds;
		_slowMoScale = scale;
	}

	/// <summary>Radial burst of particles.</summary>
	public void Explode(
		float x, float y, int count, ColorGradient palette, float speed,
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

	/// <summary>Particles shed from a sprite's own pixels, so the shape visibly shatters.</summary>
	public void Shatter(PixelMask sprite, int x, int y, ColorGradient palette, float speed)
	{
		float cx = x + sprite.Width / 2f, cy = y + sprite.Height / 2f;
		for (int sy = 0; sy < sprite.Height; sy++)
		{
			for (int sx = 0; sx < sprite.Width; sx++)
			{
				if (!sprite[sx, sy]) continue;

				float dx = x + sx - cx, dy = y + sy - cy;
				float len = MathF.Max(0.5f, MathF.Sqrt(dx * dx + dy * dy));
				float v = speed * (0.6f + (float)_rng.NextDouble() * 0.8f);
				Spawn(new Particle
				{
					X = x + sx,
					Y = y + sy,
					Vx = dx / len * v + (float)(_rng.NextDouble() - 0.5) * 8,
					Vy = dy / len * v * 0.8f - 6,
					MaxLife = 0.45f + (float)_rng.NextDouble() * 0.5f,
					Drag = 3f,
					Gravity = 40f,
					Palette = palette,
				});
			}
		}
	}

	/// <summary>Particles shed from a rectangle, for shapes with no sprite mask.</summary>
	public void Shatter(
		float x, float y, float width, float height, ColorGradient palette, float speed)
	{
		int across = Math.Max(2, (int)(width / 3));
		float cx = x + width / 2f;

		for (int i = 0; i < across; i++)
		{
			float px = x + (i + 0.5f) * (width / across);
			float py = y + height * 0.5f;
			Spawn(new Particle
			{
				X = px,
				Y = py,
				Vx = (px - cx) * 1.6f + (float)(_rng.NextDouble() - 0.5) * speed,
				Vy = -(float)_rng.NextDouble() * speed * 0.5f,
				MaxLife = 0.5f + (float)_rng.NextDouble() * 0.4f,
				Drag = 1.2f,
				Gravity = 26f,
				Palette = palette,
				Glow = true,
			});
		}
	}

	/// <summary>A directional spray, e.g. debris thrown away from an impact.</summary>
	public void Spray(
		float x, float y, int count, ColorGradient palette,
		float dirX, float dirY, float speed, float spread)
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

	/// <summary>A short directional jet, e.g. a muzzle flash or ground splash.</summary>
	public void Jet(float x, float y, int count, ColorGradient palette, float vy, float spread)
	{
		for (int i = 0; i < count; i++)
		{
			Spawn(new Particle
			{
				X = x,
				Y = y,
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

	/// <summary>
	/// Takes a slot if one is free. A full pool drops the particle rather than
	/// growing: the cost of a dropped spark is invisible, the cost of an
	/// allocation mid-frame is not.
	/// </summary>
	private void Spawn(in Particle p)
	{
		if (_count >= _particles.Length) return;
		_particles[_count++] = p;
	}

	/// <summary>Adds a piece of floating text.</summary>
	public void AddPopup(string text, float x, float y, Color color, float life = 1.1f) =>
		Popups.Add(new Popup { Text = text, X = x, Y = y, Color = color, MaxLife = life });

	/// <summary>
	/// Advances particle physics on world time and decays the camera effects on
	/// real time.
	/// </summary>
	/// <param name="worldDt">
	/// Step length scaled by <see cref="TimeScale"/>: particles slow down with
	/// the world during hit-stop.
	/// </param>
	/// <param name="realDt">
	/// Unscaled step length. Shake, flash and the hit-stop timer itself run on
	/// this, or slow motion would never end.
	/// </param>
	public void Update(float worldDt, float realDt)
	{
		for (int i = 0; i < _count;)
		{
			ref var p = ref _particles[i];
			p.Life += worldDt;

			if (p.Life >= p.MaxLife)
			{
				// Swap-remove: order does not matter to a particle cloud, and
				// this keeps the live range contiguous for the renderer's span.
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

	/// <summary>Drops everything: particles, pop-ups and camera effects.</summary>
	public void Clear()
	{
		_count = 0;
		Popups.Clear();
		Shake = 0;
		FlashIntensity = 0;
		_slowMoTimer = 0;
	}
}
