using System;
using Enaweg.Container.Internal;

namespace Enaweg.Container.Godot;

/// <summary>Reports exceptions thrown by entry-point callbacks for one container scope.</summary>
public sealed class EntryPointExceptionHandler
{
	readonly Action<Exception> handler;

	/// <summary>Creates a handler that forwards exceptions to <paramref name="handler"/>.</summary>
	public EntryPointExceptionHandler(Action<Exception> handler)
	{
		// A null handler would surface as a NullReferenceException raised while reporting
		// another exception - the worst possible place to lose the original.
		ThrowHelper.ThrowArgumentNullIfNull(handler);
		this.handler = handler;
	}

	/// <summary>Reports <paramref name="ex"/> to the configured handler.</summary>
	public void Publish(Exception ex)
	{
		handler.Invoke(ex);
	}
}
