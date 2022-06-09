using System.Collections.Generic;
using UnityEngine;

public static class PoissonDiscSampling {


    public static List<Vector2> GeneratePoints(SampleType sampleType, Vector2 sampleRegionSize, float[,] heightMap) {

        float cellSize = sampleType.radiusMin / Mathf.Sqrt(2);
        int cols = Mathf.CeilToInt(sampleRegionSize.x / cellSize);
        int rows = Mathf.CeilToInt(sampleRegionSize.y / cellSize);

        // Background grid used to only validate nearby samples for efficiency
        List<int>[,] grid = new List<int>[cols, rows];

        // This would be full only in perfect packing, which is almost impossibly improbable
        sampleType.sampleFullness = Mathf.Clamp01(sampleType.sampleFullness) / 2;
        int maxPointsCount = Mathf.FloorToInt(rows * cols * sampleType.sampleFullness);

        // Factor to transform sampling region into heightmap coordinates
        Vector2 coordsTransformScale = new(heightMap.GetLength(0) / sampleRegionSize.x, heightMap.GetLength(1) / sampleRegionSize.y);

        // Output list of properly spawned points
        List<Vector2> points = new();
        //Active points from which spawning may be initiated
        List<Vector2> spawnPoints = new() {
            new(Random.Range(0, sampleRegionSize.x),Random.Range(0, sampleRegionSize.y))
        };

        int pointsSpawned = 0;

        while (spawnPoints.Count > 0) {
            int spawnIndex = Random.Range(0, spawnPoints.Count);
            Vector2 spawnCentre = spawnPoints[spawnIndex];
            float pointSpawnRaidus = GetPointRadiusFromHeightMap(sampleType.radiusCurve, sampleType.radiusMin, spawnCentre, coordsTransformScale, heightMap);

            bool candidateAccepted = false;

            for (int i = 0; i < sampleType.rejectionSamples; i++) {
                Vector2 dir = Random.insideUnitCircle.normalized;
                Vector2 candidate = spawnCentre + dir * Random.Range(pointSpawnRaidus, 2 * pointSpawnRaidus);

                int candidateCellX = Mathf.CeilToInt(candidate.x / cellSize);
                int candidateCellY = Mathf.CeilToInt(candidate.y / cellSize);

                if (IsValid(candidate, cellSize, sampleRegionSize, pointSpawnRaidus, points, grid)) {
                    points.Add(candidate);
                    spawnPoints.Add(candidate);
                    AddPointToGridSquares(grid, candidateCellX, candidateCellY, cellSize, pointSpawnRaidus, points.Count);
                    candidateAccepted = true;
                    break;
                }
            }
            if (candidateAccepted) {
                pointsSpawned += 1;
            } else {  // Active point failed to spawn valid candidate => surrounding are must be too full
                spawnPoints.RemoveAt(spawnIndex);
            }

            if (pointsSpawned >= maxPointsCount) {
                break;
            }
        }
        return ValidatePointsWithHeightMap(points, coordsTransformScale, heightMap, sampleType.radiusCurve, sampleType.radiusMax);
    }


    // Use provided heightmap, animation curve and max radius to cull points that are not packe sufficiently densely
    private static List<Vector2> ValidatePointsWithHeightMap(List<Vector2> points, Vector2 coordsTransformScale, float[,] heightMap, AnimationCurve densityCurve, float radiusMax) {

        for (int i = points.Count - 1; i >= 0; i--) {
            Vector2 point = points[i];
            float heightmapValue = GetPointHeightMapValue(point, coordsTransformScale, heightMap);
            if (densityCurve.Evaluate(heightmapValue) > radiusMax) {
                points.RemoveAt(i);
            }
        }
        return points;
    }


    // Transform spawn-space position to heightmap position and return the heightmap value
    private static float GetPointHeightMapValue(Vector2 point, Vector2 coordsTransformScale, float[,] heightMap) {
        int transformedX = Mathf.FloorToInt(point.x * coordsTransformScale.x);
        int transformedY = Mathf.FloorToInt(point.y * coordsTransformScale.y);
        return heightMap[transformedX, transformedY];
    }


    // Read heightmap and determine radius based on heightmap and radus curve
    private static float GetPointRadiusFromHeightMap(AnimationCurve radiusCurve, float radiusMin, Vector2 point, Vector2 coordsTransformScale, float[,] heightMap) {
        float heightMapValue = GetPointHeightMapValue(point, coordsTransformScale, heightMap);
        return Mathf.Max(radiusCurve.Evaluate(heightMapValue), radiusMin);
    }


    // Add new spawn to all grid cells with which it could potentially intersect based on its radius
    private static void AddPointToGridSquares(List<int>[,] grid, int cellX, int cellY, float cellSize, float radius, int pointIndex) {

        int maxCellOffset = Mathf.CeilToInt(radius / cellSize);

        int searchStartX = Mathf.Max(cellX - maxCellOffset, 0);
        int searchEndX = Mathf.Min(cellX + maxCellOffset, grid.GetLength(0) - 1);
        int searchStartY = Mathf.Max(cellY - maxCellOffset, 0);
        int searchEndY = Mathf.Min(cellY + maxCellOffset, grid.GetLength(1) - 1);

        for (int x = searchStartX; x <= searchEndX; x++) {
            for (int y = searchStartY; y <= searchEndY; y++) {
                if (grid[x, y] == null) {
                    grid[x, y] = new();
                }
                grid[x, y].Add(pointIndex - 1);
            }
        }
    }


    // Check all points which have recorded themselves on the candidate's grid cell as potential conflicts
    // If none conflict, the candidate is in a valid position
    private static bool IsValid(Vector2 candidate, float cellSize, Vector2 sampleRegionSize, float radius, List<Vector2> points, List<int>[,] pointGridSquare) {

        // Candidate is out of bounds
        if (candidate.x < 0 || candidate.x >= sampleRegionSize.x || candidate.y < 0 || candidate.y >= sampleRegionSize.y) {
            return false;
        }

        int candidateCellX = Mathf.FloorToInt(candidate.x / cellSize);
        int candidateCellY = Mathf.FloorToInt(candidate.y / cellSize);

        if (pointGridSquare[candidateCellX, candidateCellY] == null) {
            return true;
        }

        foreach (int pointToCheck in pointGridSquare[candidateCellX, candidateCellY]) {
            float sqrDistance = (candidate - points[pointToCheck]).sqrMagnitude;
            if (sqrDistance < radius * radius) {
                return false;
            }
        }
        return true;
    }
}
