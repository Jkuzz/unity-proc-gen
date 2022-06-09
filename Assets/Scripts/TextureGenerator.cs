using UnityEngine;

public static class TextureGenerator {


    public static Texture2D TextureFromColourMap(Color[] colourMap, int width, int height) {
        Texture2D texture = new(width, height) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(colourMap);
        texture.Apply();
        return texture;
    }


    public static Texture2D TextureFromHeightMap(float[,] heightMap) {
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);

        float minValue = float.MaxValue;
        float maxValue = float.MinValue;
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                minValue = Mathf.Min(minValue, heightMap[x, y]);
                maxValue = Mathf.Max(maxValue, heightMap[x, y]);
            }
        }

        Color[] colourMap = new Color[width * height];
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                colourMap[y * width + x] = Color.Lerp(Color.black, Color.white, Mathf.InverseLerp(minValue, maxValue, heightMap[x, y]));
            }
        }
        return TextureFromColourMap(colourMap, width, height);
    }
}
