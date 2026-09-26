<div align="center">

# eContainer

**[VContainer](https://github.com/hadashiA/VContainer) - Unity's fast DI container - ported to [Godot](https://godotengine.org/).**

[![CI](https://github.com/enaweg/godot-econtainer/actions/workflows/ci-pr.yml/badge.svg)](https://github.com/enaweg/godot-econtainer/actions/workflows/ci-pr.yml)
![Godot 4.7.2](https://img.shields.io/badge/Godot-v4.7.2-202020?logo=godot-engine&logoColor=blue&color=darkgreen&labelColor=202020)
![.NET 8](https://img.shields.io/badge/.NET-8-202020?logo=dotnet&logoColor=purple&color=darkgreen&labelColor=202020)
![VContainer 1.19.0](https://img.shields.io/badge/VContainer-v1.19.0-202020?color=darkgreen&labelColor=202020)

**NOTE**: This project is experimental and still a work in progress.

**NOTE2**: This is based on https://github.com/nazgull30

</div>

## Requirements

The current CI-tested configuration uses:

+ [Godot 4.7.2 .NET](https://godotengine.org/download/archive/4.7.2-stable/)
+ [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

The project targets `net8.0`. Android builds target `net9.0`.

eContainer bundles **[VContainer](https://github.com/hadashiA/VContainer) 1.19.0** - the `VContainer.Standalone` and
`VContainer.SourceGenerator` packages at that version ship inside the release archive under
`addons/eContainer/.libs` and are installed into your project automatically. VContainer is MIT licensed by
[hadashiA](https://github.com/hadashiA).

## Installation

1. Download the latest [eContainer release](https://github.com/enaweg/godot-econtainer/releases) and
   [ePlugin release](https://github.com/enaweg/godot-epluginframework/releases).
2. Extract `addons/eContainer` from the eContainer archive and `addons/ePlugin` from the ePlugin archive into your
   Godot project's `addons` directory.
3. Open the project in the Godot .NET editor and enable **ePlugin** under **Project > Project Settings > Plugins**.
4. Enable **eContainer**.

eContainer depends on ePlugin. When it is enabled, ePlugin adds the bundled VContainer 1.19.0 packages to the Godot
project, registers their local package source in `nuget.config`, adds the `eContainer` autoload, and un-hides the
plugin's runtime source directory. No manual `dotnet add package` step is needed.

The release archive ships that source directory hidden as `addons/eContainer/.src`, so a freshly extracted, disabled
plugin does not get compiled into your project. Enabling the plugin renames it to `src`; disabling it hides the
directory again.

## Features

VContainer's `IObjectResolver`/`IContainerBuilder` API and its compile-time source generator, adapted to Godot's
`Node` lifecycle.

**Scopes**

+ `LifetimeScope` is a Godot `Node` that builds its container on `_EnterTree` and disposes it on `_ExitTree`. The
  exported `AutoRun` flag turns the automatic build off when you want to call `Build()` yourself.
+ A scope finds its parent automatically - from the parent type name set in the inspector, an explicitly assigned
  `ParentReference.Object`, a `FindParent()` override, or the override pushed by `LifetimeScope.EnqueueParent(...)`.
+ Child scopes can be created in code with `CreateChild<TScope>(...)` or instantiated from a `PackedScene` with
  `CreateChildFromPackedScene<TScope>(...)`; `LifetimeScope.Create(...)` adds an ad-hoc scope under the root.
+ The `eContainer` autoload owns the single `RootLifetimeScope` every other scope ultimately inherits from. It
  queues scopes whose declared parent has not entered the tree yet and flushes them as parents appear or the scene
  changes, and defers its own build to `_Ready` so autoloads can still contribute registrations through
  `LifetimeScope.Enqueue(...)`.

**Registration and injection**

+ Installers: implement `IInstaller`, or wrap a callback in `ActionInstaller`, to reuse registrations across scopes.
+ `RegisterNode<T>(node)` registers an existing node as a service and injects it when the scope builds.
+ `IObjectResolver.InjectNode(node)` injects a node and its descendants, stopping at any nested `LifetimeScope` -
  that scope injects its own subtree.
+ `IObjectResolver.Instantiate<T>(packedScene, parent)` instantiates, injects, and attaches a scene. Injection runs
  before the node enters the tree, so `_EnterTree` and `_Ready` already see the injected members.
+ The exported `AutoInjectNodes` array injects inspector-assigned nodes as soon as the scope builds.

**Entry points**

+ Lifecycle interfaces `IInitializable`, `IPostInitializable`, `ITickable`, and `IPhysicsTickable`, dispatched for
  registrations made with `RegisterEntryPoint(...)` or `UseEntryPoints(...)`.
+ Per-scope entry-point exception handling via `RegisterEntryPointExceptionHandler(...)` or `e.OnException(...)`;
  without a handler, exceptions are reported through `GD.PrintErr`.
+ `GodotTimeProvider.Process` and `GodotTimeProvider.PhysicsProcess` - `System.TimeProvider` implementations whose
  clocks and timers are advanced by Godot's process and physics loops rather than wall-clock time.

**Packaging**

+ Plugin installation and dependency management through the [ePlugin Framework](https://github.com/enaweg/godot-epluginframework).

## Motivation

Godot does not ship with a dependency-injection container, and maintaining scoped, hierarchical dependencies by hand
gets unwieldy in larger projects. VContainer already solves this problem for Unity, so eContainer ports its design to
Godot's `Node` and scene-tree model.

## Usage

Define a scope by extending `LifetimeScope` and registering dependencies in `Configure`:

```csharp
using Enaweg.Container.Godot;
using VContainer;

namespace YourGame;

public partial class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Entry points are the registrations that receive the lifecycle callbacks below.
        builder.RegisterEntryPoint<PlayerService>(Lifetime.Singleton);
        builder.Register<EnemySpawner>(Lifetime.Scoped);
    }
}
```

`LifetimeScope` is a `Node`, so add `GameLifetimeScope` to a scene like any other node. From code, create a typed
scope as a child of an existing one with `CreateChild<GameLifetimeScope>(...)` or
`CreateChildFromPackedScene<GameLifetimeScope>(scene)`; `LifetimeScope.Create(...)` is the shortcut for an ad-hoc,
untyped scope under the root. Either way the scope resolves its parent automatically - normally the
`RootLifetimeScope` autoload - so registrations cascade down the scope hierarchy.

### Registering into the root scope

The `eContainer` autoload owns the `RootLifetimeScope`, the container every other scope ultimately inherits from.
Unlike an ordinary scope, it does **not** build in `_EnterTree` - it builds in `_Ready`. Godot runs every autoload's
`_EnterTree`, and the whole main scene's, before the first `_Ready` fires anywhere, so deferring the build leaves a
window in which the rest of your project can still add registrations to the root container:

```csharp
public partial class GameBootstrap : Node // an autoload of your own
{
    LifetimeScope.ExtraInstallationScope installation;

    public override void _EnterTree()
        => installation = LifetimeScope.Enqueue(b => b.RegisterInstance(new SaveGameService()));

    public override void _ExitTree() => installation.Dispose();
}
```

Register from `_EnterTree`, not `_Ready`. `_EnterTree` works no matter where your autoload sits in the list, whereas
`_Ready` only runs before the root builds if your autoload is listed *above* `eContainer` in
**Project > Project Settings > Autoload**.

Two consequences are worth knowing:

- `LifetimeScope.Find<RootLifetimeScope>()!.Container` is `null` for the duration of every `_EnterTree` in the
  project, including the main scene's. A scope that enters the tree in that window is queued on the root's waiting
  list and builds as soon as the root does, so scopes in scenes need no special handling - but code reaching for
  `Container` directly from `_EnterTree` does.
- Anything that forces the root to build early - calling `Build()` on it yourself, or resolving from it during
  `_EnterTree` - closes the window for everyone. `Build()` is idempotent, so the `_Ready` build then becomes a no-op
  rather than a second container.

Resolved classes can opt into the entry-point lifecycle by implementing the annotation interfaces:

```csharp
using Enaweg.Container.Godot;

public class PlayerService : IInitializable, ITickable
{
    public void Initialize() { /* runs once the scope resolves */ }
    public void Tick(long frameCount) { /* runs every _Process */ }
}
```

The lifecycle interfaces are opt-in: register their implementation with `RegisterEntryPoint`, or add it through
`UseEntryPoints`, rather than using `Register` alone. `IInitializable` runs first, followed by
`IPostInitializable`; `ITickable` and `IPhysicsTickable` then run from `_Process` and `_PhysicsProcess`, respectively.

### Injecting into nodes

Nodes are not resolved from the container - they already exist in the scene tree - so they are injected instead:

```csharp
using Godot;
using VContainer;

public partial class Hud : Node
{
    PlayerService player = null!;

    [Inject]
    public void Construct(PlayerService player) => this.player = player;
}
```

A scope injects the nodes listed in its exported `AutoInjectNodes` array as soon as it builds. To inject a subtree
yourself, call `Container.InjectNode(node)`; it walks the node's descendants and stops at any nested `LifetimeScope`,
which injects its own subtree from its own container. For nodes you spawn at runtime, use
`Container.Instantiate<T>(packedScene, parent)` - it injects before attaching the node, so `_EnterTree` and `_Ready`
already see the injected members. An existing node can also be registered as a service with
`builder.RegisterNode<IHud>(hud)`, which injects it when the container is built.

## Development

To build the C# assembly:

```bash
dotnet build src/e-container/gContainer.sln --configuration Debug
```

No NuGet setup is needed on a clean checkout: the VContainer 1.19.0 packages are bundled in the repository under
`src/e-container/addons/eContainer/.libs` rather than published to nuget.org, and `src/e-container/nuget.config`
already registers that directory as a package source.

Opening `src/e-container/project.godot` in the Godot 4.7.2 .NET editor also triggers a build automatically and is the
normal way to exercise the plugins.

### Tests

Tests live in `src/e-container/test/` and run on [gdUnit4](https://github.com/MikeSchulze/gdUnit4) through the .NET
test adapter, so they need a Godot binary:

```bash
export GODOT_BIN=/path/to/godot
"$GODOT_BIN" --path src/e-container --editor --headless --quit-after 2000
dotnet test src/e-container/gContainer.sln --configuration Debug --settings src/e-container/.runsettings
```

The editor cache refresh is not optional: without it the `eContainer` autoload is never instantiated and the
`LifetimeScope` tests fail with null-reference errors.

### Release

Both pull requests and `v*` tag pushes build the solution, refresh the headless Godot editor cache, and run the full
gdUnit4 test suite. A tag push additionally stamps the tag's version into `addons/eContainer/plugin.cfg`, hides the
runtime sources as `.src`, zips `addons/eContainer` (including the bundled VContainer packages in `.libs`) into
`eContainer-v<version>.zip`, and drafts the GitHub release with a generated changelog.

### Project layout

`src/e-container/addons/` contains two plugins that are always co-deployed:

- **`ePlugin/`** - the vendored plugin lifecycle framework (upstream: [godot-epluginframework](https://github.com/enaweg/godot-epluginframework)).
- **`eContainer/`** - the VContainer port described above. It depends on ePlugin.

See `CLAUDE.md` for a deeper architecture walkthrough.

## Contribute

Feel free to contribute with documentation, testing, or pull requests.

## Commercial Support

Commercial services are available from [Enaweg](https://www.enaweg.at). If you need consulting, implementation
assistance, or tailored development services, please get in touch through their website.

## License

Licensed under the [MIT license](LICENSE).
