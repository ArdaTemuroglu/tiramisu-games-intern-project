# External Asset Setup

This portfolio candidate intentionally omits Unity Asset Store source content. The C# source and project configuration can be inspected immediately, but the playable scene is not visually complete until the exact external packages are imported.

## Requirements

- Unity `6000.4.0f1`
- Universal Render Pipeline project dependencies restored from `Packages/manifest.json`
- A Unity account that has acquired the required packages under the applicable Asset Store terms

## Required Packages

| Package | Publisher | Version audited | Official source | Expected path after import |
| --- | --- | ---: | --- | --- |
| ARCADE: FREE Racing Car | Mena | 3.0 | [Unity Asset Store](https://marketplace.unity.com/packages/3d/vehicles/land/arcade-free-racing-car-161085) | `Assets/ARCADE - FREE Racing Car/` |
| Cartoon City FREE - Low Poly City 3D Models Pack | ithappy | 1.0 | [Unity Asset Store](https://marketplace.unity.com/packages/3d/environments/urban/cartoon-city-free-low-poly-city-3d-models-pack-328170) | `Assets/ithappy/Cartoon_City_Free/` |

Do not obtain these packages from this repository or from an unofficial mirror. Acquire and download them from their official Unity Asset Store pages.

## Import Procedure

1. Install Unity `6000.4.0f1` through Unity Hub.
2. Acquire both packages from their official pages while signed in to Unity.
3. Open this project and allow Unity Package Manager to restore the manifest dependencies.
4. In **Window > Package Manager > My Assets**, download and import ARCADE version 3.0 with its original folder structure.
5. Download and import Cartoon City FREE version 1.0 with its original folder structure.
6. Confirm that the two expected paths shown above exist.
7. Because this project uses URP, follow the publisher-provided render-pipeline instructions if Unity reports incompatible or pink materials. The audited ARCADE snapshot used URP/Lit conversions for seven materials; visual parity may therefore require material conversion after a clean package import.
8. Reopen `Assets/Scenes/City.unity`, let Unity finish importing, run the EditMode tests, and inspect the Console before entering Play mode.

Importing a different package release may change paths, GUIDs, serialized material data, or render-pipeline behavior. Version 3.0 and version 1.0 are the versions verified by this audit.

## Expected Missing Content Before Import

`Assets/Scenes/City.unity` retains serialized references to the excluded packages:

- Within the two original vendor folders, 46 direct dependencies: one ARCADE material and 45 Cartoon City prefabs/profile/skybox assets
- Within those folders, 113 transitive dependencies: five ARCADE assets and 108 Cartoon City assets
- In addition, the scene directly references two relocated ARCADE materials that this candidate also excludes; a static GUID scan therefore finds 48 vendor-origin GUIDs in the scene
- `Assets/Prefabs/PlayerCar.prefab` directly references four relocated ARCADE assets: three materials and the vehicle FBX

Without the packages:

- the city road, sidewalk, building, billboard, prop, vegetation, skybox, and volume-profile content will be missing;
- `Assets/Prefabs/PlayerCar.prefab` will have missing vehicle mesh and material references;
- the main scene may show missing prefab/mesh/material references and is not an honest one-click clone-and-play experience.

No gameplay C# scripts were removed with the vendor assets.

## GUID Reconnection Evidence

**Expected to restore references: YES, for the exact audited package versions and their original metadata.**

The local audit compared the project against the official packages cached by Unity:

- ARCADE: all 52 package GUIDs matched, including five vendor assets that had been relocated in the development copy.
- Cartoon City: all 222 package entries still present in the development copy matched their package GUIDs; the five unmatched inventory entries were intentionally removed render-pipeline helper paths.

Unity serialization resolves asset references by GUID, so importing these exact package versions is expected to reconnect the scene and prefab references. This conclusion is conditional: Unity Store updates or a publisher changing GUIDs would make restoration uncertain. It also covers reference reconnection, not exact visual parity; modified material values and URP conversion may still need review.

## Licensing Note

These instructions do not bypass the Unity Asset Store or redistribute package content. Each developer is responsible for accepting and following the package license. See [THIRD_PARTY.md](THIRD_PARTY.md).
