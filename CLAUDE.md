# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

`godot-econtainer` is a port of Unity's [VContainer](https://github.com/hadashiA/VContainer) dependency-injection
library to the Godot Engine (4.7, C#/.NET). The actual Godot project lives under `src/e-container/` (Godot project
root, assembly name `gContainer`), and the DI library ships as a Godot editor addon at
`src/e-container/addons/eContainer/`.

A second addon, `src/e-container/addons/ePlugin/`, is a small in-house framework for building declarative,
self-installing Godot C# editor plugins. `eContainer` is built on top of `ePlugin` and depends on it.

**`addons/ePlugin/` is read-only.** It is vendored here like an external/NuGet dependency — this repo simply
tracks its latest released version. Never modify, refactor, add tests for, or otherwise change any file under
`addons/ePlugin/` as part of work on this repo. If a change to `ePlugin` itself seems necessary, say so instead of
editing it here.

## Git workflow

Use the GitKraken MCP server for all git handling (status, add, commit, push, branching, pull requests, etc.)
instead of raw `git`/`gh` shell commands.

## Commands

There is no separate CLI build step outside of Godot/.NET tooling — the addons are Godot editor plugins, not
standalone libraries.

- Build the C# assembly: `dotnet build src/e-container/gContainer.csproj` (targets `net8.0`, or `net9.0` when
  `GodotTargetPlatform=android`).
- Opening `src/e-container/project.godot` in the Godot 4.7 editor will trigger a dotnet build automatically and is
  the normal way to exercise the plugins (they register/unregister themselves via the editor plugin lifecycle).
- Tests live in `src/e-container/test/` inside the same `gContainer.csproj` and use gdUnit4 via the .NET test
  adapter. Run them with `dotnet test src/e-container/gContainer.sln --settings src/e-container/.runsettings`; a
  `GODOT_BIN` environment variable pointing at a Godot binary is required. On a stale or missing `.godot/` cache the
  `eContainer` autoload is not instantiated, `LifetimeScope.Root` stays null, and every `LifetimeScopeTest` case
  fails with an NRE — run `"$GODOT_BIN" --path src/e-container --editor --headless --quit-after 2000` first, which
  is exactly what CI does before its test step.
- CI (`.github/workflows/ci-pr.yml`) builds the solution, refreshes the headless Godot editor cache, and runs the
  gdUnit4 suite under `xvfb-run` for pull requests. Release CI (`.github/workflows/ci-release.yml`) performs the
  same build-and-test validation on `v*` tag pushes, stamps the version into `addons/eContainer/plugin.cfg`,
  renames `addons/eContainer/src` to the hidden `.src` used for distribution, zips `addons/eContainer` (with the
  bundled VContainer `.nupkg` files in `.libs`) into a release artifact, verifies the archive's contents, and
  drafts a GitHub release.

## Architecture

### `ePlugin` — declarative editor plugin framework (`addons/ePlugin/`, read-only)

Treat this directory as vendored/external code (see note above) — described here only so its behavior can be
understood, not modified. Editor-only code (`#if TOOLS`) that lets a Godot C# `EditorPlugin` describe what it needs via a fluent recipe
instead of imperatively wiring things up in `_EnablePlugin`/`_DisablePlugin`:

- `IEEditorPlugin.CreateRecipe(IEEditorPluginBuilder)` — plugins implement this to declare autoloads, plugin
  dependencies (with optional version constraints), NuGet packages, extra `.csproj` project references, and
  directories whose editor/IDE visibility should track the plugin's enabled state.
- `EGlobal` (singleton) drives the actual install/uninstall of a plugin's recipe, resolves plugin dependency
  ordering (enabling dependencies first, disabling dependents first), and tracks each plugin's state
  (`Created` → `Activated`/`Deactivated`/`Error`) in a `PluginContext`.
- `EGlobal` re-initializes itself from `EPluginPlugin._Process` because a C# assembly reload in the editor wipes
  static state; `ReloadContexts` rebuilds `PluginContext`s from the live scene tree of editor plugin nodes.
- Actual `dotnet` CLI work (adding/removing NuGet packages, solution/project references) is abstracted behind
  `IDotnetCli`, implemented differently per SDK version (`DotnetCli9` vs `DotnetCli10`, selected by
  `DotnetVersionManager` based on the installed `dotnet --version`).
- `EPlugin` is the public static entry point other code uses to reach the running framework instance (e.g. to call
  `EPlugin.RegisterInitializer`) without doing the scene-tree lookup itself.

### `eContainer` — the VContainer port (`addons/eContainer/`)

- `EContainerPlugin` (editor-only) declares its `ePlugin` recipe: install the vendored `VContainer.Standalone` /
  `VContainer.SourceGenerator` NuGet packages (1.19.0) from `.libs/` (local `.nupkg` files, not a public feed), show
  the `.src` source directory, and register the `eContainer.tscn` autoload. In the repo the sources are checked in
  as `src/` (the shown state); the release workflow hides them as `.src` so a freshly extracted, disabled plugin is
  not compiled into the consuming project.
- Runtime code lives in `src/Runtime/` and is split into:
  - `Godot/` — the Godot-specific adaptation layer of VContainer, most importantly `LifetimeScope`
    (`src/Runtime/Godot/LifetimeScope.cs`), the DI scope node ported from VContainer's Unity `MonoBehaviour` scope
    to a Godot `Node`. It builds/tears down its `IObjectResolver` container on `_EnterTree`/`_ExitTree` instead of
    `Awake`/`OnDestroy`, resolves its parent scope via `ParentReference` (explicit object, discovered type name, a
    `FindParent()` override, or a global override stack), and supports child scopes created either in code
    (`CreateChild`) or instantiated from a `PackedScene` (`CreateChildFromPackedScene`).
  - `RootLifetimeScope` — the single top-level scope (registered as the `eContainer` autoload), which also handles
    scopes whose declared parent type hasn't entered the tree yet via a `WaitingList` that gets flushed as matching
    parents become ready or the current scene changes. Unlike every other scope it builds in `_Ready`, not
    `_EnterTree`: Godot runs all autoloads' `_EnterTree` and the whole main scene's before the first `_Ready`, so
    deferring keeps a window open in which consuming projects can still `LifetimeScope.Enqueue()` installers into
    the root container. `_EnterTree` therefore suppresses `AutoRun` around its `base._EnterTree()` call, and
    `_ExitTree` calls `RequestReady()` so a root that re-enters the tree builds again (Godot notifies READY only
    once per node otherwise). Scopes that enter the tree before the root has built simply queue on the
    `WaitingList` and are flushed by the root's own `Build()`.
  - `EntryPointsBuilder` / `EntryPointDispatcher` / `FrameProviderDispatcher` / `GodotFrameProvider` /
    `GodotTimeProvider` — wire VContainer's entry-point/tickable system to Godot's `_Process`/`_PhysicsProcess`
    callbacks.
  - `Annotations/` — marker interfaces resolved objects implement to participate in the lifecycle:
    `IInitializable`, `IPostInitializable`, `ITickable`, `IPhysicsTickable`.
  - `Internal/` — supporting data structures shared with upstream VContainer (`FreeListCore`,
    `CompositeDisposable`, `FuncRegistrationBuilder`, etc.).
  - `Editor/` — Godot inspector customizations for `LifetimeScope`'s parent reference field
    (`ParentReferenceEditorProperty`, `LifeScopeInspectorPlugin`).

### Plugin dependency direction

`eContainer` depends on `ePlugin` (declared via `AddPluginDependency("ePlugin")` in `EEditorPluginBuilder`'s
constructor) — `ePlugin` has no knowledge of `eContainer` and is meant to be reusable by other Enaweg editor
plugins.
