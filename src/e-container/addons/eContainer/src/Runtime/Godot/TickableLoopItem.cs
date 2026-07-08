using System;
using System.Collections.Generic;

namespace Enaweg.Container.Godot;

public sealed class FixedTickableLoopItem : IFrameRunnerWorkItem, IDisposable
{
	readonly IReadOnlyList<IPhysicsTickable> entries;
	readonly EntryPointExceptionHandler exceptionHandler;
	bool disposed;

	public FixedTickableLoopItem(
		IReadOnlyList<IPhysicsTickable> entries,
		EntryPointExceptionHandler exceptionHandler)
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
				if (exceptionHandler == null) throw;
				exceptionHandler.Publish(ex);
			}
		}

		return !disposed;
	}

	public void Dispose() => disposed = true;
}

public sealed class TickableLoopItem(IReadOnlyList<ITickable> entries, EntryPointExceptionHandler exceptionHandler) : IFrameRunnerWorkItem, IDisposable
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
				if (exceptionHandler == null) throw;
				exceptionHandler.Publish(ex);
			}
		}

		return !disposed;
	}

	public void Dispose() => disposed = true;
}
