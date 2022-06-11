using System;
using System.Collections.Generic;
using UnityEngine;

public class TerrainChunk {

    const float colliderGenerationDistanceThreshold = 10;

    public event Action<TerrainChunk, bool> OnVisibilityChanged;
    // Coordinate of chunk in chunk space - origin is (0, 0), around it (0, 1)...
    public Vector2 coord;

    readonly GameObject meshObject;

    // Centre of the noise sample
    Vector2 noiseSampleCenter;
    // Centre of the mesh in actual world coordinates
    public readonly Vector2 meshWorldCentre;
    Bounds bounds;

    GameObject waterChunk;

    readonly WaterChunkManager waterChunkManager;
    readonly PoissonManager poissonManager;
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

    readonly List<HeightMapSettings> heightMapSettingsList;
    // height maps stored in dictionary to allow easily fetching specific map
    readonly Dictionary<string, HeightMap> heightMaps = new();
    float[,] combinedTerrainHeightMaps;
    readonly MeshSettings meshSettings;
    readonly Transform viewer;

    volatile int heightMapsRequested = 0;
    volatile int heightMapsReceived = 0;
    bool hasSpawnedObjects = false;


    public TerrainChunk(Vector2 coord, List<HeightMapSettings> heightMapSettingsList, MeshSettings meshSettings, LODInfo[] detailLevels, int colliderLODIndex, Transform parent, Material material, Transform viewer, WaterChunkManager waterChunkManager, PoissonManager poissonManager) {
        this.detailLevels = detailLevels;
        this.colliderLODIndex = colliderLODIndex;
        this.coord = coord;
        this.heightMapSettingsList = heightMapSettingsList;
        this.meshSettings = meshSettings;
        this.viewer = viewer;
        this.waterChunkManager = waterChunkManager;
        this.poissonManager = poissonManager;

        noiseSampleCenter = coord * meshSettings.MeshWorldSize / meshSettings.meshScale;
        meshWorldCentre = coord * meshSettings.MeshWorldSize;
        bounds = new(meshWorldCentre, Vector2.one * meshSettings.MeshWorldSize);

        meshObject = new GameObject("Terrain Chunk");
        meshRenderer = meshObject.AddComponent<MeshRenderer>();
        meshFilter = meshObject.AddComponent<MeshFilter>();
        meshCollider = meshObject.AddComponent<MeshCollider>();

        meshRenderer.material = material;
        meshObject.transform.position = new Vector3(meshWorldCentre.x, 0, meshWorldCentre.y);

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
        foreach (HeightMapSettings settings in heightMapSettingsList) {
            ThreadedDataRequester.RequestData(
                () => HeightMapGenerator.GenerateHeightMap(meshSettings.NumVerticesPerLine, settings, noiseSampleCenter),
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

    Vector2 ViewerPosition => new(viewer.position.x, viewer.position.z);


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
                        waterChunk = waterChunkManager.GetWaterChunk(meshWorldCentre);
                    }
                } else if (waterChunk != null) {
                    waterChunkManager.DisposeWaterChunk(waterChunk);
                    waterChunk = null;
                }
                #endregion

                #region Update/Request Mesh
                if (lodMesh.hasMesh) {
                    previousLODIndex = lodIndex;
                    meshFilter.mesh = lodMesh.mesh;

                    HeightMap textureHeightMap = heightMaps["Continentalness"];
                    Texture2D chunkTexture = HeightMapUtils.TextureFromHeightMap(textureHeightMap, Color.white, Color.magenta);
                    meshRenderer.material.mainTexture = chunkTexture;

                    if (!hasSpawnedObjects && lodIndex == 0) {
                        Debug.Log("Spawning objects for chunk " + meshWorldCentre);
                        Dictionary<PoissonSampleType, List<Vector2>> generatedPoints = poissonManager.GeneratePoints(heightMaps["Forestyness"].values, Vector2.one * meshSettings.MeshWorldSize);

                        // terrain heightmap should have been combined by now, see below
                        poissonManager.SpawnObjects(generatedPoints, meshWorldCentre, combinedTerrainHeightMaps, meshSettings.MeshWorldSize);
                        hasSpawnedObjects = true;
                    }
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

        // TODO: Fix issue when spawning in middle of chunk, collider is not set

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
