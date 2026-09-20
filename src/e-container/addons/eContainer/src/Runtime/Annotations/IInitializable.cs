namespace Enaweg.Container.Godot;

/// <summary>Runs once when an entry-point registration is dispatched for its scope.</summary>
/// <remarks>
/// Implementations must be registered with <c>RegisterEntryPoint</c> or <c>UseEntryPoints</c>;
/// an ordinary container registration does not invoke this callback.
/// </remarks>
public interface IInitializable
{
	/// <summary>Initializes the resolved service before post-initialization and frame callbacks begin.</summary>
	void Initialize();
}
