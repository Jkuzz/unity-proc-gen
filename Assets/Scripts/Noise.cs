using System;
using UnityEngine;

public static class Noise {

    public enum NormaliseMode { Local, Global };


    public static float[,] GenerateNoiseMap(int mapSize, NoiseSettings settings, Vector2 sampleCentre) {
        float[,] noiseMap = new float[mapSize, mapSize];

        System.Random prng = new(settings.seed);
        Vector2[] octaveOffsets = new Vector2[settings.octaves];

        float maxPossibleHeight = 0;
        float amplitude = 1;

        for (int octave = 0; octave < settings.octaves; octave++) {
            float offsetX = prng.Next(-100000, 100000) + settings.offset.x + sampleCentre.x;
            float offsetY = prng.Next(-100000, 100000) - settings.offset.y - sampleCentre.y;
            octaveOffsets[octave] = new Vector2(offsetX, offsetY);

            maxPossibleHeight += amplitude;
            amplitude *= settings.persistence;
        }

        settings.scale = Math.Max(settings.scale, 0.1f);

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;

        float halfSize = mapSize / 2f;

        for (int y = 0; y < mapSize; y++) {
            for (int x = 0; x < mapSize; x++) {

                amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;

                for (int octave = 0; octave < settings.octaves; octave++) {
                    float sampleX = (x - halfSize + octaveOffsets[octave].x) / settings.scale * frequency;
                    float sampleY = (y - halfSize + octaveOffsets[octave].y) / settings.scale * frequency;

                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= settings.persistence;
                    frequency *= settings.lacunarity;
                }
                maxLocalNoiseHeight = Math.Max(maxLocalNoiseHeight, noiseHeight);
                minLocalNoiseHeight = Math.Min(minLocalNoiseHeight, noiseHeight);

                noiseMap[x, y] = noiseHeight;

                if(settings.normaliseMode == NormaliseMode.Global) {
                    float normalisedHeight = (noiseMap[x, y] + 1) / (maxPossibleHeight / 0.85f);
                    noiseMap[x, y] = Math.Max(normalisedHeight, 0.05f);
                }
            }
        }

        // Normalise noise map if local
        if (settings.normaliseMode == NormaliseMode.Local) {
            for (int y = 0; y < mapSize; y++) {
                for (int x = 0; x < mapSize; x++) {
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);
                }
            }
        }
        return noiseMap;
    }
}


[Serializable]
public class NoiseSettings {
    public Noise.NormaliseMode normaliseMode;

    public float scale = 50;
    [Range(1, 8)]
    public int octaves = 6;
    [Range(0, 1)]
    public float persistence = 0.4f;
    public float lacunarity = 2;

    public int seed;
    public Vector2 offset;

    public void ValidateValues() {
        scale = Mathf.Max(scale, 0.01f);
        octaves = Mathf.Max(octaves, 1);
        lacunarity = Mathf.Max(lacunarity, 1);
        persistence = Mathf.Clamp01(persistence);
    }
}