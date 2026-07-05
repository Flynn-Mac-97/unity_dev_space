using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;
// touch

using Flynn.UI.Core;

public class FixShadowShapes
{
    [MenuItem("Flynn/Fix Shadow Shapes")]
    public static void Fix()
    {
        var casters = Object.FindObjectsOfType<ShadowCaster2D>();
        int updated = 0;

        foreach (var sc in casters)
        {
            var sr = sc.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) continue;

            var sprite = sr.sprite;
            if (sprite.GetPhysicsShapeCount() == 0) continue;

            var pts = new System.Collections.Generic.List<Vector2>();
            sprite.GetPhysicsShape(0, pts);
            if (pts.Count < 3) continue;

            int step = Mathf.Max(1, pts.Count / 20);
            var shapePath = new System.Collections.Generic.List<Vector2>();
            for (int j = 0; j < pts.Count; j += step)
                shapePath.Add(pts[j]);

            if (shapePath.Count > 2 && shapePath[0] != shapePath[shapePath.Count - 1])
                shapePath.Add(shapePath[0]);

            var shapeField = sc.GetType().GetField("m_ShapePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (shapeField != null)
            {
                shapeField.SetValue(sc, shapePath);
                sc.useRendererSilhouette = false;
                updated++;
            }
        }

        Debug.Log("Fixed " + updated + "/" + casters.Length + " shadow casters with alpha shapes");
    }
}
