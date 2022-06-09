using System.Collections.Generic;
using UnityEngine;

public class NoiseMap {

    readonly float[,] noise;
    private readonly int noiseSize;
    public readonly NoiseSettings settings;
    public readonly string name;


    public NoiseMap(int noiseSize, Vector2 noiseCenter, NoiseSettings settings, string name) {
        this.noise = Noise.GenerateNoiseMap(noiseSize, settings, noiseCenter);
        this.noiseSize = noiseSize;
        this.settings = settings;
        this.name = name;
    }
}
