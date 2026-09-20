using VContainer;

namespace Enaweg.Container.Godot;

/// <summary>Applies registrations to a <see cref="IContainerBuilder"/> while a <see cref="LifetimeScope"/> builds.</summary>
/// <remarks>
/// Installers are invoked synchronously during scope construction. They should only add registrations and must not
/// retain the builder for later use.
/// </remarks>
public interface IInstaller
{
	/// <summary>Adds this installer's registrations to <paramref name="builder"/>.</summary>
	void Install(IContainerBuilder builder);
}
