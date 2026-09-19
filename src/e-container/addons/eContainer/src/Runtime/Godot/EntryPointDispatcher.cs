using System;
using System.Collections.Generic;
using Enaweg.Container.Internal;
using Godot;
using VContainer;
using VContainer.Internal;

namespace Enaweg.Container.Godot;

[method: Inject]
public sealed class EntryPointDispatcher(IObjectResolver container) : IDisposable
{
	readonly CompositeDisposable disposable = new CompositeDisposable();

	public void Dispatch()
	{
		EntryPointExceptionHandler exceptionHandler = container.ResolveOrDefault<EntryPointExceptionHandler>();
		// This scope's handler is passed to the loop items it creates below. It deliberately
		// does not touch GodotFrameProvider.ExceptionHandler: that is a single process-wide
		// slot, so assigning it here made the most recently built scope's handler win for
		// every other scope as well.

		var initializables = container.Resolve<ContainerLocal<IReadOnlyList<IInitializable>>>().Value;
		for (var i = 0; i < initializables.Count; i++)
		{
			try
			{
				initializables[i].Initialize();
			}
			catch (Exception ex)
			{
				if (exceptionHandler != null)
					exceptionHandler.Publish(ex);
				else
					GD.PrintErr(ex);
			}
		}

		var postInitializables = container.Resolve<ContainerLocal<IReadOnlyList<IPostInitializable>>>().Value;
		for (var i = 0; i < postInitializables.Count; i++)
		{
			try
			{
				postInitializables[i].PostInitialize();
			}
			catch (Exception ex)
			{
				if (exceptionHandler != null)
					exceptionHandler.Publish(ex);
				else
					GD.PrintErr(ex);
			}
		}

		var tickableList = container.Resolve<ContainerLocal<IReadOnlyList<ITickable>>>().Value;
		if (tickableList.Count > 0)
		{
			var loopItem = new TickableLoopItem(tickableList, exceptionHandler);
			disposable.Add(loopItem);
			GodotFrameProvider.Process.Register(loopItem);
		}
		
		
		var physicsTickableList = container.Resolve<ContainerLocal<IReadOnlyList<IPhysicsTickable>>>().Value;
		if (physicsTickableList.Count > 0)
		{
			var loopItem = new FixedTickableLoopItem(physicsTickableList, exceptionHandler);
			disposable.Add(loopItem);
			GodotFrameProvider.PhysicsProcess.Register(loopItem);
		}
	}

	public void Dispose() => disposable.Dispose();
}
