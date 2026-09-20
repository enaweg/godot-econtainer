using System;
using Enaweg.Container.Internal;
using VContainer;

namespace Enaweg.Container.Godot;

public static class ContainerBuilderNodeExtensions
{
	public static RegistrationBuilder RegisterEntryPoint<T>(
		this IContainerBuilder builder,
		Lifetime lifetime = Lifetime.Singleton)
	{
		EntryPointsBuilder.EnsureDispatcherRegistered(builder);
		return builder.Register<T>(lifetime).AsImplementedInterfaces();
	}

	public static RegistrationBuilder RegisterEntryPoint<TInterface>(this IContainerBuilder builder,
		Func<IObjectResolver, TInterface> implementationConfiguration,
		Lifetime lifetime)
	{
		EntryPointsBuilder.EnsureDispatcherRegistered(builder);
		return builder.Register(new FuncRegistrationBuilder(container => implementationConfiguration(container),
			typeof(TInterface), lifetime)).AsImplementedInterfaces();
	}

	public static void RegisterEntryPointExceptionHandler(this IContainerBuilder builder, Action<Exception> exceptionHandler)
	{
		builder.RegisterInstance(new EntryPointExceptionHandler(exceptionHandler));
	}

	/// <summary>
	/// Registers several entry points sharing one lifetime:
	/// <c>builder.UseEntryPoints(e =&gt; { e.Add&lt;Foo&gt;(); e.OnException(Log); })</c>.
	/// </summary>
	public static void UseEntryPoints(
		this IContainerBuilder builder,
		Action<EntryPointsBuilder> configuration,
		Lifetime lifetime = Lifetime.Singleton)
		=> EntryPointsBuilder.UseEntryPoints(builder, configuration, lifetime);


	public static RegistrationBuilder RegisterNode<TInterface>(this IContainerBuilder builder, TInterface node)
	{
		var registrationBuilder = new NodeRegistrationBuilder(node).As(typeof(TInterface));
		// Force inject execution
		builder.RegisterBuildCallback(container => container.Resolve<TInterface>());
		return builder.Register(registrationBuilder);
	}
}
