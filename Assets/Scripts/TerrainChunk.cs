using System;
using System.Collections.Generic;
using UnityEngine;

public class TerrainChunk {

    const float colliderGenerationDistanceThreshold = 5;

    public event Action<TerrainChunk, bool> OnVisibilityChanged;
    public Vector2 coord;

    GameObject meshObject;
    Vector2 sampleCenter;
    Bounds bounds;

    GameObject waterChunk;
    WaterChunkManager waterChunkManager;

    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    MeshCollider meshCollider;

    LODInfo[] detailLevels;
    LODMesh[] lodMeshes;
    int colliderLODIndex;

    HeightMap heightMap;
    bool heightMapReceived;
    bool hasSetCollider;
    int previousLODIndex = -1;
    float maxViewDist;

    List<HeightMapSettingsSelect> heightMapSettings;
    Dictionary<string, NoiseMap> noiseMaps;
    MeshSettings meshSettings;
    Transform viewer;


    public TerrainChunk(Vector2 coord, List<HeightMapSettingsSelect> heightMapSettings, MeshSettings meshSettings, LODInfo[] detailLevels, int colliderLODIndex, Transform parent, Material material, Transform viewer, WaterChunkManager waterChunkManager) {
        this.detailLevels = detailLevels;
        this.colliderLODIndex = colliderLODIndex;
        this.coord = coord;
        this.heightMapSettings = heightMapSettings;
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
        if(chunkParent == null) {
            GameObject newContainer = new("Chunk Container");
            newContainer.transform.parent = parent;
            chunkParent =  newContainer.transform;
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
        ThreadedDataRequester.RequestData(
            () => HeightMapGenerator.GenerateHeightMap(meshSettings.NumVerticesPerLine, heightMapSettings, sampleCenter),
            OnHeightMapReceived
        );
    }


    void OnHeightMapReceived(object heightMapObject) {
        heightMap = (HeightMap)heightMapObject;
        heightMapReceived = true;

        // meshRenderer.material.SetTexture();

        UpdateTerrainChunk();
    }


    Vector2 ViewerPosition => new(viewer.position.x / meshSettings.meshScale, viewer.position.z / meshSettings.meshScale);


    public void UpdateTerrainChunk() {
        if (!heightMapReceived) {
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
                } else if (!lodMesh.hasRequestedMesh) {
                    lodMesh.RequestMesh(heightMap, meshSettings);
                }
                #endregion
            }
        }
        if (wasVisible != visible) {
            SetVisible(visible);
            OnVisibilityChanged?.Invoke(this, visible);
        }
    }


    public void UpdateCollisionMesh() {
        if (hasSetCollider) {
            return;
        }
        float sqrDstFromViewerToEdge = bounds.SqrDistance(ViewerPosition);

        if (sqrDstFromViewerToEdge < detailLevels[colliderLODIndex].SqrVisibleDstThreshold) {
            if (!lodMeshes[colliderLODIndex].hasRequestedMesh) {
                lodMeshes[colliderLODIndex].RequestMesh(heightMap, meshSettings);
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


        public void RequestMesh(HeightMap heightMap, MeshSettings meshSettings) {
            hasRequestedMesh = true;
            ThreadedDataRequester.RequestData(
                () => MeshGenerator.GenerateTerrainMesh(heightMap.values, meshSettings, LOD),
                OnMeshDataReceived
            );
        }
    }
}
