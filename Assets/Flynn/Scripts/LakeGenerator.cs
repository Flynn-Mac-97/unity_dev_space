using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

public class LakeGenerator : MonoBehaviour
{
    [SerializeField] private SpriteShapeController waterSpriteShape;
    [SerializeField] private ShapeTangentMode       tangentMode = ShapeTangentMode.Continuous;

    private readonly List<GameObject>    _spawnedWater          = new List<GameObject>();
    private readonly List<List<Vector3>> _lakePerimeterVertices = new List<List<Vector3>>();

    public IReadOnlyList<List<Vector3>> LakePerimeterVertices => _lakePerimeterVertices;

    // Accepts the pre-computed corner perimeters from IslandGeneratorTwo.
    public void Build(IReadOnlyList<List<Vector3>> lakePerimeters, Transform parent)
    {
        _lakePerimeterVertices.Clear();

        if (waterSpriteShape == null)
        {
            Debug.LogWarning("LakeGenerator: waterSpriteShape is not assigned.");
            return;
        }

        // Hide template so it doesn't render an empty spline.
        var r = waterSpriteShape.GetComponent<SpriteShapeRenderer>();
        if (r != null) r.enabled = false;

        for (int i = 0; i < lakePerimeters.Count; i++)
        {
            _lakePerimeterVertices.Add(lakePerimeters[i]);
            SpawnWaterShape(lakePerimeters[i], i);
        }
    }

    public void Clear()
    {
        foreach (var go in _spawnedWater)
            if (go != null) Destroy(go);
        _spawnedWater.Clear();
        _lakePerimeterVertices.Clear();
    }

    // -------------------------------------------------------------------------
    // Spawning
    // -------------------------------------------------------------------------

    private void SpawnWaterShape(List<Vector3> detailPoints, int waterIdx)
    {
        if (detailPoints.Count < 2) return;

        GameObject go = Instantiate(waterSpriteShape.gameObject, transform);
        go.name = $"Water_{waterIdx}";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = waterSpriteShape.transform.localRotation;
        go.transform.localScale    = waterSpriteShape.transform.localScale;

        var r = go.GetComponent<SpriteShapeRenderer>();
        if (r != null) r.enabled = true;

        var ssc = go.GetComponent<SpriteShapeController>();
        if (ssc != null) FillSpline(ssc, detailPoints);

        _spawnedWater.Add(go);
    }

    // -------------------------------------------------------------------------
    // Spline helper
    // -------------------------------------------------------------------------

    private void FillSpline(SpriteShapeController ssc, List<Vector3> points)
    {
        int       count  = points.Count;
        Transform t      = ssc.transform;
        Spline    spline = ssc.spline;
        spline.Clear();

        for (int i = 0; i < count; i++)
        {
            Vector3 local = t.InverseTransformPoint(points[i]);
            spline.InsertPointAt(i, local);
            spline.SetTangentMode(i, tangentMode);
        }

        if (tangentMode == ShapeTangentMode.Continuous || tangentMode == ShapeTangentMode.Broken)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 prev    = points[(i - 1 + count) % count];
                Vector3 curr    = points[i];
                Vector3 next    = points[(i + 1) % count];
                Vector3 chord   = next - prev;
                float   mag     = (Vector3.Distance(prev, curr) + Vector3.Distance(curr, next)) * 0.25f;
                Vector3 tangent = t.InverseTransformVector(chord.normalized * mag);
                spline.SetRightTangent(i, tangent);
            }
        }

        spline.isOpenEnded = false;
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------

    private void OnDrawGizmos()
    {
        if (_lakePerimeterVertices == null) return;

        for (int i = 0; i < _lakePerimeterVertices.Count; i++)
        {
            var verts = _lakePerimeterVertices[i];
            if (verts == null || verts.Count < 2) continue;

            Gizmos.color = new Color(0.2f, 0.6f, 1f);
            for (int v = 0; v < verts.Count; v++)
            {
                Vector3 a = transform.TransformPoint(verts[v]);
                Vector3 b = transform.TransformPoint(verts[(v + 1) % verts.Count]);
                Gizmos.DrawLine(a, b);
            }

            Gizmos.color = new Color(0.5f, 0.9f, 1f, 0.9f);
            foreach (var v in verts)
                Gizmos.DrawSphere(transform.TransformPoint(v), 0.06f);
        }
    }
}
