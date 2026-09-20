namespace Enaweg.Container.Godot;

/// <summary>Runs once after every <see cref="IInitializable"/> in the same scope has initialized.</summary>
/// <remarks>Implementations must be registered as entry points to receive this callback.</remarks>
public interface IPostInitializable
{
	/// <summary>Performs work that requires the scope's initializable entry points to have run.</summary>
	void PostInitialize();
}
