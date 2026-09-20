namespace Enaweg.Container.Godot
{
    /// <summary>Receives one callback for each Godot process frame while its scope is alive.</summary>
    /// <remarks>Implementations must be registered as entry points to receive this callback.</remarks>
    public interface ITickable
    {
        /// <summary>Runs from Godot's <c>_Process</c> callback.</summary>
        /// <param name="frameCount">Godot's current process-frame count.</param>
        void Tick(long frameCount);
    }
}
