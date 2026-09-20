using System;
using Enaweg.Container.Internal;

namespace Enaweg.Container.Godot;

public sealed class EntryPointExceptionHandler
{
	readonly Action<Exception> handler;

	public EntryPointExceptionHandler(Action<Exception> handler)
	{
		// A null handler would surface as a NullReferenceException raised while reporting
		// another exception - the worst possible place to lose the original.
		ThrowHelper.ThrowArgumentNullIfNull(handler);
		this.handler = handler;
	}

	public void Publish(Exception ex)
	{
		handler.Invoke(ex);
	}
}
