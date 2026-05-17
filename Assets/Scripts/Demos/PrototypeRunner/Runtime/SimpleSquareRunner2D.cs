using UnityEngine;
using UnityEngine.Tilemaps;
using TMPro;

[DisallowMultipleComponent]
public class SimpleSquareRunner2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5.5f;
    [SerializeField] private float acceleration = 55f;
    [SerializeField] private float deceleration = 65f;

    [Header("Walkable Top Surface")]
    [Tooltip("Tilemap used as the walkable mask for the island top surface.")]
    [SerializeField] private Tilemap walkableTilemap;
    [Tooltip("Probe half-extents used to keep the square body inside walkable tiles.")]
    [SerializeField] private Vector2 walkProbeExtents = new Vector2(0.26f, 0.26f);

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Run Bounce")]
    [SerializeField] private bool enableRunBounce = true;
    [SerializeField] private float bounceAmplitude = 0.075f;
    [SerializeField] private float bounceFrequency = 12f;
    [SerializeField] private float squashAmount = 0.08f;
    [SerializeField] private float visualSettleSpeed = 12f;

    [Header("Direction Prototype")]
    [SerializeField] private bool showDirectionIndicator = true;
    [SerializeField] private Vector3 directionIndicatorLocalOffset = new Vector3(0f, 1.15f, -0.2f);
    [SerializeField] private string directionArrowGlyph = "▲";
    [SerializeField] private float directionIndicatorFontSize = 3f;
    [SerializeField] private Color directionIndicatorColor = Color.white;
    [SerializeField] private float directionTurnSpeed = 18f;
    [SerializeField] private TextMeshPro directionIndicatorTextMesh;

    private float _inputX;
    private float _inputY;
    private float _facing = 1f;
    private float _bounceClock;
    private Vector2 _velocity;
    private Vector3 _baseSpriteLocalPosition;
    private Vector3 _baseSpriteLocalScale;
    private bool _hasSpriteBaseline;
    private Transform _directionIndicatorRoot;
    private TextMeshPro _directionIndicator;
    private Vector2 _lastMoveDirection = Vector2.up;

    private void Reset()
    {
        AutoBindWalkableTilemap();
        AutoBindSpriteRenderer();
        CaptureSpriteBaseline();
        EnsureDirectionIndicator();
        UpdateDirectionIndicator();
    }

    private void Awake()
    {
        AutoBindWalkableTilemap();

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;

        AutoBindSpriteRenderer();
        CaptureSpriteBaseline();
        EnsureDirectionIndicator();
        UpdateDirectionIndicator();
    }

    private void Update()
    {
        _inputX = Input.GetAxisRaw("Horizontal");
        _inputY = Input.GetAxisRaw("Vertical");

        Vector2 input = new Vector2(_inputX, _inputY);
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        float inputMagnitude = input.magnitude;
        float ramp = inputMagnitude > 0.01f ? acceleration : deceleration;

        Vector2 targetVelocity = input * moveSpeed;
        _velocity.x = Mathf.MoveTowards(_velocity.x, targetVelocity.x, ramp * Time.deltaTime);
        _velocity.y = Mathf.MoveTowards(_velocity.y, targetVelocity.y, ramp * Time.deltaTime);

        MoveConstrained(_velocity * Time.deltaTime);

        if (_velocity.sqrMagnitude > 0.0001f)
            _lastMoveDirection = _velocity.normalized;
        else if (input.sqrMagnitude > 0.0001f)
            _lastMoveDirection = input.normalized;

        if (_inputX > 0.01f)
            _facing = 1f;
        else if (_inputX < -0.01f)
            _facing = -1f;

        UpdateFacing();
        UpdateRunBounce();
        UpdateDirectionIndicator();
    }

    private void UpdateFacing()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.flipX = _facing > 0f;
    }

    private void UpdateRunBounce()
    {
        if (spriteRenderer == null)
            return;

        if (!_hasSpriteBaseline)
            CaptureSpriteBaseline();

        if (!enableRunBounce)
            return;

        Transform spriteTransform = spriteRenderer.transform;
        float runAmount = Mathf.Clamp01(_velocity.magnitude / Mathf.Max(0.01f, moveSpeed));
        bool running = runAmount > 0.05f;

        Vector3 targetPos = _baseSpriteLocalPosition;
        Vector3 targetScale = _baseSpriteLocalScale;

        if (running)
        {
            _bounceClock += Time.deltaTime * bounceFrequency * Mathf.Lerp(0.7f, 1.4f, runAmount);
            float pulse = Mathf.Abs(Mathf.Sin(_bounceClock));

            targetPos.y += pulse * bounceAmplitude;
            targetScale.x = _baseSpriteLocalScale.x * (1f + pulse * squashAmount);
            targetScale.y = _baseSpriteLocalScale.y * (1f - pulse * squashAmount);
        }

        float t = 1f - Mathf.Exp(-visualSettleSpeed * Time.deltaTime);
        spriteTransform.localPosition = Vector3.Lerp(spriteTransform.localPosition, targetPos, t);
        spriteTransform.localScale = Vector3.Lerp(spriteTransform.localScale, targetScale, t);
    }

    private void MoveConstrained(Vector2 delta)
    {
        if (delta.sqrMagnitude <= 0f)
            return;

        Vector3 start = transform.position;
        Vector3 tryFull = start + new Vector3(delta.x, delta.y, 0f);
        if (IsWalkableAt(tryFull))
        {
            transform.position = tryFull;
            return;
        }

        Vector3 tryX = start + new Vector3(delta.x, 0f, 0f);
        bool moved = false;
        if (IsWalkableAt(tryX))
        {
            start = tryX;
            moved = true;
        }

        Vector3 tryY = start + new Vector3(0f, delta.y, 0f);
        if (IsWalkableAt(tryY))
        {
            start = tryY;
            moved = true;
        }

        if (moved)
            transform.position = start;
        else
            _velocity = Vector2.Lerp(_velocity, Vector2.zero, 0.35f);
    }

    private bool IsWalkableAt(Vector3 centerWorld)
    {
        if (walkableTilemap == null)
            return true;

        float ex = Mathf.Max(0.05f, walkProbeExtents.x);
        float ey = Mathf.Max(0.05f, walkProbeExtents.y);

        Vector3[] samplePoints =
        {
            centerWorld,
            centerWorld + new Vector3(-ex, 0f, 0f),
            centerWorld + new Vector3(ex, 0f, 0f),
            centerWorld + new Vector3(0f, -ey, 0f),
            centerWorld + new Vector3(0f, ey, 0f),
            centerWorld + new Vector3(-ex, -ey, 0f),
            centerWorld + new Vector3(ex, -ey, 0f),
            centerWorld + new Vector3(-ex, ey, 0f),
            centerWorld + new Vector3(ex, ey, 0f)
        };

        for (int i = 0; i < samplePoints.Length; i++)
        {
            Vector3Int cell = walkableTilemap.WorldToCell(samplePoints[i]);
            if (!walkableTilemap.HasTile(cell))
                return false;
        }

        return true;
    }

    private void AutoBindWalkableTilemap()
    {
        if (walkableTilemap != null)
            return;

        GameObject gameplayTilemapGo = GameObject.Find("Grid/Tilemap_Gameplay");
        if (gameplayTilemapGo != null && gameplayTilemapGo.TryGetComponent(out Tilemap tm))
        {
            walkableTilemap = tm;
            return;
        }

        Tilemap any = FindFirstObjectByType<Tilemap>();
        if (any != null)
            walkableTilemap = any;
    }

    private void AutoBindSpriteRenderer()
    {
        if (spriteRenderer != null)
            return;

        if (!TryGetComponent(out spriteRenderer))
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void CaptureSpriteBaseline()
    {
        if (spriteRenderer == null)
            return;

        Transform spriteTransform = spriteRenderer.transform;
        _baseSpriteLocalPosition = spriteTransform.localPosition;
        _baseSpriteLocalScale = spriteTransform.localScale;
        _hasSpriteBaseline = true;
    }

    private void EnsureDirectionIndicator()
    {
        if (_directionIndicatorRoot == null)
        {
            if (directionIndicatorTextMesh != null)
                _directionIndicatorRoot = directionIndicatorTextMesh.transform.parent;

            if (_directionIndicatorRoot == null)
            {
                Transform existing = transform.Find("DirectionIndicator");
                if (existing != null)
                    _directionIndicatorRoot = existing;
            }

            if (_directionIndicatorRoot == null)
            {
                GameObject rootGo = new GameObject("DirectionIndicator");
                _directionIndicatorRoot = rootGo.transform;
                _directionIndicatorRoot.SetParent(transform, false);
            }
        }

        if (_directionIndicator == null)
        {
            _directionIndicator = directionIndicatorTextMesh;

            if (_directionIndicator == null && _directionIndicatorRoot != null)
                _directionIndicator = _directionIndicatorRoot.GetComponentInChildren<TextMeshPro>(true);

            if (_directionIndicator == null && _directionIndicatorRoot != null)
            {
                GameObject textGo = new GameObject("DirectionText");
                textGo.transform.SetParent(_directionIndicatorRoot, false);
                _directionIndicator = textGo.AddComponent<TextMeshPro>();
            }

            directionIndicatorTextMesh = _directionIndicator;
        }

        if (_directionIndicatorRoot != null)
            _directionIndicatorRoot.localPosition = directionIndicatorLocalOffset;

        if (_directionIndicator == null)
            return;

        _directionIndicator.alignment = TextAlignmentOptions.Center;
        _directionIndicator.fontSize = Mathf.Max(0.5f, directionIndicatorFontSize);
        _directionIndicator.color = directionIndicatorColor;

        MeshRenderer renderer = _directionIndicator.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sortingOrder = 200;
    }

    private void UpdateDirectionIndicator()
    {
        if (!showDirectionIndicator)
        {
            if (_directionIndicatorRoot != null)
                _directionIndicatorRoot.gameObject.SetActive(false);

            return;
        }

        EnsureDirectionIndicator();
        if (_directionIndicatorRoot == null || _directionIndicator == null)
            return;

        if (!_directionIndicatorRoot.gameObject.activeSelf)
            _directionIndicatorRoot.gameObject.SetActive(true);

        _directionIndicatorRoot.localPosition = directionIndicatorLocalOffset;

        _directionIndicator.text = string.IsNullOrEmpty(directionArrowGlyph) ? "▲" : directionArrowGlyph;

        Vector2 direction = _lastMoveDirection.sqrMagnitude > 0.0001f
            ? _lastMoveDirection
            : new Vector2(_facing, 0f);

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        float currentAngle = _directionIndicatorRoot.localEulerAngles.z;
        float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, directionTurnSpeed) * Time.deltaTime);
        float z = Mathf.LerpAngle(currentAngle, targetAngle, t);
        _directionIndicatorRoot.localRotation = Quaternion.Euler(0f, 0f, z);
    }
}