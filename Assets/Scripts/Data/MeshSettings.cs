using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class MeshSettings : UpdatableData {

    public const int numSupportedLODs = 5;
    public const int numSupportedChunkSizes = 9;
    public const int numSupportedFlatShadedChunkSizes = 3;
    public static readonly int[] supportedChunkSizes = {48, 72, 96, 120, 144, 168, 192, 216, 240};

    // Scale at which the mesh should be rendered at
    public float meshScale = 5f;
    public bool useFlatShading;

    [Range(0, numSupportedChunkSizes - 1)]
    public int chunkSizeIndex;
    [Range(0, numSupportedFlatShadedChunkSizes - 1)]
    public int flatShadedChunkSizeIndex;

    // num vertices per line of mesh rendered at LOD = 0
    // Includes 2 extra vertices that are exluded from final mesh but used for calculating normals.
    public int NumVerticesPerLine => supportedChunkSizes[useFlatShading ? flatShadedChunkSizeIndex : chunkSizeIndex] + 1;

    // How large the mesh is in real coordinates
    public float MeshWorldSize => (NumVerticesPerLine - 3) * meshScale;
}
