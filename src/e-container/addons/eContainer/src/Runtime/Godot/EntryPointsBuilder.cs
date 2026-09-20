using System;
using Enaweg.Container.Internal;
using VContainer;

namespace Enaweg.Container.Godot;

public readonly struct EntryPointsBuilder(IContainerBuilder containerBuilder, Lifetime lifetime)
{
	public static void EnsureDispatcherRegistered(IContainerBuilder containerBuilder)
	{
		if (containerBuilder.Exists(typeof(EntryPointDispatcher), false)) return;
		containerBuilder.Register<EntryPointDispatcher>(Lifetime.Scoped);
		containerBuilder.RegisterBuildCallback(container => { container.Resolve<EntryPointDispatcher>().Dispatch(); });
	}

	/// <summary>
	/// Registers a group of entry points sharing one lifetime, the port of VContainer's
	/// <c>UseEntryPoints</c>. Without it nothing constructed this struct, so <see cref="Add{T}"/>
	/// and <see cref="OnException"/> were public but unreachable.
	/// </summary>
	public static void UseEntryPoints(
		IContainerBuilder containerBuilder,
		Action<EntryPointsBuilder> configuration,
		Lifetime lifetime = Lifetime.Singleton)
	{
		ThrowHelper.ThrowArgumentNullIfNull(configuration);
		EnsureDispatcherRegistered(containerBuilder);
		configuration(new EntryPointsBuilder(containerBuilder, lifetime));
	}

	public RegistrationBuilder Add<T>() => containerBuilder.Register<T>(lifetime).AsImplementedInterfaces();

	public void OnException(Action<Exception> exceptionHandler) => containerBuilder.RegisterEntryPointExceptionHandler(exceptionHandler);
}
