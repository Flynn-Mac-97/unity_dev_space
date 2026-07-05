using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public static class SetupEnvironmentSprites
{
    [MenuItem("Tools/Flynn/Setup Environment Sprites")]
    public static void Execute()
    {
        string folder = "Assets/Flynn/Sprites/Environment";
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });

        if (guids.Length == 0)
        {
            Debug.LogWarning("[SetupEnvSprites] No textures found in " + folder);
            return;
        }

        Debug.Log($"[SetupEnvSprites] Processing {guids.Length} textures...");

        // Phase 1: Configure import settings + auto-slice
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Debug.Log($"[{i + 1}/{guids.Length}] {Path.GetFileName(path)}");

            try
            {
                ProcessTexture(path);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed: {path}\n{e}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Phase 2: Place in scene
        PlaceAllSpritesInScene(guids);

        // Save scene
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

        Debug.Log("[SetupEnvSprites] Complete!");
    }

    static void ProcessTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        bool wasReadable = importer.isReadable;

        // Configure: Sprite, Multiple, 256 PPU, readable
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 256;
        importer.isReadable = true;
        importer.SaveAndReimport();

        // Reload importer + texture
        importer = AssetImporter.GetAtPath(path) as TextureImporter;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            Debug.LogError("  Cannot load texture");
            return;
        }

        // Find sprite rects via flood fill on alpha
        List<Rect> rects;
        bool hasAlpha = importer.DoesSourceTextureHaveAlpha();

        if (hasAlpha)
        {
            try
            {
                rects = FindSpriteRects(texture);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"  Flood fill failed ({e.Message}), using whole texture");
                rects = new List<Rect> { new Rect(0, 0, texture.width, texture.height) };
            }
        }
        else
        {
            rects = new List<Rect> { new Rect(0, 0, texture.width, texture.height) };
        }

        if (rects.Count == 0)
        {
            Debug.LogWarning("  No sprites found (empty texture?), skipping");
            return;
        }

        // Add 2px padding
        for (int i = 0; i < rects.Count; i++)
        {
            var r = rects[i];
            float pad = 2f;
            r.x = Mathf.Max(0, r.x - pad);
            r.y = Mathf.Max(0, r.y - pad);
            r.width = Mathf.Min(texture.width - r.x, r.width + pad * 2);
            r.height = Mathf.Min(texture.height - r.y, r.height + pad * 2);
            rects[i] = r;
        }

        Debug.Log($"  {rects.Count} sprite(s)");

        // Apply sprite rects via ISpriteEditorDataProvider
        ApplySpriteRects(importer, rects, path);

        // Restore isReadable
        importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && !wasReadable)
        {
            importer.isReadable = false;
            importer.SaveAndReimport();
        }
    }

    static List<Rect> FindSpriteRects(Texture2D texture)
    {
        int w = texture.width;
        int h = texture.height;
        Color32[] pixels = texture.GetPixels32();
        bool[] visited = new bool[pixels.Length];
        var rects = new List<Rect>();

        const byte alphaThreshold = 12;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (visited[idx] || pixels[idx].a < alphaThreshold) continue;

                int minX = x, maxX = x, minY = y, maxY = y;
                var queue = new Queue<int>(256);
                queue.Enqueue(idx);
                visited[idx] = true;

                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue();
                    int cx = cur % w;
                    int cy = cur / w;

                    if (cx < minX) minX = cx;
                    if (cx > maxX) maxX = cx;
                    if (cy < minY) minY = cy;
                    if (cy > maxY) maxY = cy;

                    if (cx > 0 && !visited[cur - 1] && pixels[cur - 1].a >= alphaThreshold)
                    { visited[cur - 1] = true; queue.Enqueue(cur - 1); }
                    if (cx < w - 1 && !visited[cur + 1] && pixels[cur + 1].a >= alphaThreshold)
                    { visited[cur + 1] = true; queue.Enqueue(cur + 1); }
                    if (cy > 0 && !visited[cur - w] && pixels[cur - w].a >= alphaThreshold)
                    { visited[cur - w] = true; queue.Enqueue(cur - w); }
                    if (cy < h - 1 && !visited[cur + w] && pixels[cur + w].a >= alphaThreshold)
                    { visited[cur + w] = true; queue.Enqueue(cur + w); }
                }

                rects.Add(new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
            }
        }

        // Sort: top to bottom, left to right
        rects = rects.OrderByDescending(r => r.y).ThenBy(r => r.x).ToList();
        return rects;
    }

    static void ApplySpriteRects(TextureImporter importer, List<Rect> rects, string assetPath)
    {
        var factory = new SpriteDataProviderFactories();
        factory.Init();

        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (dataProvider == null)
        {
            Debug.LogError("  ISpriteEditorDataProvider unavailable");
            return;
        }

        dataProvider.InitSpriteEditorDataProvider();

        string baseName = Path.GetFileNameWithoutExtension(assetPath);
        var spriteRects = new List<SpriteRect>();
        for (int i = 0; i < rects.Count; i++)
        {
            spriteRects.Add(new SpriteRect
            {
                name = $"{baseName}_{i}",
                rect = rects[i],
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = new Vector4(0, 0, 0, 0)
            });
        }

        dataProvider.SetSpriteRects(spriteRects.ToArray());

        var nameIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameIdProvider != null)
        {
            var pairs = new List<SpriteNameFileIdPair>();
            foreach (var sr in spriteRects)
                pairs.Add(new SpriteNameFileIdPair(sr.name, GUID.Generate()));
            nameIdProvider.SetNameFileIdPairs(pairs);
        }

        dataProvider.Apply();
        importer.SaveAndReimport();
    }

    static void PlaceAllSpritesInScene(string[] guids)
    {
        var allEntries = new List<(string group, Sprite sprite)>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                .Where(a => a is Sprite)
                .Cast<Sprite>()
                .ToArray();

            string groupName = Path.GetFileNameWithoutExtension(path);
            foreach (var s in sprites)
                allEntries.Add((groupName, s));
        }

        if (allEntries.Count == 0)
        {
            Debug.LogWarning("[SetupEnvSprites] No sprites to place");
            return;
        }

        Debug.Log($"[SetupEnvSprites] Placing {allEntries.Count} sprites...");

        var parent = new GameObject("EnvironmentSprites");
        Undo.RegisterCreatedObjectUndo(parent, "Setup Environment Sprites");

        var groups = allEntries.GroupBy(e => e.group).ToList();
        int columnsPerRow = 10;
        float spacingX = 2f;
        float spacingY = 2f;
        int currentRow = 0;

        foreach (var group in groups)
        {
            var groupObj = new GameObject(group.Key);
            groupObj.transform.SetParent(parent.transform, false);

            var sprites = group.ToList();
            for (int i = 0; i < sprites.Count; i++)
            {
                int col = i % columnsPerRow;
                int row = i / columnsPerRow;

                var go = new GameObject(sprites[i].sprite.name);
                go.transform.SetParent(groupObj.transform, false);
                go.transform.position = new Vector3(
                    col * spacingX,
                    -(currentRow + row) * spacingY,
                    0
                );

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprites[i].sprite;
            }

            currentRow += (sprites.Count - 1) / columnsPerRow + 2;
        }

        Debug.Log($"[SetupEnvSprites] Created {allEntries.Count} GameObjects under 'EnvironmentSprites'");
    }
}
