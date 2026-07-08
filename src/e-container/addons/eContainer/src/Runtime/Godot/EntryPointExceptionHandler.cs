using System;

namespace Enaweg.Container.Godot;

public sealed class EntryPointExceptionHandler(Action<Exception> handler)
{
	public void Publish(Exception ex)
	{
		handler.Invoke(ex);
	}
}
