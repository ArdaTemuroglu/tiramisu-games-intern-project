# Technical Architecture

This document maps the project-authored C# under `Assets/Scripts`. It describes observable responsibilities and dependencies; it does not attribute Unity package code or third-party assets to the project author.

## Runtime Flow

```text
VehicleSelectionManager
  -> binds the selected CarController to RaceManager, CameraFollow,
     UIManager, UpgradeManager, DriftScoreManager, and GhostReplaySystem

RaceManager
  -> coordinates mode selection, countdown, race state, checkpoints,
     timers, drift scoring, ghost playback, rewards, persistence, and UI

LapTimer + CheckpointManager
  -> emit progress events consumed by UIManager and GhostReplaySystem
```

## System Map

| Area | Files | Responsibility and integration | Principal Unity/.NET APIs |
| --- | --- | --- | --- |
| Camera | `Camera/CameraFollow.cs` | Smooth follow and look-ahead camera, speed-based FOV, additional nitro FOV, and collision-direction impact shake. Retargeted by `VehicleSelectionManager`; reads `CarController` and `NitroSystem`; receives impact requests from `VehicleImpactFeedback`. | `Camera`, `Transform`, `Vector3.SmoothDamp`, `Quaternion.Slerp`, `Mathf.Exp`, scaled and unscaled `Time` |
| Ghost | `Ghost/GhostReplaySystem.cs` | Records position/rotation frames for each lap, keeps a best lap per scene/vehicle/mode, interpolates playback, and constructs a translucent runtime visual from the selected car. Subscribes to `LapTimer` and reads `RaceManager`. | `Application.persistentDataPath`, `File`, `Directory`, `JsonUtility`, `SceneManager`, `Mesh`, `MeshFilter`, `SkinnedMeshRenderer.BakeMesh`, `Material`, `Vector3.Lerp`, `Quaternion.Slerp` |
| Race | `Race/RaceMode.cs`, `Race/RaceManager.cs`, `Race/Checkpoint.cs`, `Race/CheckpointManager.cs`, `Race/LapTimer.cs`, `Race/DriftScoreManager.cs`, `Race/CheckpointMarkerVisual.cs` | Defines Time Trial and Drift Challenge, a waiting/countdown/racing/finished state machine, ordered checkpoint and finish-line validation, checkpoint-based respawn poses, lap timing/events, drift-angle/speed/combo scoring, race rewards, and active-checkpoint feedback. `RaceManager` is the coordinator for save, UI, ghost, drift, timer, and player-control state. | Coroutines and `WaitForSeconds`, trigger callbacks, `Collider`/`Rigidbody`, `Vector3.Angle`/`ProjectOnPlane`, `Time`, C# `Action` events, `SceneManager`, `Application.Quit` |
| Systems | `Systems/VehicleSelectionManager.cs`, `Systems/SaveManager.cs`, `Systems/UpgradeManager.cs`, `Systems/PauseManager.cs` | Selects and activates a configured car, rebinds all runtime consumers, stores cash/records/upgrade levels, applies engine/grip/nitro multipliers, calculates escalating upgrade costs, and coordinates pause/restart/quit state. | `PlayerPrefs`, `JsonUtility`, `Time.timeScale`, `AudioListener.pause`, `Input.GetKeyDown`, `GetComponentInChildren`, C# events |
| UI | `UI/UIManager.cs` | Coordinates selection, mode, HUD, countdown, checkpoint/lap feedback, drift telemetry, pause, results, and garage panels. It subscribes to gameplay events and uses unscaled animation time for UI that must continue while paused. | TextMesh Pro, Unity UI (`Button`, `Image`, `Slider`), coroutines, `Time.unscaledDeltaTime`, event subscriptions |
| Vehicle | `Vehicle/CarConfig.cs`, `Vehicle/CarController.cs`, `Vehicle/NitroSystem.cs`, `Vehicle/NitroVfxController.cs`, `Vehicle/TireEffectsController.cs`, `Vehicle/VehicleImpactFeedback.cs` | Data-driven tuning; FWD/RWD/AWD torque distribution; input smoothing; speed-sensitive Ackermann steering; traction/ABS modulation; handbrake drift grip; suspension, anti-roll, aerodynamic, downforce, and stability forces; nitro resource/force; procedural nitro and tire particles; bounded skid-mesh generation; and collision-to-camera feedback. | `ScriptableObject`, `Rigidbody`, `WheelCollider`, `WheelHit`, `WheelFrictionCurve`, `AddForce`/`AddTorque`/`AddForceAtPosition`, `Physics.SyncTransforms`, legacy `Input`, `ParticleSystem`, `Mesh`, runtime `Material`/`Texture2D`, collision contacts |

## Data and Event Boundaries

- `CarConfig` owns reusable vehicle physics parameters; `CarController` applies them to the active Rigidbody and wheel colliders.
- `VehicleSelectionManager` performs runtime dependency rebinding when a car is chosen instead of requiring separate manager instances per vehicle.
- `LapTimer` emits race/lap events. `GhostReplaySystem` uses those events to begin recording and decide whether a completed lap replaces the stored ghost.
- `CheckpointManager` accepts only the active player car and the next required checkpoint; it updates the car's respawn pose after valid progress.
- `SaveManager` owns persistent cash, best records, and upgrade levels. `UpgradeManager` translates levels into runtime multipliers and notifies the UI.
- `UIManager` observes state and routes button actions, while the gameplay managers retain the game rules.

## Persistence

- Progress data is serialized with `JsonUtility` into one `PlayerPrefs` value.
- Ghost data is stored as JSON files below `Application.persistentDataPath/Ghosts`.
- Ghost file names are scoped by scene, selected vehicle profile, and race mode and are sanitized before use.
- Ghost writes use a `.tmp` file followed by replacement of the previous save, reducing the chance of a partially written final file.

## Input and Rendering Notes

- Driving and pause controls currently use Unity's legacy Input Manager (`Input.GetAxisRaw`, `Input.GetKey`, and `Input.GetKeyDown`). The repository also contains a template Input System actions asset, but the gameplay scripts do not use it; the project should not be described as Input-System-driven.
- The project uses URP. Nitro and tire effects build runtime particle/material resources, while the main city and vehicle appearance depend on excluded Asset Store packages. See [External Asset Setup](../ASSET_SETUP.md).

## Validation Scope

The full development copy passed Unity compilation and `59/59` EditMode tests on September 11, 2026, with an empty Console after validation. The asset-stripped portfolio copy has not been opened as a second Unity project because doing so would generate local cache data and cannot validate missing third-party visuals. Re-run compilation, tests, scene checks, and play-mode inspection after importing the required packages.
