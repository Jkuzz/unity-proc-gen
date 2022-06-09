using UnityEngine;

[CreateAssetMenu()]
public class HeightMapSettings : UpdatableData {

    public NoiseSettings noiseSettings;
    public bool useForTerrain;
    public float heightMultiplier;
    public AnimationCurve heightCurve;
    public string mapName;

    #if UNITY_EDITOR
    public float minHeight {
        get {
            return heightMultiplier * heightCurve.Evaluate(0);
        }
    }

    public float maxHeight {
        get {
            return heightMultiplier * heightCurve.Evaluate(1);
        }
    }

    protected override void OnValidate() {
    noiseSettings.ValidateValues();
        base.OnValidate();
    }
    #endif
}
