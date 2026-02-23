# NifSkope glTF 2.0 Import and Export v1.2

glTF 2.0 export and import are currently supported on static and skinned Skyrim (import is limited to Special Edition), Fallout 4, Fallout 76 and Starfield meshes. Collision data, controllers and particle systems cannot be exported or imported, and there are a number of other limitations described below.

# glTF Import

## Skinned meshes

Skin partitions (used by Skyrim) are currently not supported, the mesh is always imported as BSTriShape for games before Starfield. Weights and bone transforms are imported for all games.

For Starfield only, setting the "Flat" custom boolean property on a skeleton node disables importing that node and all of its children. This is useful when the model needs to use an external skeleton file, with the bone nodes referenced by name in a SkinAttach block instead of being imported to the NIF.

## Materials

Material import is limited to setting material paths, using the name of the material, or the "Material Path" extra data if available. Shader property data and texture sets need to be created manually, or copied from an existing model.

# glTF Export

To view and export Starfield meshes, you must first:

1. Enable and add the path to your Starfield installation in Settings > Resources.
2. Add the Meshes archives or extracted folders containing `geometries` to Paths in Settings > Resources, under Starfield.

If no item is selected, then the entire scene is exported. Otherwise, either a node or a shape must be selected, and only that item and its children are exported.

## Skinned meshes

### Pre-Export

If you do not desire the full posable skeleton, you may skip these steps.

If you do not paste in the COM skeleton, a flat skeleton will be reconstructed for you.
**Please note:** You will receive a warning after export that the skeleton has been reconstructed for you. This is fine.

If you desire the full posable skeleton, and the mesh does not have a skeleton with a `COM` or `COM_Twin` NiNode in its NIF:

1. Open the skeleton.nif for that skinned mesh (e.g. `meshes\actors\human\characterassets\skeleton.nif`)
2. Copy (Ctrl-C) the `COM` NiNode in skeleton.nif
3. Paste (Ctrl-V) the entire `COM` branch onto the mesh NIF's root NiNode (0)
4. Export to glTF

### Pre-Blender Import

As of Exporter v1.1 you should **no longer need to uncheck "Guess Original Bind Pose"**.

## Materials

glTF export includes a limited set of material settings, and textures from the first layer of the material, which are saved in the output .bin file in PNG format. Replacement colors are stored as 1x1 textures. Texture quality can be configured in the general settings with the 'Export texture mip level' option. A mip level of -1 disables texture export.

For Skyrim, only the diffuse and normal maps are exported. PBR support is limited for Fallout 4, while in the case of Fallout 76 materials, albedo, roughness, metalness and occlusion maps are generated from the texture set.

## LOD

Exporting and importing LOD meshes is implemented only for Starfield. It is disabled by default, and can be enabled in the general settings. When enabled, LOD meshes use the MSFT\_lod glTF extension.

## Blender scripts

Blender scripts are provided for use with glTF exports. They may be opened and run from the Scripting tab inside Blender.

1. `gltf_lod_blender_script.py` is included in the `scripts` folder for managing LOD visibility in exported glTF.
