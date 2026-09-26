using System;

namespace Enaweg.Logger;

/// <summary>
/// Tracks whether a log processor is currently writing to Godot's output on this thread.
/// <para>
/// <see cref="GodotOSLogger" /> is installed into the engine and turns every engine message back into a ZLogger
/// entry. Without this flag a processor's own <c>GD.Print</c> would come back through the OS logger and be logged
/// a second time. Every processor that writes to Godot enters the guard around those writes, and the OS logger
/// ignores messages while it is held.
/// </para>
/// <para>
/// This does not exist to prevent unbounded recursion: Godot 4.7.2 silently discards log output emitted from
/// inside a logger callback, so a missing guard degrades into a duplicate entry at worst, never a stack overflow.
/// The guard makes that independent of engine internals we do not control.
/// </para>
/// </summary>
internal static class GodotLogGuard
{
    [ThreadStatic] static bool isWriting;

    internal static bool IsWriting => isWriting;

    internal static Scope Enter() => new(isWriting);

    internal readonly ref struct Scope
    {
        readonly bool previous;

        internal Scope(bool previous)
        {
            this.previous = previous;
            isWriting = true;
        }

        public void Dispose()
        {
            isWriting = previous;
        }
    }
}
