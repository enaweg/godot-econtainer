namespace Enaweg.Container.Godot
{
    /// <summary>Receives one callback for each Godot physics frame while its scope is alive.</summary>
    /// <remarks>Implementations must be registered as entry points to receive this callback.</remarks>
    public interface IPhysicsTickable
    {
        /// <summary>Runs from Godot's <c>_PhysicsProcess</c> callback.</summary>
        /// <param name="frameCount">Godot's current physics-frame count.</param>
        void PhysicsTick(long frameCount);
    }
}
