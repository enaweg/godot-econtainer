using System;
using System.Collections.Generic;
using Godot;

namespace Enaweg.Container.Godot;

public sealed class FixedTickableLoopItem : IFrameRunnerWorkItem, IDisposable
{
	readonly IReadOnlyList<IPhysicsTickable> entries;
	readonly EntryPointExceptionHandler? exceptionHandler;
	bool disposed;

	public FixedTickableLoopItem(
		IReadOnlyList<IPhysicsTickable> entries,
		EntryPointExceptionHandler? exceptionHandler)
	{
		this.entries = entries;
		this.exceptionHandler = exceptionHandler;
	}

	public bool MoveNext(long frameCount)
	{
		if (disposed) return false;
		for (var i = 0; i < entries.Count; i++)
		{
			try
			{
				entries[i].PhysicsTick(frameCount);
			}
			catch (Exception ex)
			{
				// Never rethrow: GodotFrameProvider.Run() deregisters a work item that throws,
				// which would silently stop every other entry in this scope too.
				if (exceptionHandler != null)
					exceptionHandler.Publish(ex);
				else
					GD.PrintErr(ex);
			}
		}

		return !disposed;
	}

	public void Dispose() => disposed = true;
}

public sealed class TickableLoopItem(IReadOnlyList<ITickable> entries, EntryPointExceptionHandler? exceptionHandler) : IFrameRunnerWorkItem, IDisposable
{
	bool disposed;

	public bool MoveNext(long frameCount)
	{
		if (disposed) return false;
		for (var i = 0; i < entries.Count; i++)
		{
			try
			{
				entries[i].Tick(frameCount);
			}
			catch (Exception ex)
			{
				// Never rethrow: GodotFrameProvider.Run() deregisters a work item that throws,
				// which would silently stop every other entry in this scope too.
				if (exceptionHandler != null)
					exceptionHandler.Publish(ex);
				else
					GD.PrintErr(ex);
			}
		}

		return !disposed;
	}

	public void Dispose() => disposed = true;
}
