using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cinemachine;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Loads a 2D map JSON file and builds the Unity scene representation:
/// SpriteShape ground polygons, per-tile layer items (decals/resources/npcs/sprites),
/// and a single ground collider sized to the map.
/// </summary>
public class MapLoader : MonoBehaviour
{
	// ---------------------------------------------------------------------
	// Inspector
	// ---------------------------------------------------------------------

	[Header("Map File")]
	[SerializeField] private string mapDirectoryPath = "Assets";
	[SerializeField] private string mapJsonFileName = "map.json";
	[SerializeField] private bool loadAndGenerateOnStart;

	[Header("Scene Roots")]
	[SerializeField] private Transform groundRoot;
	[SerializeField] private Transform decalRoot;
	[SerializeField] private Transform spriteRoot;
	[SerializeField] private Transform npcRoot;
	[SerializeField] private Transform resourceRoot;

	[Header("SpriteShape Template")]
	[Tooltip("Source SpriteShape used as a clone template for every generated ground polygon.")]
	[SerializeField] private SpriteShapeController spriteShapeObject;
	[Tooltip("Hide the source template at runtime so it doesn't render alongside generated shapes.")]
	[SerializeField] private bool hideTemplateGround = true;

	[Header("World Mapping")]
	[SerializeField] private float worldUnitsPerTile = 1f;
	[SerializeField] private Vector3 mapOrigin;
	[SerializeField] private bool clearGeneratedBeforeBuild = true;

	[Header("Ground Collider")]
	[SerializeField] private bool generateGroundCollider = true;
	[SerializeField] private float groundColliderDepth = 1f;
	[Tooltip("Where the generated BoxCollider is parented. Falls back to this transform.")]
	[SerializeField] private Transform groundColliderRoot;

	[Header("Ground Orientation")]
	[Tooltip("Rotate generated ground SpriteShapes after baking so they lie flat for the 2.5D world. The spline math runs in XY; this rotation tilts the baked geometry into XZ.")]
	[SerializeField] private bool rotateGroundFlat = true;
	[Tooltip("Euler rotation applied to each generated ground shape after baking. (90, 0, 0) tilts an XY-plane shape onto the XZ ground plane with map-Y becoming world-Z.")]
	[SerializeField] private Vector3 groundFlatRotation = new Vector3(90f, 0f, 0f);

	[Header("Player Spawn")]
	[Tooltip("When false, no player is instantiated at runtime — use a player already placed in the scene instead.")]
	[SerializeField] private bool spawnPlayerAtRuntime = true;
	[Tooltip("Prefab spawned on the tile flagged with NPC typeId 3000 in the loaded map JSON.")]
	[SerializeField] private GameObject playerPrefab;
	[Tooltip("Where the spawned player is parented. Falls back to this transform.")]
	[SerializeField] private Transform playerRoot;
	[Tooltip("World-space Y offset above the ground collider top (Y=0). Tune to clear the player's collider half-height so it doesn't spawn embedded in the ground.")]
	[SerializeField] private float playerSpawnYOffset = 1f;

	[Header("Resource Palette")]
	[Tooltip("Maps palette stringKey / typeId to a Unity prefab. Unregistered types fall back to a colored quad.")]
	[SerializeField] private ResourcePaletteSO resourcePalette;
	[Tooltip("World-space Y offset applied to every spawned resource prefab. Keep 0 when the prefab's pivot is already at its base.")]
	[SerializeField] private float resourceSpawnYOffset = 0f;

	[Header("Ground Palette")]
	[Tooltip("Maps ground stringKey / typeId to a sorting order. Water above grass, paths above both, etc. Leave unassigned to use the default sorting order for all ground types.")]
	[SerializeField] private GroundPaletteSO groundPalette;

	[Header("Runtime (debug)")]
	[SerializeField, TextArea(6, 20)] private string loadedJson;

	// ---------------------------------------------------------------------
	// Constants / cached reflection
	// ---------------------------------------------------------------------

	// Generated GameObjects are named "<Prefix>_<id>_<key>" / "Ground_<id>_<key>" so the
	// clear helpers can find them again without tracking references explicitly.
	private const string GroundPrefix = "Ground_";
	private const string PlayerPrefix = "Player_";
	private const string GroundColliderName = "GroundCollider";

	// Fallback sorting order when no GroundPaletteSO is assigned or the type is not listed.
	// Must be below the SortableSprite dynamic range (baseOrder=5000 ± ~3500).
	private const int GroundSortingOrderFallback = -1000;

	// The NPC layer in the map editor uses id 3000 as the player-start marker.
	// We *don't* act on it here — see DialogueRuntime/spawning systems for use.
	private const int PlayerStartTypeId = 3000;

	// SpriteShapeController.Instantiate() shallow-copies the underlying Spline reference,
	// so all clones end up sharing one spline. We replace it via reflection.
	private static readonly FieldInfo SplineField =
		typeof(SpriteShapeController).GetField("m_Spline", BindingFlags.Instance | BindingFlags.NonPublic);

	// Cached 1x1 white sprite used for every layer-item quad.
	private static Sprite _unitSquareSprite;

	// ---------------------------------------------------------------------
	// Unity lifecycle / context menus
	// ---------------------------------------------------------------------

	private void Start()
	{
		HideTemplateGroundIfNeeded();
		if (loadAndGenerateOnStart)
		{
			LoadAndGenerate();
		}
	}

public void LoadAndGenerate()
	{
		if (!TryLoadAndParse(out MapData mapData))
		{
			return;
		}

		if (clearGeneratedBeforeBuild)
		{
			ClearGenerated();
		}

		HideTemplateGroundIfNeeded();
		GenerateGroundShapes(mapData);
		SpawnFlatLayerItems(mapData);          // decals, npcs, largeSprites (flat ground quads)
		ApplyGroundFlatRotation();             // tilts flat roots + resourceRoot into XZ plane
		SpawnResourceItems(mapData);           // resources: prefab or fallback quad, world-space pos
		GenerateGroundCollider(mapData);
		SpawnPlayer(mapData);
	}

public void ClearGenerated()
	{
		ClearChildrenWithPrefix(groundRoot, GroundPrefix);
		ClearChildrenWithPrefix(decalRoot, "Decal_");
		ClearChildrenWithPrefix(resourceRoot, "Resource_");
		ClearChildrenWithPrefix(npcRoot, "Npc_");
		ClearChildrenWithPrefix(spriteRoot, "Sprite_");
		ClearSpawnedPlayers();
		ClearGroundCollider();
	}

	// ---------------------------------------------------------------------
	// JSON I/O
	// ---------------------------------------------------------------------

	/// <summary>Reads and parses the configured map JSON file. Returns false on any failure.</summary>
	private bool TryLoadAndParse(out MapData mapData)
	{
		mapData = null;

		string filePath = Path.Combine(ResolveDirectoryPath(), mapJsonFileName);
		if (!File.Exists(filePath))
		{
			Debug.LogWarning($"Map JSON file not found at: {filePath}", this);
			return false;
		}

		try
		{
			loadedJson = File.ReadAllText(filePath);
			mapData = JsonUtility.FromJson<MapData>(loadedJson);
		}
		catch (Exception ex)
		{
			Debug.LogError($"Failed to load/parse map JSON '{filePath}': {ex.Message}", this);
			return false;
		}

		if (mapData == null)
		{
			Debug.LogWarning("Parsed map JSON returned null data.", this);
			return false;
		}

		return true;
	}

	/// <summary>Resolves <see cref="mapDirectoryPath"/> against the project root if relative.</summary>
	private string ResolveDirectoryPath()
	{
		if (string.IsNullOrWhiteSpace(mapDirectoryPath))
		{
			return Application.dataPath;
		}

		if (Path.IsPathRooted(mapDirectoryPath))
		{
			return mapDirectoryPath;
		}

		string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
		return Path.GetFullPath(Path.Combine(projectRoot, mapDirectoryPath));
	}

	// ---------------------------------------------------------------------
	// Ground SpriteShapes
	// ---------------------------------------------------------------------

	/// <summary>
	/// For each distinct ground id, builds a corner-perimeter polygon and instantiates a
	/// SpriteShape clone of <see cref="spriteShapeObject"/> filled with that polygon.
	/// </summary>
	private void GenerateGroundShapes(MapData mapData)
	{
		// One pass over all tiles: bucket cells by ground id.
		Dictionary<int, List<(int x, int y)>> tilesByGround = GroupTilesByGround(mapData);
		if (tilesByGround.Count == 0)
		{
			Debug.LogWarning("No ground tiles found in map JSON.", this);
			return;
		}

		Dictionary<int, GroundTypeData> groundStyles = BuildTypeLookup(mapData?.toolset?.groundTypes);
		int gridWidth = mapData?.canvas?.gridWidth ?? 0;
		int gridHeight = mapData?.canvas?.gridHeight ?? 0;

		int success = 0;
		foreach (KeyValuePair<int, List<(int x, int y)>> entry in tilesByGround)
		{
			int groundId = entry.Key;
			List<(int x, int y)> cells = entry.Value;

			List<Vector3> splinePoints = BuildGroundSplinePoints(cells, gridWidth, gridHeight);
			if (splinePoints == null || splinePoints.Count < 3)
			{
				Debug.LogWarning($"Ground id {groundId}: could not build a valid boundary.", this);
				continue;
			}

			groundStyles.TryGetValue(groundId, out GroundTypeData style);
			Color fill = ParseHtmlColor(style?.color, Color.white);

			SpriteShapeController shape = InstantiateGroundShape(groundId, style?.stringKey, fill);
			if (shape == null)
			{
				continue;
			}

			FillSpline(shape, splinePoints);
			success++;
		}

		Debug.Log($"Ground shape generation: {success}/{tilesByGround.Count} shapes built.", this);
	}

/// <summary>
	/// Tilts every generated ground SpriteShape clone into the XZ plane. Runs after
	/// FillSpline/BakeMesh so the spline math (which expects XY coords) is undisturbed —
	/// only the baked transform is rotated.
	/// </summary>
private void ApplyGroundFlatRotation()
	{
		if (!rotateGroundFlat) return;

		Quaternion rot = Quaternion.Euler(groundFlatRotation);

		// Ground SpriteShapes: rotate each clone's transform. Spline points are authored
		// in local-space XY, so per-shape rotation tilts the baked mesh around its own
		// pivot without disturbing the spline data.
		if (groundRoot != null)
		{
			for (int i = 0; i < groundRoot.childCount; i++)
			{
				Transform child = groundRoot.GetChild(i);
				if (!child.name.StartsWith(GroundPrefix, StringComparison.Ordinal)) continue;
				child.localRotation = rot;
			}
		}

		// Layer items: rotate the layer roots themselves. Each item's localPosition is its
		// XY tile coord, so rotating the parent transforms both position (XY→XZ) and the
		// SpriteRenderer facing in one step — items end up flat on the ground at their tile.
		if (decalRoot    != null) decalRoot.localRotation    = rot;
		if (resourceRoot != null) resourceRoot.localRotation = rot;
		if (npcRoot      != null) npcRoot.localRotation      = rot;
		if (spriteRoot   != null) spriteRoot.localRotation   = rot;
	}


	/// <summary>
	/// Computes the world-space corner perimeter for a single ground region using
	/// <see cref="GridPerimeterHelper"/>. Returns null on invalid input.
	/// </summary>
	private List<Vector3> BuildGroundSplinePoints(List<(int x, int y)> cells, int gridWidth, int gridHeight)
	{
		if (cells == null || cells.Count == 0)
		{
			return null;
		}

		// Fall back to the cells' own bounds when the JSON canvas dims aren't usable.
		if (gridWidth <= 0 || gridHeight <= 0)
		{
			int maxX = 0, maxY = 0;
			foreach ((int x, int y) cell in cells)
			{
				if (cell.x > maxX) maxX = cell.x;
				if (cell.y > maxY) maxY = cell.y;
			}
			gridWidth = maxX + 1;
			gridHeight = maxY + 1;
		}

		return GridPerimeterHelper.ComputeCornerPerimeter(
			cells,
			gridWidth,
			gridHeight,
			(GridPerimeterHelper.CornerToWorldFn)((cx, cy) => new Vector3(
				mapOrigin.x + cx * worldUnitsPerTile,
				mapOrigin.y + cy * worldUnitsPerTile,
				0f)));
	}

	/// <summary>
	/// Clones the SpriteShape template, gives it an independent Spline (Instantiate
	/// shares it by default), and tints it.
	/// </summary>
	private SpriteShapeController InstantiateGroundShape(int groundId, string groundKey, Color fill)
	{
		if (spriteShapeObject == null)
		{
			Debug.LogWarning("No SpriteShape template assigned.", this);
			return null;
		}

		Transform parent = groundRoot != null ? groundRoot : transform;
		string key = string.IsNullOrWhiteSpace(groundKey) ? "Unknown" : groundKey;

		GameObject go = Instantiate(spriteShapeObject.gameObject, parent);
		go.name = $"{GroundPrefix}{groundId}_{key}";
		go.transform.localPosition = spriteShapeObject.transform.localPosition;
		go.transform.localRotation = Quaternion.identity;
		go.transform.localScale = spriteShapeObject.transform.localScale;

		SpriteShapeController controller = go.GetComponent<SpriteShapeController>();

		// Replace the shallow-copied spline with a fresh one so each shape is independent.
		SplineField?.SetValue(controller, new Spline());

		SpriteShapeRenderer renderer = go.GetComponent<SpriteShapeRenderer>();
		if (renderer != null)
		{
			renderer.enabled = true;
			renderer.color = fill;
			renderer.sortingOrder = groundPalette != null
				? groundPalette.GetSortingOrder(groundId, groundKey)
				: GroundSortingOrderFallback;
		}

		return controller;
	}

	/// <summary>Writes the given corner points into the controller's spline and bakes it.</summary>
	private void FillSpline(SpriteShapeController controller, List<Vector3> points)
	{
		if (controller == null || points == null || points.Count < 3)
		{
			return;
		}

		Spline spline = controller.spline;
		spline.Clear();

		for (int i = 0; i < points.Count; i++)
		{
			Vector3 p = points[i];
			p.z = 0f;
			spline.InsertPointAt(i, p);
			spline.SetTangentMode(i, ShapeTangentMode.Linear);
		}

		spline.isOpenEnded = false;

		// Force a full rebuild so the new geometry is visible without entering play mode.
		controller.RefreshSpriteShape();
		controller.UpdateSpriteShapeParameters();
		var bakeHandle = controller.BakeMesh();
		bakeHandle.Complete();
		controller.RefreshSpriteShape();

		// Fix frustum-culling: the Job System bake does not update SpriteShapeRenderer.localBounds
		// until end-of-frame. Until then Unity sees stale/zero bounds and culls the mesh, so the
		// ground is invisible until the Scene camera moves. Force correct bounds from the spline
		// points so the renderer is never incorrectly culled at runtime.
		SpriteShapeRenderer ssr = controller.GetComponent<SpriteShapeRenderer>();
		if (ssr != null && points.Count > 0)
		{
			Bounds b = new Bounds(points[0], Vector3.zero);
			for (int i = 1; i < points.Count; i++) b.Encapsulate(points[i]);
			// Small Y padding to cover the fill mesh thickness (SpriteShape extrudes slightly in Z/Y).
			b.Expand(new Vector3(0f, 1f, 0f));
			ssr.localBounds = b;
		}
	}

	// ---------------------------------------------------------------------
	// Layer items (decals / resources / npcs / large sprites)
	// ---------------------------------------------------------------------

	/// <summary>
	/// Spawns colored quads for decals, NPCs, and large sprites — the three flat layers
	/// that lie on the ground plane. Resources are handled separately in
	/// <see cref="SpawnResourceItems"/> which runs AFTER the ground rotation is applied.
	/// </summary>
	private void SpawnFlatLayerItems(MapData mapData)
	{
		LayersData layers = mapData?.layers;
		if (layers == null)
		{
			return;
		}

		ToolsetData ts = mapData.toolset;
		int total = 0;
		total += SpawnItemSet(layers.decals, ts?.decalTypes, "Decal", decalRoot);
		total += SpawnItemSet(layers.npcs, ts?.npcTypes, "Npc", npcRoot);
		total += SpawnItemSet(layers.largeSprites, ts?.largeSpriteTypes, "Sprite", spriteRoot);
		Debug.Log($"Spawned {total} flat layer items.", this);
	}

	/// <summary>
	/// Spawns resource items from the map, called AFTER <see cref="ApplyGroundFlatRotation"/>
	/// so that resourceRoot is already in its final (90,0,0) orientation.
	///
	/// For each resource tile:
	/// - If <see cref="resourcePalette"/> has a matching entry, the linked prefab is
	///   instantiated at the correct world-space position with upright rotation.
	/// - Otherwise a plain colored quad is spawned as a fallback (same as the legacy path).
	/// </summary>
	private void SpawnResourceItems(MapData mapData)
	{
		LayerItemData[] items = mapData?.layers?.resources;
		if (items == null || items.Length == 0)
		{
			return;
		}

		if (resourceRoot == null)
		{
			Debug.LogWarning("No resourceRoot assigned — skipping resource items.", this);
			return;
		}

		Dictionary<int, GroundTypeData> typeLookup = BuildTypeLookup(mapData.toolset?.resourceTypes);
		int prefabCount = 0;
		int quadCount = 0;

		for (int i = 0; i < items.Length; i++)
		{
			LayerItemData item = items[i];
			if (item == null)
			{
				continue;
			}

			typeLookup.TryGetValue(item.typeId, out GroundTypeData typeEntry);
			string key = typeEntry?.stringKey ?? item.typeId.ToString();
			string goName = $"Resource_{item.typeId}_{key}";

			if (resourcePalette != null && resourcePalette.TryGetPrefab(item.typeId, key, out GameObject prefab))
			{
				// World-space position: tile centre on the XZ ground plane.
				// Using the 4-arg Instantiate overload places the GO at world position / rotation
				// regardless of the parent's (already-rotated) transform, so the prefab stands
				// upright and its colliders are correctly oriented.
				Vector3 worldPos = new Vector3(
					mapOrigin.x + (item.x + 0.5f) * worldUnitsPerTile,
					resourceSpawnYOffset,
					mapOrigin.y + (item.y + 0.5f) * worldUnitsPerTile);

				GameObject go = Instantiate(prefab, worldPos, Quaternion.identity, resourceRoot);
				go.name = goName;
				prefabCount++;
			}
			else
			{
				// Fallback: flat colored quad. resourceRoot is already rotated, so local XY
				// maps to world XZ — SpawnLayerItem's local-position math still works correctly.
				Color color = ParseHtmlColor(typeEntry?.color, Color.white);
				SpawnLayerItem(item.x, item.y, goName, color, item.typeId, resourceRoot);
				quadCount++;
			}
		}

		Debug.Log($"Resources spawned: {prefabCount} prefab(s), {quadCount} fallback quad(s).", this);
	}

	private int SpawnItemSet(LayerItemData[] items, GroundTypeData[] types, string prefix, Transform parent)
	{
		if (items == null || items.Length == 0)
		{
			return 0;
		}

		if (parent == null)
		{
			Debug.LogWarning($"No parent root assigned for {prefix} items — skipping {items.Length} entries.", this);
			return 0;
		}

		// Build the id→type lookup once per layer instead of once per item.
		Dictionary<int, GroundTypeData> typeLookup = BuildTypeLookup(types);
		int count = 0;

		for (int i = 0; i < items.Length; i++)
		{
			LayerItemData item = items[i];
			if (item == null)
			{
				continue;
			}

			typeLookup.TryGetValue(item.typeId, out GroundTypeData typeEntry);
			string key = typeEntry?.stringKey ?? item.typeId.ToString();
			Color color = ParseHtmlColor(typeEntry?.color, Color.white);

			SpawnLayerItem(item.x, item.y, $"{prefix}_{item.typeId}_{key}", color, item.typeId, parent);
			count++;
		}

		return count;
	}

	/// <summary>Creates a single tile-sized SpriteRenderer quad at the given tile coords.</summary>
	private void SpawnLayerItem(int tileX, int tileY, string itemName, Color color, int sortingOrder, Transform parent)
	{
		// +0.5 centers the quad in the tile.
		Vector3 tilePos = new Vector3(
			mapOrigin.x + (tileX + 0.5f) * worldUnitsPerTile,
			mapOrigin.y + (tileY + 0.5f) * worldUnitsPerTile,
			0f);

		GameObject go = new GameObject(itemName);
		go.transform.SetParent(parent, false);
		go.transform.localPosition = tilePos;
		go.transform.localRotation = Quaternion.identity;
		go.transform.localScale = Vector3.one * worldUnitsPerTile;

		SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
		sr.sprite = GetUnitSquareSprite();
		sr.color = color;
		sr.sortingOrder = sortingOrder;
	}

	// ---------------------------------------------------------------------
	// Ground collider
	// ---------------------------------------------------------------------

	/// <summary>
	/// Computes the overall tile bounds and creates a single BoxCollider that covers the
	/// whole map. The top face sits at world Y = 0.
	/// </summary>
	private void GenerateGroundCollider(MapData mapData)
	{
		if (!generateGroundCollider || mapData?.islands == null)
		{
			return;
		}

		if (!TryComputeTileBounds(mapData, out int minX, out int maxX, out int minY, out int maxY))
		{
			Debug.LogWarning("No tiles found — cannot generate ground collider.", this);
			return;
		}

		// World extents: a tile at (x,y) occupies the cell [x .. x+1] in tile space.
		float worldMinX = mapOrigin.x + minX * worldUnitsPerTile;
		float worldMaxX = mapOrigin.x + (maxX + 1) * worldUnitsPerTile;
		float worldMinZ = mapOrigin.y + minY * worldUnitsPerTile;
		float worldMaxZ = mapOrigin.y + (maxY + 1) * worldUnitsPerTile;

		float sizeX = worldMaxX - worldMinX;
		float sizeZ = worldMaxZ - worldMinZ;
		Vector3 center = new Vector3(
			(worldMinX + worldMaxX) * 0.5f,
			-groundColliderDepth * 0.5f, // top face at Y=0
			(worldMinZ + worldMaxZ) * 0.5f);

		Transform parent = groundColliderRoot != null ? groundColliderRoot : transform;
		GameObject go = new GameObject(GroundColliderName);
		go.transform.SetParent(parent, false);
		go.transform.localPosition = center;
		go.transform.localRotation = Quaternion.identity;
		go.transform.localScale = Vector3.one;

		BoxCollider box = go.AddComponent<BoxCollider>();
		box.center = Vector3.zero;
		box.size = new Vector3(sizeX, groundColliderDepth, sizeZ);

		// Kinematic Rigidbody so physics treats this as a static body without batching cost.
		Rigidbody rb = go.AddComponent<Rigidbody>();
		rb.isKinematic = true;

		Debug.Log($"Ground collider: size ({sizeX:F2}, {groundColliderDepth:F2}, {sizeZ:F2}) at {center}.", this);
	}

	private void ClearGroundCollider()
	{
		Transform parent = groundColliderRoot != null ? groundColliderRoot : transform;
		for (int i = parent.childCount - 1; i >= 0; i--)
		{
			GameObject child = parent.GetChild(i).gameObject;
			if (child.name.Equals(GroundColliderName, StringComparison.Ordinal))
			{
				DestroySafe(child);
			}
		}
	}

/// <summary>
	/// Spawns <see cref="playerPrefab"/> at the NPC-layer tile flagged with typeId
	/// <see cref="PlayerStartTypeId"/>. Tile (x,y) maps to world (x, offset, y) so the
	/// player lands above the ground collider whose top face is at Y=0.
	/// </summary>
private void SpawnPlayer(MapData mapData)
	{
		// Opt-out: scenes that place a persistent player by hand skip runtime spawning.
		if (!spawnPlayerAtRuntime)
		{
			return;
		}

		// Runtime only — in edit mode the player-start NPC marker quad acts as a placement
		// preview; the actual prefab is instantiated when entering play mode.
		if (!Application.isPlaying)
		{
			return;
		}

		if (playerPrefab == null)
		{
			Debug.LogWarning("No player prefab assigned — skipping player spawn.", this);
			return;
		}

		LayerItemData startTile = FindPlayerStartTile(mapData);
		if (startTile == null)
		{
			Debug.LogWarning($"No player-start tile (NPC typeId {PlayerStartTypeId}) found in map JSON.", this);
			return;
		}

		Transform parent = playerRoot != null ? playerRoot : transform;
		Vector3 spawnPos = new Vector3(
			mapOrigin.x + (startTile.x + 0.5f) * worldUnitsPerTile,
			playerSpawnYOffset,
			mapOrigin.y + (startTile.y + 0.5f) * worldUnitsPerTile);

		// Use the position-providing Instantiate overload so the transform is set BEFORE
		// any component awakens. This avoids a race where a Rigidbody on the prefab caches
		// its position from the prefab origin (0,0,0) and overrides a later transform write.
		GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity, parent);
		player.name = $"{PlayerPrefix}{startTile.typeId}";

		// Belt-and-braces: explicitly sync Rigidbody state in case the prefab ships with
		// cached velocity or a Rigidbody position that wasn't picked up from the transform.
		if (player.TryGetComponent(out Rigidbody rb))
		{
			rb.position = spawnPos;
			rb.velocity = Vector3.zero;
			rb.angularVelocity = Vector3.zero;
		}

		Debug.Log($"Player spawned at tile ({startTile.x}, {startTile.y}) → intended {spawnPos}, actual {player.transform.position}.", this);

		CinemachineVirtualCamera vcam = GameObject.FindObjectOfType<CinemachineVirtualCamera>();
		if (vcam != null)
		{
			vcam.Follow = player.transform;
			Debug.Log($"Cinemachine virtual camera '{vcam.name}' follow target set to '{player.name}'.", this);
		}
		else
		{
			Debug.LogWarning("No CinemachineVirtualCamera found in scene — follow target not set.", this);
		}
	}

	/// <summary>Returns the first NPC-layer entry tagged as the player start, or null.</summary>
	private static LayerItemData FindPlayerStartTile(MapData mapData)
	{
		LayerItemData[] npcs = mapData?.layers?.npcs;
		if (npcs == null) return null;

		for (int i = 0; i < npcs.Length; i++)
		{
			LayerItemData item = npcs[i];
			if (item != null && item.typeId == PlayerStartTypeId) return item;
		}
		return null;
	}

	/// <summary>Destroys previously-spawned players so re-running the load doesn't stack them.</summary>
	private void ClearSpawnedPlayers()
	{
		Transform parent = playerRoot != null ? playerRoot : transform;
		for (int i = parent.childCount - 1; i >= 0; i--)
		{
			GameObject child = parent.GetChild(i).gameObject;
			if (child.name.StartsWith(PlayerPrefix, StringComparison.Ordinal))
			{
				DestroySafe(child);
			}
		}
	}


	// ---------------------------------------------------------------------
	// Map-data passes (single scan reused for several outputs)
	// ---------------------------------------------------------------------

	/// <summary>Single pass over every tile that groups them by ground id.</summary>
	private static Dictionary<int, List<(int x, int y)>> GroupTilesByGround(MapData mapData)
	{
		Dictionary<int, List<(int x, int y)>> result = new Dictionary<int, List<(int x, int y)>>();
		if (mapData?.islands == null)
		{
			return result;
		}

		for (int i = 0; i < mapData.islands.Length; i++)
		{
			TileData[] tiles = mapData.islands[i]?.tiles;
			if (tiles == null)
			{
				continue;
			}

			for (int t = 0; t < tiles.Length; t++)
			{
				TileData tile = tiles[t];
				if (tile == null)
				{
					continue;
				}

				if (!result.TryGetValue(tile.ground, out List<(int x, int y)> list))
				{
					list = new List<(int x, int y)>();
					result[tile.ground] = list;
				}

				list.Add((tile.x, tile.y));
			}
		}

		return result;
	}

	/// <summary>Single pass to find the (min,max) tile bounds across all islands.</summary>
	private static bool TryComputeTileBounds(MapData mapData, out int minX, out int maxX, out int minY, out int maxY)
	{
		minX = int.MaxValue; maxX = int.MinValue;
		minY = int.MaxValue; maxY = int.MinValue;

		if (mapData?.islands == null)
		{
			return false;
		}

		for (int i = 0; i < mapData.islands.Length; i++)
		{
			TileData[] tiles = mapData.islands[i]?.tiles;
			if (tiles == null)
			{
				continue;
			}

			for (int t = 0; t < tiles.Length; t++)
			{
				TileData tile = tiles[t];
				if (tile == null)
				{
					continue;
				}

				if (tile.x < minX) minX = tile.x;
				if (tile.x > maxX) maxX = tile.x;
				if (tile.y < minY) minY = tile.y;
				if (tile.y > maxY) maxY = tile.y;
			}
		}

		return minX != int.MaxValue;
	}

	// ---------------------------------------------------------------------
	// Small helpers
	// ---------------------------------------------------------------------

	private static Dictionary<int, GroundTypeData> BuildTypeLookup(GroundTypeData[] types)
	{
		Dictionary<int, GroundTypeData> lookup = new Dictionary<int, GroundTypeData>();
		if (types == null)
		{
			return lookup;
		}

		for (int i = 0; i < types.Length; i++)
		{
			GroundTypeData type = types[i];
			if (type != null)
			{
				lookup[type.id] = type;
			}
		}

		return lookup;
	}

	private static Color ParseHtmlColor(string html, Color fallback)
	{
		if (string.IsNullOrWhiteSpace(html))
		{
			return fallback;
		}

		return ColorUtility.TryParseHtmlString(html, out Color c) ? c : fallback;
	}

	/// <summary>Returns a shared 1x1 white sprite used for every layer-item quad.</summary>
	private static Sprite GetUnitSquareSprite()
	{
		if (_unitSquareSprite != null)
		{
			return _unitSquareSprite;
		}

		Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
		tex.SetPixel(0, 0, Color.white);
		tex.Apply();
		_unitSquareSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
		return _unitSquareSprite;
	}

	private void HideTemplateGroundIfNeeded()
	{
		if (!hideTemplateGround || spriteShapeObject == null)
		{
			return;
		}

		SpriteShapeRenderer renderer = spriteShapeObject.GetComponent<SpriteShapeRenderer>();
		if (renderer != null)
		{
			renderer.enabled = false;
		}
	}

	/// <summary>
	/// Destroys every direct child of <paramref name="root"/> whose name starts with
	/// <paramref name="namePrefix"/>. Used to clean previously-generated objects.
	/// </summary>
	private void ClearChildrenWithPrefix(Transform root, string namePrefix)
	{
		if (root == null)
		{
			return;
		}

		for (int i = root.childCount - 1; i >= 0; i--)
		{
			GameObject child = root.GetChild(i).gameObject;

			// Never destroy the template itself.
			if (spriteShapeObject != null && child == spriteShapeObject.gameObject)
			{
				continue;
			}

			if (child.name.StartsWith(namePrefix, StringComparison.Ordinal))
			{
				DestroySafe(child);
			}
		}
	}

	private static void DestroySafe(GameObject go)
	{
		if (Application.isPlaying)
		{
			Destroy(go);
		}
		else
		{
			DestroyImmediate(go);
		}
	}

	// ---------------------------------------------------------------------
	// JSON DTOs (must mirror the map editor's output format)
	// ---------------------------------------------------------------------

	[Serializable]
	private class MapData
	{
		public int version;
		public string mapName;
		public CanvasData canvas;
		public ToolsetData toolset;
		public IslandData[] islands;
		public LayersData layers;
	}

	[Serializable]
	private class CanvasData
	{
		public int size;
		public int cellSize;
		public int gridWidth;
		public int gridHeight;
	}

	[Serializable]
	private class ToolsetData
	{
		public GroundTypeData[] groundTypes;
		public GroundTypeData[] decalTypes;
		public GroundTypeData[] resourceTypes;
		public GroundTypeData[] npcTypes;
		public GroundTypeData[] largeSpriteTypes;
	}

	[Serializable]
	private class GroundTypeData
	{
		public int id;
		public string name;
		public string stringKey;
		public string color;
		public bool enabled;
	}

	[Serializable]
	private class IslandData
	{
		public string id;
		public string name;
		public string outlineColor;
		public TileData[] tiles;
	}

	[Serializable]
	private class TileData
	{
		public int x;
		public int y;
		public int ground;
	}

	[Serializable]
	private class LayersData
	{
		public LayerItemData[] decals;
		public LayerItemData[] resources;
		public LayerItemData[] npcs;
		public LayerItemData[] largeSprites;
	}

	[Serializable]
	private class LayerItemData
	{
		public string id;
		public int typeId;
		public int x;
		public int y;
		public string islandId;
	}
}
