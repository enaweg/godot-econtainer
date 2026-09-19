using System;
using System.Runtime.CompilerServices;
using Enaweg.Container.Internal;
using Godot;

#nullable enable

namespace Enaweg.Container.Godot;

internal enum PlayerLoopTiming
{
	Process,
	PhysicsProcess
}

public abstract class FrameProvider
{
	public abstract long GetFrameCount();
	public abstract void Register(IFrameRunnerWorkItem callback);
}

public interface IFrameRunnerWorkItem
{
	// true, continue
	bool MoveNext(long frameCount);
}

public class GodotFrameProvider : FrameProvider
{
	public static readonly GodotFrameProvider Process = new GodotFrameProvider(PlayerLoopTiming.Process);
	public static readonly GodotFrameProvider PhysicsProcess = new GodotFrameProvider(PlayerLoopTiming.PhysicsProcess);
	/// <summary>
	/// Process-wide fallback for work items that let an exception escape MoveNext(). Entry
	/// point tickables do not rely on it - they carry their own scope's handler - so this is
	/// only for work items registered directly against a frame provider. Assign it once at
	/// startup if you want them reported somewhere.
	/// </summary>
	public static EntryPointExceptionHandler? ExceptionHandler;
		
	FreeListCore<IFrameRunnerWorkItem> list;
	readonly object gate = new object();

	PlayerLoopTiming PlayerLoopTiming { get; }

	internal StrongBox<double> Delta = default!; // set from Node before running process.

	internal GodotFrameProvider(PlayerLoopTiming playerLoopTiming)
	{
		this.PlayerLoopTiming = playerLoopTiming;
		this.list = new FreeListCore<IFrameRunnerWorkItem>(gate);
	}

	public override long GetFrameCount()
	{
		if (PlayerLoopTiming == PlayerLoopTiming.Process)
		{
			return (long)Engine.GetProcessFrames();
		}
		else
		{
			return (long)Engine.GetPhysicsFrames();
		}
	}

	public override void Register(IFrameRunnerWorkItem callback)
	{
		list.Add(callback, out _);
	}

	internal void Run(double _)
	{
		long frameCount = GetFrameCount();

		ReadOnlySpan<IFrameRunnerWorkItem?> span = list.AsSpan();
		for (var i = 0; i < span.Length; i++)
		{
			ref readonly IFrameRunnerWorkItem? item = ref span[i];
			if (item != null)
			{
				try
				{
					if (!item.MoveNext(frameCount))
					{
						list.Remove(i);
					}
				}
				catch (Exception ex)
				{
					list.Remove(i);
					try
					{
						ExceptionHandler?.Publish(ex);
					}
					catch { }
				}
			}
		}
	}
}
