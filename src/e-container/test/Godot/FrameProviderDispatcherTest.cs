using System.Linq;
using Enaweg.Container.Godot;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

// FrameProviderDispatcher is what drives GodotFrameProvider.Run() every engine frame and what
// assigns the Delta boxes the frame providers read. It ships as part of the `eContainer`
// autoload scene; without it nothing registered by EntryPointDispatcher would ever tick.
[TestSuite]
[RequireGodotRuntime]
public class FrameProviderDispatcherTest
{
    static FrameProviderDispatcher FindAutoloadDispatcher()
    {
        var autoload = ((SceneTree)Engine.GetMainLoop()).Root.GetNodeOrNull("eContainer");
        AssertObject(autoload).IsNotNull();
        return autoload!.GetChildren().OfType<FrameProviderDispatcher>().FirstOrDefault()!;
    }

    [TestCase]
    public void Autoload_ContainsAFrameProviderDispatcher()
    {
        AssertObject(FindAutoloadDispatcher()).IsNotNull();
    }

    [TestCase]
    public void Autoload_ProvisionsDeltaOnBothFrameProviders()
    {
        AssertObject(GodotFrameProvider.Process.Delta).IsNotNull();
        AssertObject(GodotFrameProvider.PhysicsProcess.Delta).IsNotNull();
    }

    [TestCase]
    public void Process_PublishesDelta_AdvancesTime_AndRunsRegisteredWork()
    {
        var dispatcher = FindAutoloadDispatcher();
        var ticked = 0;
        var originalTime = GodotTimeProvider.Process.time;
        try
        {
            GodotFrameProvider.Process.Register(new CallbackWorkItem(() => ticked++, runOnce: true));

            dispatcher._Process(0.25);

            AssertFloat(GodotFrameProvider.Process.Delta.Value).IsEqualApprox(0.25, 0.0001);
            AssertFloat(GodotTimeProvider.Process.time - originalTime).IsEqualApprox(0.25, 0.0001);
            AssertInt(ticked).IsEqual(1);
        }
        finally
        {
            GodotTimeProvider.Process.time = originalTime;
        }
    }

    [TestCase]
    public void PhysicsProcess_PublishesDelta_AdvancesTime_AndRunsRegisteredWork()
    {
        var dispatcher = FindAutoloadDispatcher();
        var ticked = 0;
        var originalTime = GodotTimeProvider.PhysicsProcess.time;
        try
        {
            GodotFrameProvider.PhysicsProcess.Register(new CallbackWorkItem(() => ticked++, runOnce: true));

            dispatcher._PhysicsProcess(0.5);

            AssertFloat(GodotFrameProvider.PhysicsProcess.Delta.Value).IsEqualApprox(0.5, 0.0001);
            AssertFloat(GodotTimeProvider.PhysicsProcess.time - originalTime).IsEqualApprox(0.5, 0.0001);
            AssertInt(ticked).IsEqual(1);
        }
        finally
        {
            GodotTimeProvider.PhysicsProcess.time = originalTime;
        }
    }

    // Deregisters itself after the first tick so the shared provider is left clean.
    sealed class CallbackWorkItem(System.Action onTick, bool runOnce) : IFrameRunnerWorkItem
    {
        public bool MoveNext(long frameCount)
        {
            onTick();
            return !runOnce;
        }
    }
}
