using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

public class RockBandGenerator : MonoBehaviour
{
    [SerializeField] private SpriteShapeController shapeTemplate;
    [SerializeField] private ScriptableObject      rockProfile;
    [SerializeField, Range(1, 16)] private int            rockBandCount         = 4;
    [SerializeField]               private float          rockBandSpacing       = 0.3f;
    [SerializeField]               private Gradient       rockGradient;
    // X=0 is the top band, X=1 is the bottom band. Y is the uniform scale applied to that band.
    [SerializeField]               private AnimationCurve rockScaleCurve        = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [SerializeField]               private float          rockFillPixelsPerUnit = 100f;
    [SerializeField, Range(0f, 3f)]  private float        rockEdgeNoise         = 0.08f;
    [SerializeField, Range(1f, 32f)] private float        rockNoiseFrequency    = 12f;
    [SerializeField, Range(0f, 1f)]  private float        rockSharpness         = 0.35f;

    private readonly List<GameObject> _rockBands = new List<GameObject>();

    public void Build(IReadOnlyList<List<Vector3>> islandPerimeters, IReadOnlyList<GameObject> islandGOs, int seed)
    {
        for (int i = 0; i < islandPerimeters.Count; i++)
        {
            GameObject islandGO = i < islandGOs.Count ? islandGOs[i] : null;
            BuildRocksForIsland(islandPerimeters[i], islandGO, i, seed);
        }
    }

    // Rebuilds only the rock bands, reusing the same perimeter data.
    public void Rebuild(IReadOnlyList<List<Vector3>> islandPerimeters, IReadOnlyList<GameObject> islandGOs, int seed)
    {
        Clear();
        Build(islandPerimeters, islandGOs, seed);
    }

    public void Clear()
    {
        foreach (var go in _rockBands)
            if (go != null) Destroy(go);
        _rockBands.Clear();
    }

    // -------------------------------------------------------------------------
    // Rock band spawning
    // -------------------------------------------------------------------------

    private void BuildRocksForIsland(List<Vector3> detailPoints, GameObject grassGO, int islandIdx, int seed)
    {
        if (shapeTemplate == null || detailPoints == null || detailPoints.Count < 2) return;

        var grassRenderer = grassGO != null ? grassGO.GetComponent<SpriteShapeRenderer>() : null;
        int baseOrder     = grassRenderer != null ? grassRenderer.sortingOrder : 0;

        for (int i = 0; i < rockBandCount; i++)
        {
            float t         = rockBandCount > 1 ? i / (float)(rockBandCount - 1) : 0f;
            Color bandColor = rockGradient.Evaluate(t);
            float bandScale = rockScaleCurve.Evaluate(t);

            GameObject bandGO = Instantiate(shapeTemplate.gameObject, transform);
            bandGO.name = $"Island_{islandIdx}_RockBand_{i}";
            bandGO.transform.localPosition = Vector3.down * (i + 1) * rockBandSpacing;
            bandGO.transform.localRotation = shapeTemplate.transform.localRotation;
            bandGO.transform.localScale    = shapeTemplate.transform.localScale;

            var ssc = bandGO.GetComponent<SpriteShapeController>();
            if (ssc != null)
            {
                if (rockProfile != null)
                {
                    var prop = ssc.GetType().GetProperty("spriteShape",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    prop?.SetValue(ssc, rockProfile);
                }
                ssc.fillPixelsPerUnit = rockFillPixelsPerUnit;
                // XOR island index into seed so each island's rocks look distinct.
                FillRockSpline(ssc, detailPoints, seed ^ (islandIdx * 31337) ^ (i * 7919), bandScale);
            }

            var ssr = bandGO.GetComponent<SpriteShapeRenderer>();
            if (ssr != null)
            {
                ssr.enabled      = true;
                ssr.color        = bandColor;
                ssr.sortingOrder = baseOrder - (i + 1);
            }

            _rockBands.Add(bandGO);
        }
    }

    // -------------------------------------------------------------------------
    // Spline helper
    // -------------------------------------------------------------------------

    private void FillRockSpline(SpriteShapeController ssc, List<Vector3> sourcePoints, int seed, float scale = 1f)
    {
        int       count  = sourcePoints.Count;
        Transform t      = ssc.transform;
        Spline    spline = ssc.spline;

        Vector3 centroid = Vector3.zero;
        foreach (var p in sourcePoints) centroid += p;
        centroid /= count;

        float totalLen = 0f;
        for (int i = 0; i < count; i++)
            totalLen += Vector3.Distance(sourcePoints[i], sourcePoints[(i + 1) % count]);
        float avgSeg = totalLen / count;

        float seedOffset = (seed % 1000) * 0.001f;
        var   rng        = new System.Random(seed);
        var   points     = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            Vector3 p       = sourcePoints[i];
            p               = centroid + (p - centroid) * scale;
            // Outward from island centroid, not world origin.
            Vector3 outward = new Vector3(p.x - centroid.x, 0f, p.z - centroid.z).normalized;
            float   u       = i / (float)count * rockNoiseFrequency + seedOffset;
            float   push    = (Mathf.PerlinNoise(u, seedOffset * 3.7f) * 2f - 1f) * rockEdgeNoise * avgSeg;
            points.Add(p + outward * push);
        }

        spline.Clear();
        for (int i = 0; i < count; i++)
        {
            Vector3 local = t.InverseTransformPoint(points[i]);
            spline.InsertPointAt(i, local);
            bool sharp = rng.NextDouble() < rockSharpness;
            spline.SetTangentMode(i, sharp ? ShapeTangentMode.Broken : ShapeTangentMode.Continuous);
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 prev  = points[(i - 1 + count) % count];
            Vector3 curr  = points[i];
            Vector3 next  = points[(i + 1) % count];
            Vector3 chord = next - prev;
            float   mag   = (Vector3.Distance(prev, curr) + Vector3.Distance(curr, next)) * 0.25f;

            var mode = spline.GetTangentMode(i);
            if (mode == ShapeTangentMode.Broken)
            {
                float leftMag  = Vector3.Distance(prev, curr) * 0.1f;
                float rightMag = Vector3.Distance(curr, next) * 0.1f;
                spline.SetLeftTangent( i, t.InverseTransformVector((prev - curr).normalized * leftMag));
                spline.SetRightTangent(i, t.InverseTransformVector((next - curr).normalized * rightMag));
            }
            else
            {
                Vector3 tangent = t.InverseTransformVector(chord.normalized * (mag * 0.6f));
                spline.SetRightTangent(i, tangent);
            }
        }

        spline.isOpenEnded = false;
    }
}
