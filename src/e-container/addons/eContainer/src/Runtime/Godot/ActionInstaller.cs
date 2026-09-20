using System;
using Enaweg.Container.Internal;
using VContainer;

namespace Enaweg.Container.Godot;

/// <summary>Adapts a registration callback to an <see cref="IInstaller"/>.</summary>
/// <remarks>Use this when a scope is created with an inline <see cref="Action{T}"/> configuration callback.</remarks>
public class ActionInstaller : IInstaller
{
	/// <summary>Creates an installer that invokes <paramref name="installation"/> during scope construction.</summary>
	public static implicit operator ActionInstaller(Action<IContainerBuilder> installation) => new ActionInstaller(installation);

	readonly Action<IContainerBuilder> configuration;

	/// <summary>Creates an installer that invokes <paramref name="configuration"/> during scope construction.</summary>
	/// <param name="configuration">The registrations to add to the scope's builder.</param>
	/// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
	public ActionInstaller(Action<IContainerBuilder> configuration)
	{
		// Rejected here rather than at Install(), which runs deep inside a container build.
		ThrowHelper.ThrowArgumentNullIfNull(configuration);
		this.configuration = configuration;
	}

	/// <inheritdoc />
	public void Install(IContainerBuilder builder)
	{
		configuration(builder);
	}
}
