<div align="center">

# eContainer

**[VContainer](https://github.com/hadashiA/VContainer) — Unity's fast DI container — ported to [Godot](https://godotengine.org/).**

![Godot 4.7](https://img.shields.io/badge/Godot-v4.7-202020?logo=godot-engine&logoColor=blue&color=darkgreen&labelColor=202020)

![Dotnet 8](https://img.shields.io/badge/8-02020?logo=dotnet&logoSize=auto&logoColor=purple&color=darkgreen&labelColor=E0E0E0)
![Dotnet 9](https://img.shields.io/badge/9-02020?logo=dotnet&logoSize=auto&logoColor=purple&color=darkgreen&labelColor=E0E0E0)

**NOTE**: This is currently in an experimental state and very much WIP!

</div>

## Features

+ [VContainer](https://github.com/hadashiA/VContainer)'s DI container (`IObjectResolver`/`IContainerBuilder`) ported from Unity's `MonoBehaviour` lifecycle to a Godot `Node` lifecycle.
+ `LifetimeScope` — a DI scope `Node` that builds/tears down its container on `_EnterTree`/`_ExitTree`, resolves its parent scope automatically, and supports child scopes created in code or instantiated from a `PackedScene`.
+ A single top-level `RootLifetimeScope`, registered as an autoload, that queues up child scopes whose declared parent hasn't entered the tree yet and flushes them as matching parents become ready.
+ Entry-point/tickable annotations (`IInitializable`, `IPostInitializable`, `ITickable`, `IPhysicsTickable`) wired to Godot's `_Process`/`_PhysicsProcess` callbacks.
+ Installed and managed via the [ePlugin Framework](https://github.com/enaweg/godot-epluginframework) — enabling the plugin automatically wires up the vendored `VContainer.Standalone`/`VContainer.SourceGenerator` NuGet packages and the `eContainer` autoload.

## Why?

Godot doesn't ship with a dependency-injection container, and rolling your own scoped, hierarchical DI setup by hand
gets unwieldy fast in larger projects. VContainer already solved this well for Unity — this project ports that
proven design to Godot's `Node`/scene-tree model instead of reinventing DI from scratch.

## Installation

1. Copy `addons/eContainer` and `addons/ePlugin` (eContainer depends on ePlugin) into your project's `addons/` directory.
2. In Godot, open **Project > Project Settings > Plugins** and enable both **ePlugin** and **eContainer**.
3. Enabling the plugin adds the `VContainer.Standalone`/`VContainer.SourceGenerator` NuGet packages to your `.csproj` and registers the `eContainer` autoload — no manual `dotnet add package` needed.

## Usage

Define a scope by extending `LifetimeScope` and registering your dependencies in `Configure`:

```csharp
using Enaweg.Container.Godot;
using VContainer;

namespace YourGame;

public partial class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<IPlayerService, PlayerService>(Lifetime.Singleton);
        builder.Register<EnemySpawner>(Lifetime.Scoped);
    }
}
```

Attach `GameLifetimeScope` to a `Node` in your scene (or create one from code with `LifetimeScope.Create(...)`).
`LifetimeScope` resolves its parent scope automatically — normally the `RootLifetimeScope` autoload — so registrations
cascade down the scope hierarchy just like in VContainer for Unity.

Classes resolved by the container can opt into the entry-point lifecycle by implementing the annotation interfaces:

```csharp
using Enaweg.Container.Annotations;

public class PlayerService : IInitializable, ITickable
{
    public void Initialize() { /* runs once the scope resolves */ }
    public void Tick() { /* runs every _Process */ }
}
```

## Development

- Build the C# assembly: `dotnet build src/e-container/gContainer.csproj` (targets `net8.0`, or `net9.0` when
  `GodotTargetPlatform=android`).
- Opening `src/e-container/project.godot` in the Godot 4.7 editor triggers a dotnet build automatically and is the
  normal way to exercise the plugin.
- There is currently no automated test project in the repo.

### Project layout

`src/e-container/addons/` contains two plugins that are always co-deployed:

- **`ePlugin/`** — the plugin lifecycle framework (vendored, upstream: [godot-epluginframework](https://github.com/enaweg/godot-epluginframework)). Handles NuGet/`.csproj`/autoload wiring for ePlugin-based plugins; has no knowledge of VContainer.
- **`eContainer/`** — the VContainer port described above. Depends on ePlugin.

See `CLAUDE.md` for a deeper architecture walkthrough.

## Contribute

Feel free to contribute with Documentation, Testing, or PRs.

## Commercial Support

Commercial services are available from [Enaweg](https://www.enaweg.at). If you need consulting, implementation
assistance, or tailored development services, please get in touch through their website.

## License

Licensed under the MIT license, see `LICENSE` for more information.
