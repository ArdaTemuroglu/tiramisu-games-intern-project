# Third-Party Assets and Notices

This document records evidence found during the local publication audit. It is not legal advice and does not grant rights to any asset or to the project as a whole.

## Unity Asset Store Packages Excluded from This Candidate

| Package | Publisher | Audited version | Official source | License shown by Unity | Expected import path |
| --- | --- | ---: | --- | --- | --- |
| ARCADE: FREE Racing Car | Mena | 3.0 | [Unity Asset Store](https://marketplace.unity.com/packages/3d/vehicles/land/arcade-free-racing-car-161085) | [Standard Unity Asset Store EULA](https://unity.com/legal/as-terms) | `Assets/ARCADE - FREE Racing Car/` |
| Cartoon City FREE - Low Poly City 3D Models Pack | ithappy | 1.0 | [Unity Asset Store](https://marketplace.unity.com/packages/3d/environments/urban/cartoon-city-free-low-poly-city-3d-models-pack-328170) | [Standard Unity Asset Store EULA](https://unity.com/legal/as-terms) | `Assets/ithappy/Cartoon_City_Free/` |

The Standard Unity Asset Store EULA licenses assets for permitted use, including incorporation into a licensed product, but does not provide a general right to republish reusable Asset Store source files in a public source repository. Therefore, neither the package contents nor the cached `.unitypackage` files are distributed here. Every developer must acquire the packages through Unity under their own account and applicable terms.

## Excluded Repository Paths

The public candidate excludes the two original vendor trees:

- `Assets/ARCADE - FREE Racing Car/`
- `Assets/ithappy/Cartoon_City_Free/`

It also excludes five ARCADE package assets that had been moved outside the vendor tree while retaining their package GUIDs and `AssetOrigin` metadata:

- `Assets/Materials/AFRC_Emission.mat`
- `Assets/Materials/AFRC_Env_Mat.mat`
- `Assets/Materials/AFRC_Mat_Col1.mat`
- `Assets/Materials/AFRC_Mat_Col4.mat`
- `Assets/Meshes/ARCADE - FREE Racing Car.fbx`

Their matching `.meta` files are excluded as well. Reimporting the official package restores the corresponding GUIDs under the package's own folder structure.

## Package Audit Findings

### ARCADE: FREE Racing Car

- **Audited cache archive SHA-256:** `A8CA33D892C91CA7B21EFDF39C6CB64D27526DA9E4B93E551FC6485D7DFCF35B`
- **Vendor files:** The cached version 3.0 package contained 52 asset paths: 38 file payloads and 14 folders. The source set covers the vehicle FBX, prefabs, materials, textures, sample scenes, skyboxes, and a volume profile.
- **User-created files inside the vendor folder:** None. Every current non-`.meta` file in the folder matched a path in the cached official package inventory.
- **Modified vendor files:** Seven material payloads differ from the cached originals. Three remain in the vendor tree: `AFRC_Mat_Col2.mat`, `AFRC_Mat_Col3.mat`, and `AFRC_Mat_Col5.mat`. Four had been moved to `Assets/Materials/`: `AFRC_Emission.mat`, `AFRC_Env_Mat.mat`, `AFRC_Mat_Col1.mat`, and `AFRC_Mat_Col4.mat`. The observed ARCADE material changes include conversion from the Built-in Standard shader to URP/Lit. The relocated FBX payload is byte-identical to the package original.
- **GUID evidence:** All 52 package GUIDs are preserved when the five relocated items are included. Existing package paths matched `47/47`; relocated items matched `5/5`.
- **Referenced by:** `Assets/Prefabs/PlayerCar.prefab`, `Assets/Scenes/City.unity`, and the relocated project-path material files. The scene has five transitive ARCADE dependencies.

The cached package alone cannot establish who performed the material conversion; the vendor trees were unchanged across the repository's two commits.

### Cartoon City FREE - Low Poly City 3D Models Pack

- **Audited cache archive SHA-256:** `2C032B6A0F5F0E951645133545D86CBEB4DF6E837EB26FDB898F150747D8B6BA`
- **Vendor files:** The cached version 1.0 package contained 227 asset paths: 198 file payloads and 29 folders. The source set covers environment meshes, prefabs, materials, textures, demo scenes, skyboxes, render-pipeline profiles, and conversion helpers.
- **User-created files inside the vendor folder:** None. Every current non-`.meta` file matched a path in the cached official package inventory.
- **Modified vendor files:** Sixteen material payloads differ from the cached originals: `Asphalt_Dark_Gray.mat`, `Billboard.mat`, `Car_Color.mat`, `Car_Headlights.mat`, `Car_Taillights.mat`, `Color.mat`, `Color_Glossy.mat`, `Emissive.mat`, `Glass.mat`, `Graffiti.mat`, `Grass.mat`, `Metal.mat`, `Road_Signs.mat`, `Roads.mat`, `scrolling text.mat`, and `Tile_1.mat`. Intent and authorship cannot be established from the available two-commit history.
- **Removed vendor helpers:** The current development snapshot no longer contains the package's `Render_Pipeline_Convert/` folder, its instructions, or its three nested pipeline `.unitypackage` files.
- **GUID evidence:** All `222/222` package entries that remain in the development snapshot retain the official package GUIDs. The five absent entries are the removed conversion folder and its four children.
- **Referenced by:** `Assets/Scenes/City.unity`. The scene has 45 direct and 108 transitive Cartoon City dependencies.

Many `.meta` files differ byte-for-byte from the cached package due to imported/editor metadata, but every compared GUID matches. A changed `.meta` file alone is not treated as proof of a user-authored asset.

## Unity and Package Manager Content

- Unity registry packages are restored from `Packages/manifest.json`; package source is not vendored into this repository.
- `Assets/TextMesh Pro/Fonts/LiberationSans.ttf` is accompanied by `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`, which contains the SIL Open Font License 1.1 notice. Keep that notice with redistributed copies of the font software.
- `com.coplaydev.unity-mcp` is pinned in the Package Manager files; its own upstream terms apply.

## Publication Gate

Before publication, obtain explicit confirmation that the internship project code, serialized scenes/prefabs, project name, and any allowed media may be published on a personal GitHub account. No repository-wide open-source license has been added.
