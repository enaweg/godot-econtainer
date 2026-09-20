using System;
using Enaweg.Container.Internal;
using VContainer;

namespace Enaweg.Container.Godot;

/// <summary>Builds a group of entry-point registrations that share a VContainer lifetime.</summary>
/// <remarks>Instances are supplied to the callback passed to <see cref="UseEntryPoints"/>.</remarks>
public readonly struct EntryPointsBuilder(IContainerBuilder containerBuilder, Lifetime lifetime)
{
	/// <summary>Ensures that the current scope will dispatch its entry points after it is built.</summary>
	public static void EnsureDispatcherRegistered(IContainerBuilder containerBuilder)
	{
		if (containerBuilder.Exists(typeof(EntryPointDispatcher), false)) return;
		containerBuilder.Register<EntryPointDispatcher>(Lifetime.Scoped);
		containerBuilder.RegisterBuildCallback(container => { container.Resolve<EntryPointDispatcher>().Dispatch(); });
	}

	/// <summary>
	/// Registers a group of entry points sharing one lifetime.
	/// </summary>
	/// <param name="containerBuilder">The builder for the scope that owns the entry points.</param>
	/// <param name="configuration">Configures the group's registrations and exception handler.</param>
	/// <param name="lifetime">The lifetime used by <see cref="Add{T}"/>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
	public static void UseEntryPoints(
		IContainerBuilder containerBuilder,
		Action<EntryPointsBuilder> configuration,
		Lifetime lifetime = Lifetime.Singleton)
	{
		ThrowHelper.ThrowArgumentNullIfNull(configuration);
		EnsureDispatcherRegistered(containerBuilder);
		configuration(new EntryPointsBuilder(containerBuilder, lifetime));
	}

	/// <summary>Registers <typeparamref name="T"/> as an entry point in this group.</summary>
	public RegistrationBuilder Add<T>() => containerBuilder.Register<T>(lifetime).AsImplementedInterfaces();

	/// <summary>Sets the exception handler used by this group's scope-level entry-point dispatcher.</summary>
	public void OnException(Action<Exception> exceptionHandler) => containerBuilder.RegisterEntryPointExceptionHandler(exceptionHandler);
}
