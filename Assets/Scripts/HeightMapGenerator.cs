using System;
using System.Collections.Generic;
using UnityEngine;


public static class HeightMapGenerator {

    public static HeightMap GenerateHeightMap(int size, HeightMapSettings heightMapSettings, Vector2 sampleCentre) {

        // Total noise of all noiseMaps scaled by their height multiplier
        float[,] totalHeightMap = new float[size, size];

        float[,] values = Noise.GenerateNoiseMap(size, heightMapSettings.noiseSettings, sampleCentre);
        AnimationCurve heightCurve_threadsafe = new(heightMapSettings.heightCurve.keys);
        for (int i = 0; i < size; i++) {
            for (int j = 0; j < size; j++) {
                totalHeightMap[i, j] += heightCurve_threadsafe.Evaluate(values[i, j]) * heightMapSettings.heightMultiplier;
            }
        }

        float minValue = float.MaxValue;
        float maxValue = float.MinValue;

        for (int i = 0; i < size; i++) {
            for (int j = 0; j < size; j++) {
                maxValue = Math.Max(maxValue, totalHeightMap[i, j]);
                minValue = Math.Min(minValue, totalHeightMap[i, j]);
            }
        }
        return new HeightMap(totalHeightMap, minValue, maxValue, heightMapSettings);
    }
}


public struct HeightMap {
    public readonly float[,] values;
    public HeightMapSettings heightMapSettings;
    public readonly float minValue;
    public readonly float maxValue;

    public HeightMap(float[,] values, float minValue, float maxValue, HeightMapSettings heightMapSettings) {
        this.values = values;
        this.minValue = minValue;
        this.maxValue = maxValue;
        this.heightMapSettings = heightMapSettings;
    }
}