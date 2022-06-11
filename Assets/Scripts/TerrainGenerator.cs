using System;
using System.Collections.Generic;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour {

    const float viewerMoveThresholdForChunkUpdate = 25;
    const float sqrViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;

    public int colliderLODIndex;
    public LODInfo[] detailLevels;

    public MeshSettings meshSettings;
    public List<HeightMapSettings> heightMapSettings;

    public Transform viewer;
    public Material mapMaterial;

    Vector2 viewerPosition;
    Vector2 viewerPositionOld;

    float meshWorldSize;
    int chunksVisibleInViewdist;

    WaterChunkManager waterChunkManager;
    PoissonManager poissonManager;
    readonly Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new();
    readonly List<TerrainChunk> visibleTerrainChunks = new();


    void Start() {
        float maxViewDist = detailLevels[^1].visibleDstThreshold;
        meshWorldSize = meshSettings.MeshWorldSize;
        chunksVisibleInViewdist = Mathf.RoundToInt(maxViewDist / meshWorldSize);
        waterChunkManager = GetComponentInChildren<WaterChunkManager>();
        poissonManager = GetComponentInChildren<PoissonManager>();

        UpdateVisibleChunks();
    }


    void Update() {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);

        if (viewerPosition != viewerPositionOld) {
            foreach (TerrainChunk chunk in visibleTerrainChunks) {
                chunk.UpdateCollisionMesh();
            }
        }

        if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate) {
            viewerPositionOld = viewerPosition;
            UpdateVisibleChunks();
        }
    }


    void UpdateVisibleChunks() {
        HashSet<Vector2> alreadyUpdatedChunkCoords = new();

        // Iterate backwards because they can remove themselves from the list
        for (int i = visibleTerrainChunks.Count - 1; i >= 0; i -= 1) {
            alreadyUpdatedChunkCoords.Add(visibleTerrainChunks[i].coord);
            visibleTerrainChunks[i].UpdateTerrainChunk();
        }

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / meshWorldSize);
        int currentChunkCoordy = Mathf.RoundToInt(viewerPosition.y / meshWorldSize);

        for (int yOffset = -chunksVisibleInViewdist; yOffset <= chunksVisibleInViewdist; yOffset += 1) {
            for (int xOffset = -chunksVisibleInViewdist; xOffset <= chunksVisibleInViewdist; xOffset += 1) {
                Vector2 viewedChunkCoord = new(currentChunkCoordX + xOffset, currentChunkCoordy + yOffset);

                if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord)) {
                    if (terrainChunkDictionary.ContainsKey(viewedChunkCoord)) {
                        terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                    } else {
                        TerrainChunk newChunk = new(viewedChunkCoord, heightMapSettings, meshSettings, detailLevels, colliderLODIndex, transform, mapMaterial, viewer, waterChunkManager, poissonManager);
                        terrainChunkDictionary.Add(viewedChunkCoord, newChunk);
                        newChunk.OnVisibilityChanged += OnTerrainChunkVisibilityChanged;
                        newChunk.Load();
                    }
                }
            }
        }
    }


    void OnTerrainChunkVisibilityChanged(TerrainChunk chunk, bool isVisible) {
        if (isVisible) {
            visibleTerrainChunks.Add(chunk);
        } else {
            visibleTerrainChunks.Remove(chunk);
        }
    }
}


[Serializable]
public struct LODInfo {
    [Range(0, MeshSettings.numSupportedLODs - 1)]
    public int lod;
    public float visibleDstThreshold;

    public float SqrVisibleDstThreshold => visibleDstThreshold * visibleDstThreshold;
}
