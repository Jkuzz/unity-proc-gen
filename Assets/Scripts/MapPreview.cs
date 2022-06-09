using System.Collections.Generic;
using UnityEngine;

public class MapPreview : MonoBehaviour {

    public Renderer textureRenderer;
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;

    public enum DrawMode {NoiseMap, Mesh, FalloffMap}
    public DrawMode drawMode;

    public MeshSettings meshSettings;
    List<HeightMapSettingsSelect> heightMapSettings;
    // public TextureData textureData;

    public Material terrainMaterial;

    [Range(0,MeshSettings.numSupportedLODs - 1)]
    public int editorPreviewLOD;

    public bool autoUpdate;


    public void DrawTexture(Texture2D texture) {
        textureRenderer.sharedMaterial.mainTexture = texture;
        textureRenderer.transform.localScale = new Vector3(texture.width, 1, texture.height) / 10f;

        textureRenderer.gameObject.SetActive(true);
        meshFilter.gameObject.SetActive(false);
    }


    public void DrawMesh(MeshData meshData) {
        meshFilter.sharedMesh = meshData.CreateMesh();

        textureRenderer.gameObject.SetActive(false);
        meshFilter.gameObject.SetActive(true);
    }


    public void DrawMapInEditor() {
        heightMapSettings = GetComponent<TerrainGenerator>().heightMapSettings;

        if(drawMode == DrawMode.NoiseMap) {
            float[,] combinedTerrainHeightMap = GetCombinedHeightMaps(10);
            DrawTexture(TextureGenerator.TextureFromHeightMap(combinedTerrainHeightMap));
        } else if (drawMode == DrawMode.Mesh) {
            float[,] combinedTerrainHeightMap = GetCombinedHeightMaps();
            DrawMesh(MeshGenerator.GenerateTerrainMesh(combinedTerrainHeightMap, meshSettings, editorPreviewLOD));
        }
    }


    private float[,] GetCombinedHeightMaps(int sizeMultiplier = 1) {
        List<HeightMap> heightMapsToCombine = new();
        foreach(HeightMapSettingsSelect hmSettingsSelect in heightMapSettings) {
            if(!hmSettingsSelect.enabled || !hmSettingsSelect.heightMapSettings.useForTerrain) {
                continue;
            }
            heightMapsToCombine.Add(HeightMapGenerator.GenerateHeightMap(meshSettings.NumVerticesPerLine * sizeMultiplier, hmSettingsSelect.heightMapSettings, Vector2.zero));
        }
        return HeightMapUtils.CombineHeightMaps(heightMapsToCombine);
    }


    void OnValidate() {
        heightMapSettings = GetComponent<TerrainGenerator>().heightMapSettings;
        if(meshSettings != null) {
            meshSettings.OnValuesUpdated -= OnValuesUpdated;  // Nothing if not subscribed
            meshSettings.OnValuesUpdated += OnValuesUpdated;  // Subscribed max once
        }
        if(heightMapSettings != null) {
            foreach(HeightMapSettingsSelect heightMapSettingsSelect in heightMapSettings) {
                if(!heightMapSettingsSelect.enabled) {
                    continue;
                }
                heightMapSettingsSelect.heightMapSettings.OnValuesUpdated -= OnValuesUpdated;
                heightMapSettingsSelect.heightMapSettings.OnValuesUpdated += OnValuesUpdated;
            }
        }
        // if(textureData != null) {
        //     textureData.OnValuesUpdated -= OnValuesUpdated;
        //     textureData.OnValuesUpdated += OnValuesUpdated;
        // }
    }


    void OnValuesUpdated() {
        if(!Application.isPlaying) {
            DrawMapInEditor();
        }
    }
}
