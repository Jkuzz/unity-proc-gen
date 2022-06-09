using System;
using System.Collections.Generic;
using UnityEngine;

public class TerrainChunk {

    const float colliderGenerationDistanceThreshold = 5;

    public event Action<TerrainChunk, bool> OnVisibilityChanged;
    public Vector2 coord;

    readonly GameObject meshObject;
    Vector2 sampleCenter;
    Bounds bounds;

    GameObject waterChunk;

    readonly WaterChunkManager waterChunkManager;
    readonly MeshRenderer meshRenderer;
    readonly MeshFilter meshFilter;
    readonly MeshCollider meshCollider;
    readonly LODInfo[] detailLevels;
    readonly LODMesh[] lodMeshes;
    readonly int colliderLODIndex;

    HeightMap heightMap;
    bool HeightMapsReceived => heightMapsReceived >= heightMapsRequested;

    bool hasSetCollider;
    int previousLODIndex = -1;
    readonly float maxViewDist;

    readonly List<HeightMapSettingsSelect> heightMapSettingsList;
    // height maps stored in dictionary to allow easily fetching specific map
    readonly Dictionary<string, HeightMap> heightMaps = new();
    float[,] combinedTerrainHeightMaps;
    readonly MeshSettings meshSettings;
    readonly Transform viewer;

    int heightMapsRequested = 0;
    int heightMapsReceived = 0;


    public TerrainChunk(Vector2 coord, List<HeightMapSettingsSelect> heightMapSettingsList, MeshSettings meshSettings, LODInfo[] detailLevels, int colliderLODIndex, Transform parent, Material material, Transform viewer, WaterChunkManager waterChunkManager) {
        this.detailLevels = detailLevels;
        this.colliderLODIndex = colliderLODIndex;
        this.coord = coord;
        this.heightMapSettingsList = heightMapSettingsList;
        this.meshSettings = meshSettings;
        this.viewer = viewer;
        this.waterChunkManager = waterChunkManager;

        sampleCenter = coord * meshSettings.MeshWorldSize / meshSettings.meshScale;
        Vector2 position = coord * meshSettings.MeshWorldSize;
        bounds = new(sampleCenter, Vector2.one * meshSettings.MeshWorldSize);

        meshObject = new GameObject("Terrain Chunk");
        meshRenderer = meshObject.AddComponent<MeshRenderer>();
        meshFilter = meshObject.AddComponent<MeshFilter>();
        meshCollider = meshObject.AddComponent<MeshCollider>();

        meshRenderer.material = material;
        meshObject.transform.position = new Vector3(position.x, 0, position.y);

        Transform chunkParent = parent.Find("Chunk Container");
        if (chunkParent == null) {
            GameObject newContainer = new("Chunk Container");
            newContainer.transform.parent = parent;
            chunkParent = newContainer.transform;
        }
        meshObject.transform.parent = chunkParent;

        SetVisible(false);

        lodMeshes = new LODMesh[detailLevels.Length];
        for (int i = 0; i < detailLevels.Length; i += 1) {
            lodMeshes[i] = new LODMesh(detailLevels[i].lod);
            lodMeshes[i].UpdateCallback += UpdateTerrainChunk;
            if (i == colliderLODIndex) {
                lodMeshes[i].UpdateCallback += UpdateCollisionMesh;
            }
        }
        maxViewDist = detailLevels[^1].visibleDstThreshold;
    }


    public void Load() {
        heightMapsRequested = heightMapSettingsList.Count; // to avoid potential race condition
        foreach (HeightMapSettingsSelect settings in heightMapSettingsList) {
            if (!settings.enabled) {
                heightMapsRequested -= 1;
                continue;
            }
            ThreadedDataRequester.RequestData(
                () => HeightMapGenerator.GenerateHeightMap(meshSettings.NumVerticesPerLine, settings.heightMapSettings, sampleCenter),
                OnHeightMapReceived
            );
        }
    }


    void OnHeightMapReceived(object heightMapObject) {
        heightMapsReceived += 1;
        heightMap = (HeightMap)heightMapObject;
        heightMaps.Add(heightMap.heightMapSettings.mapName, heightMap);

        if (HeightMapsReceived) {
            // meshRenderer.material.SetTexture();
            UpdateTerrainChunk();
        }
    }


    Vector2 ViewerPosition => new(viewer.position.x / meshSettings.meshScale, viewer.position.z / meshSettings.meshScale);


    // Update the chunk when entered/leaves render distance
    // Called when height maps and meshes are received
    public void UpdateTerrainChunk() {
        if (!HeightMapsReceived) {
            return;
        }
        float viewerDistanceFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(ViewerPosition));

        bool wasVisible = IsVisible();
        bool visible = viewerDistanceFromNearestEdge <= maxViewDist;

        if (visible) {
            int lodIndex = 0;
            #region Find LOD Index
            foreach (LODInfo detailLevel in detailLevels) {
                if (viewerDistanceFromNearestEdge > detailLevel.visibleDstThreshold) {
                    lodIndex += 1;
                } else {
                    break;
                }
            }
            #endregion

            // Terrain chunk LOD change happens here
            if (lodIndex != previousLODIndex) {
                LODMesh lodMesh = lodMeshes[lodIndex];
                #region Get Water
                if (lodIndex == 0) {
                    if (waterChunk == null) {
                        waterChunk = waterChunkManager.GetWaterChunk(sampleCenter * meshSettings.meshScale);
                    }
                } else if (waterChunk != null) {
                    waterChunkManager.DisposeWaterChunk(waterChunk);
                    waterChunk = null;
                }
                #endregion

                #region Request Mesh
                if (lodMesh.hasMesh) {
                    previousLODIndex = lodIndex;
                    meshFilter.mesh = lodMesh.mesh;

                    HeightMap textureHeightMap = heightMaps["Continentalness"];
                    Texture2D chunkTexture = HeightMapUtils.TextureFromHeightMap(textureHeightMap, Color.white, Color.magenta);
                    meshRenderer.material.mainTexture = chunkTexture;
                    // TODO: colour heightmap here
                } else if (!lodMesh.hasRequestedMesh) {
                    CombineTerrainHeightMaps();
                    lodMesh.RequestMesh(combinedTerrainHeightMaps, meshSettings);
                }
                #endregion
            }
        }
        if (wasVisible != visible) {
            SetVisible(visible);
            OnVisibilityChanged?.Invoke(this, visible);
        }
    }


    private void CombineTerrainHeightMaps() {
        List<HeightMap> heightMapsToCombine = new();
        foreach (HeightMap heightMap in heightMaps.Values) {
            if (heightMap.heightMapSettings.useForTerrain) {
                heightMapsToCombine.Add(heightMap);
            }
        }
        combinedTerrainHeightMaps = HeightMapUtils.CombineHeightMaps(heightMapsToCombine);
    }



    public void UpdateCollisionMesh() {
        if (hasSetCollider) {
            return;
        }
        float sqrDstFromViewerToEdge = bounds.SqrDistance(ViewerPosition);

        if (sqrDstFromViewerToEdge < detailLevels[colliderLODIndex].SqrVisibleDstThreshold) {
            if (!lodMeshes[colliderLODIndex].hasRequestedMesh) {
                CombineTerrainHeightMaps();
                lodMeshes[colliderLODIndex].RequestMesh(combinedTerrainHeightMaps, meshSettings);
            }
        }

        if (sqrDstFromViewerToEdge < colliderGenerationDistanceThreshold * colliderGenerationDistanceThreshold) {
            if (lodMeshes[colliderLODIndex].hasMesh) {
                meshCollider.sharedMesh = lodMeshes[colliderLODIndex].mesh;
                hasSetCollider = true;
            }
        }
    }


    public void SetVisible(bool visible) {
        meshObject.SetActive(visible);
    }


    public bool IsVisible() {
        return meshObject.activeSelf;
    }


    class LODMesh {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        public readonly int LOD;
        public event Action UpdateCallback;

        public LODMesh(int lod) {
            LOD = lod;
        }


        void OnMeshDataReceived(object meshDataObject) {
            mesh = ((MeshData)meshDataObject).CreateMesh();
            hasMesh = true;
            UpdateCallback();
        }


        public void RequestMesh(float[,] heightMapValues, MeshSettings meshSettings) {
            hasRequestedMesh = true;
            ThreadedDataRequester.RequestData(
                () => MeshGenerator.GenerateTerrainMesh(heightMapValues, meshSettings, LOD),
                OnMeshDataReceived
            );
        }
    }
}
