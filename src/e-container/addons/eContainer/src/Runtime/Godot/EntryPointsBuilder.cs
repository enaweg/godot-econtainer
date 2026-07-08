using System;
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

	public RegistrationBuilder Add<T>() => containerBuilder.Register<T>(lifetime).AsImplementedInterfaces();

	public void OnException(Action<Exception> exceptionHandler) => containerBuilder.RegisterEntryPointExceptionHandler(exceptionHandler);
}
