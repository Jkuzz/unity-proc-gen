using System.Collections.Generic;
using UnityEngine;

public class PoissonViewer : MonoBehaviour {

    public Vector2 regionSize = Vector2.one;
    public NoiseSettings noiseSettings;
    public int noiseMapSize;

    // Plane onto which noise preview is drawn
    public Transform previewPlane;
    public Color heightMapColourLow = Color.black;
    public Color heightMapColourHigh = Color.white;

    // Types of objects to spawn
    public List<PoissonSampleType> sampleTypes;

    // Spawned object 2D positions
    readonly Dictionary<PoissonSampleType, List<Vector2>> points = new();
    // To not generate new points when old ones still valid
    bool gizmosValid = false;


    private void OnValidate() {
        // noiseScale = Mathf.Max(0.1f, noiseScale);

        float[,] heightMap = Noise.GenerateNoiseMap(noiseMapSize, noiseSettings, Vector3.zero);
        ShowNoisePreview(heightMap);

        points.Clear();
        foreach (PoissonSampleType sampleType in sampleTypes) {
            sampleType.Validate();
            points.Add(sampleType, PoissonDiscSampling.GeneratePoints(sampleType, regionSize, heightMap));
        }
        gizmosValid = false;
    }


    private void OnDrawGizmos() {
        if (gizmosValid) {
            return;
        }
        Gizmos.DrawWireCube(new(0, 200, 0), new Vector3(regionSize.x, 0, regionSize.y));

        if (points == null) {
            Debug.LogWarning("Points are null, not drawing gizmos!");
            return;
        }

        foreach (KeyValuePair<PoissonSampleType, List<Vector2>> entry in points) {
            Gizmos.color = entry.Key.gizmoColour;
            foreach (Vector2 point in entry.Value) {
                Gizmos.DrawSphere(new Vector3(point.x - regionSize.x / 2, 200, point.y - regionSize.y / 2), entry.Key.gizmoRadius);
            }
        }
    }


    private void Start() {
        SpawnObjects(points);
    }


    public void SpawnObjects(Dictionary<PoissonSampleType, List<Vector2>> objectSpawnPoints) {
        GameObject containerParent = GameObject.Find("SpawnedObjectsContainer");
        if (containerParent == null) {
            containerParent = new("SpawnedObjectsContainer");
            containerParent.transform.parent = transform;
        }

        foreach (KeyValuePair<PoissonSampleType, List<Vector2>> entry in objectSpawnPoints) {
            if (entry.Key.variants == null) {
                Debug.LogWarning("SampleType " + entry.Key.typeName + " has no registered variant, skipping!");
                continue;
            }

            GameObject spawnContainer = new("ObjectContainer[" + entry.Key.typeName + "]");
            spawnContainer.transform.parent = containerParent.transform;
            int ignoreSpawnLayer = LayerMask.NameToLayer("Ignore Object Spawning");

            foreach (Vector2 point in entry.Value) {
                Debug.Log("Attempting to spawn at " + point);
                GameObject randomSampleModel = entry.Key.variants[Random.Range(0, entry.Key.variants.Count)];

                Vector2 spawnRotation = Random.insideUnitCircle.normalized;
                Vector3 spawnOrientation = new(spawnRotation.x, 0, spawnRotation.y);
                Vector3 objectPosition = FindHighestMeshForSpawn(point - regionSize / 2, ignoreSpawnLayer);
                if (objectPosition == Vector3.zero) {
                    continue;
                }

                GameObject spawnedObject = Instantiate(
                    randomSampleModel,
                    objectPosition,
                    Quaternion.LookRotation(spawnOrientation, Vector3.up),
                    spawnContainer.transform
                );
                spawnedObject.layer = ignoreSpawnLayer;
                spawnedObject.transform.localScale = Vector3.one * Random.Range(1, 1.4f);
            }
        }
    }



    // Raycast from a high position and find the highest mesh to spawn object on
    public Vector3 FindHighestMeshForSpawn(Vector2 location, int ignoreSpawnLayer) {
        Vector3 positionAboveTerrain = new(location.x, 1000, location.y);
        if (Physics.Raycast(positionAboveTerrain, Vector3.down, out RaycastHit hit, Mathf.Infinity, ~(1 << ignoreSpawnLayer))) {

            // Do not spawn in water
            // TODO: Change this to work for some spawnable objects
            if (hit.transform.gameObject.layer == LayerMask.NameToLayer("Water")) {
                Debug.Log("Raycast water");
                return Vector3.zero;
            }
            Debug.Log("Raycast hit!");
            return hit.point;
        }
        Debug.Log("Raycast missed");
        return Vector3.zero;
    }


    public void ShowNoisePreview(float[,] heightMap) {
        Renderer renderer = previewPlane.GetComponent<Renderer>();

        Texture2D heightMapTexture = HeightMapUtils.TextureFromHeightMap(heightMap, noiseMapSize, heightMapColourLow, heightMapColourHigh, 0, 1);
        renderer.sharedMaterial.mainTexture = heightMapTexture;
        previewPlane.localScale = new(regionSize.x / 10, 1, regionSize.y / 10);
    }
}
