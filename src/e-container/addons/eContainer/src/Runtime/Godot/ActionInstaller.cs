using System;
using Enaweg.Container.Internal;
using VContainer;

namespace Enaweg.Container.Godot;

public class ActionInstaller : IInstaller
{
	public static implicit operator ActionInstaller(Action<IContainerBuilder> installation) => new ActionInstaller(installation);

	readonly Action<IContainerBuilder> configuration;

	public ActionInstaller(Action<IContainerBuilder> configuration)
	{
		// Rejected here rather than at Install(), which runs deep inside a container build.
		ThrowHelper.ThrowArgumentNullIfNull(configuration);
		this.configuration = configuration;
	}

	public void Install(IContainerBuilder builder)
	{
		configuration(builder);
	}
}
