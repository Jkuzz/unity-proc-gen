using System;
using System.Collections.Generic;
using UnityEngine;


public static class HeightMapGenerator {

    public static HeightMap GenerateHeightMap(int size, List<HeightMapSettingsSelect> settingsList, Vector2 sampleCentre) {

        // Total noise of all noiseMaps scaled by their height multiplier
        float[,] totalHeightMap = new float[size, size];

        foreach (HeightMapSettingsSelect settingsSelection in settingsList) {
            if (!settingsSelection.enabled) {
                continue;
            }

            HeightMapSettings settings = settingsSelection.heightMapSettings;

            float[,] values = Noise.GenerateNoiseMap(size, settings.noiseSettings, sampleCentre);
            AnimationCurve heightCurve_threadsafe = new(settings.heightCurve.keys);
            for (int i = 0; i < size; i++) {
                for (int j = 0; j < size; j++) {
                    totalHeightMap[i, j] += heightCurve_threadsafe.Evaluate(values[i, j]) * settings.heightMultiplier;
                }
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
        return new HeightMap(totalHeightMap, minValue, maxValue);
    }
}


public struct HeightMap {
    public readonly float[,] values;
    public readonly float minValue;
    public readonly float maxValue;

    public HeightMap(float[,] values, float minValue, float maxValue) {
        this.values = values;
        this.minValue = minValue;
        this.maxValue = maxValue;
    }
}