using System;
using Enaweg.Container.Internal;
using VContainer;

namespace Enaweg.Container.Godot;

/// <summary>Godot-specific registration helpers for VContainer builders.</summary>
public static class ContainerBuilderNodeExtensions
{
	/// <summary>Registers <typeparamref name="T"/> as an entry point for the specified lifetime.</summary>
	/// <remarks>The service's implemented lifecycle interfaces are dispatched when the scope is built.</remarks>
	public static RegistrationBuilder RegisterEntryPoint<T>(
		this IContainerBuilder builder,
		Lifetime lifetime = Lifetime.Singleton)
	{
		EntryPointsBuilder.EnsureDispatcherRegistered(builder);
		return builder.Register<T>(lifetime).AsImplementedInterfaces();
	}

	/// <summary>Registers a factory-produced entry point as <typeparamref name="TInterface"/>.</summary>
	/// <remarks>The returned implementation must implement the lifecycle interfaces it is expected to receive.</remarks>
	public static RegistrationBuilder RegisterEntryPoint<TInterface>(this IContainerBuilder builder,
		Func<IObjectResolver, TInterface> implementationConfiguration,
		Lifetime lifetime)
	{
		EntryPointsBuilder.EnsureDispatcherRegistered(builder);
		return builder.Register(new FuncRegistrationBuilder(container => implementationConfiguration(container)!,
			typeof(TInterface), lifetime)).AsImplementedInterfaces();
	}

	/// <summary>Registers the handler used for exceptions thrown by entry-point callbacks in this scope.</summary>
	/// <param name="builder">The container receiving the handler registration.</param>
	/// <param name="exceptionHandler">The scope-local exception sink.</param>
	public static void RegisterEntryPointExceptionHandler(this IContainerBuilder builder, Action<Exception> exceptionHandler)
	{
		builder.RegisterInstance(new EntryPointExceptionHandler(exceptionHandler));
	}

	/// <summary>
	/// Registers several entry points sharing one lifetime:
	/// <c>builder.UseEntryPoints(e =&gt; { e.Add&lt;Foo&gt;(); e.OnException(Log); })</c>.
	/// </summary>
	/// <param name="builder">The container receiving the entry-point registrations.</param>
	/// <param name="configuration">Adds entry points and optionally configures their exception handler.</param>
	/// <param name="lifetime">The lifetime applied to entry points added by <paramref name="configuration"/>.</param>
	public static void UseEntryPoints(
		this IContainerBuilder builder,
		Action<EntryPointsBuilder> configuration,
		Lifetime lifetime = Lifetime.Singleton)
		=> EntryPointsBuilder.UseEntryPoints(builder, configuration, lifetime);


	/// <summary>Registers an existing Godot node and injects it when the container is built.</summary>
	/// <typeparam name="TInterface">The service type under which <paramref name="node"/> is resolved.</typeparam>
	/// <param name="builder">The container receiving the node registration.</param>
	/// <param name="node">The existing node instance to inject and register.</param>
	/// <remarks>The node is resolved by a build callback so injection occurs even if no other service requests it.</remarks>
	public static RegistrationBuilder RegisterNode<TInterface>(this IContainerBuilder builder, TInterface node)
	{
		var registrationBuilder = new NodeRegistrationBuilder(node!).As(typeof(TInterface));
		// Force inject execution
		builder.RegisterBuildCallback(container => container.Resolve<TInterface>());
		return builder.Register(registrationBuilder);
	}
}
