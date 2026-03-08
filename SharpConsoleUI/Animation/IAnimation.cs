namespace SharpConsoleUI.Animation;

/// <summary>
/// Represents an active animation that can be updated and cancelled.
/// </summary>
public interface IAnimation
{
	/// <summary>Whether the animation has finished or been cancelled.</summary>
	bool IsComplete { get; }

	/// <summary>Advances the animation by the given delta time.</summary>
	void Update(TimeSpan deltaTime);

	/// <summary>Cancels the animation, marking it as complete.</summary>
	void Cancel();
}
