# NoOpTexturePacker

This ssmall software can convert PBR textures stored in roughness, metallic and ambient occlusion textures to ORM textures for Unity and Unreal Engine.
It can also create smoothness textures from roughness by inverting the texture and store it as the alpha of the metallic texture for Unity's default shaders.

Optionally it can delete the roughness, metallic and AO textures it used for ORM creation.

# How To Use

Build and run the solution using visual studio or any other supported way for .NET 8 and then run the program. It asks you which folder to search for textures and what extension to use.
The default extension is PNG. Then it asks for yes and no questions on what textures to generate and if it should delete the original roughness, metallic and AO textures.

The search is recursive and it expects a folder to contain all 3 textures and the names are case sensitive but they names should contain `roughness` `metallic` and `AO` in their names.

# Performance

The code is multi-threaded and will use all of the cores in your machine and its performance is ok but it can be improved by using faster ImageSharp texture manipulation techniques or SIMD.