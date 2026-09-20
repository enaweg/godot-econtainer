using System;
using System.Collections.Generic;
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

/// <summary>Schedules <see cref="IFrameRunnerWorkItem"/> instances against a Godot frame loop.</summary>
public abstract class FrameProvider
{
	/// <summary>Gets the current frame count for this provider's loop.</summary>
	public abstract long GetFrameCount();
	/// <summary>Adds a work item that will run until it returns <see langword="false"/>.</summary>
	public abstract void Register(IFrameRunnerWorkItem callback);
}

/// <summary>Represents work that can continue over successive frames.</summary>
public interface IFrameRunnerWorkItem
{
	/// <summary>Runs once for the current frame.</summary>
	/// <returns><see langword="true"/> to remain registered; otherwise <see langword="false"/>.</returns>
	bool MoveNext(long frameCount);
}

/// <summary>Provides process and physics frame scheduling backed by Godot's frame counters.</summary>
public class GodotFrameProvider : FrameProvider
{
	/// <summary>The provider advanced from Godot's <c>_Process</c> loop.</summary>
	public static readonly GodotFrameProvider Process = new GodotFrameProvider(PlayerLoopTiming.Process);
	/// <summary>The provider advanced from Godot's <c>_PhysicsProcess</c> loop.</summary>
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

	/// <inheritdoc />
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

	/// <inheritdoc />
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
						TryRemove(i);
					}
				}
				catch (Exception ex)
				{
					TryRemove(i);
					try
					{
						ExceptionHandler?.Publish(ex);
					}
					catch { }
				}
			}
		}
	}

	/// <summary>
	/// Removes the slot at <paramref name="index"/>, tolerating a slot that is already empty.
	/// </summary>
	/// <remarks>
	/// MoveNext() can remove this work item itself, directly or by disposing something that
	/// does. FreeListCore.Remove throws KeyNotFoundException on an empty slot, and from here
	/// that would escape into _Process and take down the whole frame loop.
	/// </remarks>
	void TryRemove(int index)
	{
		try
		{
			list.Remove(index);
		}
		catch (KeyNotFoundException)
		{
		}
	}
}
