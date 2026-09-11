# Ghost Lap Racing

A Unity/C# arcade racing project focused on vehicle physics, race systems, ghost replays, progression, and gameplay architecture.

This repository is a code-focused portfolio snapshot intended for technical review. It preserves the gameplay systems, project architecture, automated tests, and Unity project organization while excluding redistributable copies of third-party art assets. The complete visual environment can be restored with the officially distributed packages listed below.

## Overview

Ghost Lap Racing combines selectable vehicles with Time Trial and Drift Challenge modes, ordered checkpoints and laps, drift combos, race rewards, persistent upgrades, and recorded ghost laps. The repository is structured to make the implementation and system boundaries easy to explore without presenting third-party art as original work.

## Features

- WheelCollider-based vehicle physics
- Speed-sensitive steering with Ackermann geometry
- Configurable FWD, RWD, and AWD drivetrains
- Traction control, ABS-style braking, anti-roll forces, and stability assistance
- Downforce, aerodynamic drag, and rolling resistance
- Nitro boost with runtime effects, camera FOV response, and impact feedback
- Ordered checkpoints, lap validation, and checkpoint respawning
- Time Trial and Drift Challenge game modes
- JSON-based ghost recording and playback
- Persistent progression, rewards, and vehicle upgrades
- Event-driven race, checkpoint, and UI coordination

## Technical Highlights

- Component-oriented gameplay systems coordinated by `RaceManager`
- `ScriptableObject`-based `CarConfig` assets for vehicle data and tuning
- Physics work performed through `Rigidbody` and `WheelCollider` in `FixedUpdate`
- Events used to decouple race state, checkpoints, scoring, and interface updates
- Ghost data stored under `Application.persistentDataPath`, with temporary-file replacement for safer saves
- Runtime-created particles, materials, textures, and skid meshes with explicit cleanup
- Unity 6 compatibility through the current `Rigidbody.linearVelocity` API

## Architecture

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the detailed system map and execution flow.

At a high level, `VehicleSelectionManager` binds the selected car to `RaceManager`, camera, UI, and upgrade systems. `RaceManager` then coordinates checkpoints, lap timing, drift scoring, ghost recording, persistence, rewards, and race UI, while interface components primarily observe events and route player actions.

## Controls

| Action | Input |
| --- | --- |
| Accelerate / brake | `W` / `S` or arrow keys |
| Steer | `A` / `D` or arrow keys |
| Handbrake | `Space` |
| Nitro | `Left Shift` or `Right Shift` |
| Reset vehicle | `R` |
| Pause | `Escape` |

Gameplay input uses Unity's legacy Input Manager rather than the newer Input System package.

## Tests

59 EditMode tests passed in the complete local development project prior to preparation of this asset-stripped portfolio version.

The included tests cover checkpoint ordering, race flow, persistence, upgrades, and ghost data. They were not rerun in this stripped repository because the original scene and prefab references depend on excluded third-party assets. After restoring those assets, open **Window > General > Test Runner** in Unity and run the EditMode suite.

## Project Structure

```text
Assets/
├── Materials/
├── Prefabs/
├── Scenes/
├── Scriptable Objects/
├── Scripts/
│   ├── Camera/
│   ├── Core/
│   ├── Ghost/
│   ├── Input/
│   ├── Race/
│   ├── UI/
│   └── Vehicle/
├── Settings/
├── Tests/EditMode/
└── TextMesh Pro/
Packages/
ProjectSettings/
docs/
```

## Third-Party Assets

Third-party Asset Store source files are intentionally excluded from this repository because their licenses do not grant unrestricted source redistribution through a public Git repository.

The complete development project used:

- [ARCADE: FREE Racing Car](https://marketplace.unity.com/packages/3d/vehicles/land/arcade-free-racing-car-161085) — Mena — version 3.0
- [Cartoon City FREE - Low Poly City 3D Models Pack](https://marketplace.unity.com/packages/3d/environments/urban/cartoon-city-free-low-poly-city-3d-models-pack-328170) — ithappy — version 1.0

This is intentionally a code-focused repository, not a clone-and-play distribution of those packages. For the complete visual environment, acquire the assets from their official marketplace pages and follow [ASSET_SETUP.md](ASSET_SETUP.md). Attribution and licensing notes are documented in [THIRD_PARTY.md](THIRD_PARTY.md).

## Setup / Exploring the Project

1. Use Unity `6000.4.0f1`.
2. Open the repository and review `Assets/Scripts`, `Assets/Tests/EditMode`, and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md); these are available immediately without third-party source files.
3. To restore the full visual scene, acquire and import the exact packages listed in [ASSET_SETUP.md](ASSET_SETUP.md).
4. Restore the Unity packages declared in `Packages/manifest.json`.
5. Open `Assets/Scenes/City.unity`.
6. Run the EditMode tests and check the Console for missing references after asset restoration.

## Development Context

Developed as part of a volunteer game development internship. This repository is a personal technical presentation and does not imply endorsement by or ownership transfer from the host company.

## My Contributions

- Vehicle physics, drivetrain behavior, and handling configuration
- Race, checkpoint, lap, drift-score, reward, and game-mode systems
- Ghost recording, persistence, and playback
- Vehicle selection, save data, progression, and upgrade systems
- Camera behavior, nitro feedback, tire effects, race UI, pause, and restart flows

Third-party models, prefabs, textures, materials, and Unity-provided resources are not claimed as original work. Seventeen characterization-test paths were untracked in the former development repository; their relevance and passing status were verified, but authorship is not attributed from Git history.

## Third-Party Notices

See [THIRD_PARTY.md](THIRD_PARTY.md) for package names, authors, versions, official sources, and redistribution notes.

## License

No repository-wide open-source license is currently granted. Third-party assets referenced by the project remain subject to their respective licenses.
