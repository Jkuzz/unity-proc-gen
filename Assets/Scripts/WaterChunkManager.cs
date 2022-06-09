using System.Collections.Generic;
using UnityEngine;

public class WaterChunkManager : MonoBehaviour {

    static float meshSize;

    public GameObject waterPrototype;
    public float waterLevel;

    private readonly Queue<GameObject> inactiveWaterChunks = new();


    private void Awake() {
        TerrainGenerator terrainGenerator = GetComponentInParent<TerrainGenerator>();
        meshSize = terrainGenerator.meshSettings.MeshWorldSize;
    }


    void Start() {
        // Transform prototype to unit size to avoid weird scaling behaviour
        waterPrototype.transform.localScale = Vector3.one;
        Vector3 waterPrototypeSize = waterPrototype.GetComponent<Renderer>().bounds.size;
        // Scale prototype to chunk size
        waterPrototype.transform.localScale = new(meshSize / waterPrototypeSize.x, 1, meshSize / waterPrototypeSize.z);
        waterPrototype.transform.localPosition = new(0, waterLevel, 0);
        // Store prototype to be available for chunk requests
        DisposeWaterChunk(waterPrototype);
    }


    public void DisposeWaterChunk(GameObject waterToDeactivate) {
        waterToDeactivate.SetActive(false);
        inactiveWaterChunks.Enqueue(waterToDeactivate);
    }


    public GameObject GetWaterChunk(Vector2 waterPosition) {
        GameObject waterToReturn;
        Vector3 worldWaterPosition = new(waterPosition.x, waterLevel, waterPosition.y);

        // If there are pooled inactive water chunks, return one of them
        if (inactiveWaterChunks.Count > 0) {
            waterToReturn = inactiveWaterChunks.Dequeue();
            waterToReturn.transform.position = worldWaterPosition;
        } else {
            // If there are no pooled inactive water chunks, instantiate new chunk from prototype
            waterToReturn = Instantiate(waterPrototype, worldWaterPosition, Quaternion.identity, transform);
        }
        waterToReturn.SetActive(true);
        return waterToReturn;
    }
}
