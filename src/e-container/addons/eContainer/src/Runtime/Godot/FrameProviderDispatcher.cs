using System.Runtime.CompilerServices;
using Godot;

namespace Enaweg.Container.Godot;

/// <summary>Autoload node that advances eContainer's process and physics frame providers.</summary>
/// <remarks>Only one active dispatcher is supported; eContainer installs it automatically.</remarks>
public partial class FrameProviderDispatcher : global::Godot.Node
{
	// The frame providers and the time providers are process-wide statics, so a second
	// dispatcher would advance GodotTimeProvider.time twice per frame and call Run() twice,
	// ticking every registered entry point twice. Only the eContainer autoload should own one.
	static FrameProviderDispatcher? instance;

	StrongBox<double> processDelta = new StrongBox<double>();
	StrongBox<double> physicsProcessDelta = new StrongBox<double>();

	public override void _Ready()
	{
		if (instance != null && instance != this)
		{
			GD.PushWarning(
				$"A {nameof(FrameProviderDispatcher)} is already running; this one will stay idle. " +
				"It is installed by the eContainer autoload and should not be added manually.");
			SetProcess(false);
			SetPhysicsProcess(false);
			return;
		}

		instance = this;
		GodotFrameProvider.Process.Delta = processDelta;
		GodotFrameProvider.PhysicsProcess.Delta = physicsProcessDelta;
	}

	public override void _ExitTree()
	{
		if (instance == this)
		{
			instance = null;
		}
	}

	public override void _Process(double delta)
	{
		processDelta.Value = delta;
		GodotTimeProvider.Process.time += delta;
		GodotFrameProvider.Process.Run(delta);
	}

	public override void _PhysicsProcess(double delta)
	{
		physicsProcessDelta.Value = delta;
		GodotTimeProvider.PhysicsProcess.time += delta;
		GodotFrameProvider.PhysicsProcess.Run(delta);
	}
}
