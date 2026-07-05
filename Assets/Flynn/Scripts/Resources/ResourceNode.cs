using UnityEngine;
using UnityEngine.Events;
using Flynn.Core;
using Flynn.Events;
using Flynn.UI.Core;
using Flynn.Player;
using Flynn.Player.Interaction;
using Flynn.World;

namespace Flynn.Resources
{
    /// <summary>
    /// Harvestable resource node. Uses Hoverable for outline highlight and
    /// Interactable for the hover prompt ("LMB Harvest Tree").
    /// Hits arrive via ResourceHit events from the wrench swing system in PlayerController2D.
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _visualRigidbody;
        [SerializeField] private ResourceNodeConfig _config;

        private int _currentHealth;
        private bool _isDepleted;

        // Cached visual refs for stage swaps
        private SpriteRenderer _mainSpriteRenderer;
        private Transform _visualRoot;
        private Coroutine _stagePopRoutine;
        private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");

        public int MaxHealth => _config != null ? _config.maxHealth : 1;
        public ResourceType ResourceKind => _config != null ? _config.resourceType : ResourceType.None;
        public float Weight => _config != null ? _config.weight : 10f;
        public float HitKnockback => _config != null ? _config.hitKnockback : 0.8f;
        public float CameraShakeAmount => _config != null ? _config.cameraShakeAmount : 0.3f;

        private void Start()
        {
            _currentHealth = _config != null ? _config.maxHealth : 1;
            CacheVisualRefs();
            UpdateStageSprite(1f);
            RegisterWithWind();
            RaiseSpawn();
        }

        private void CacheVisualRefs()
        {
            _visualRoot = transform.Find("VisualRoot");
            if (_visualRoot == null)
                _visualRoot = transform.Find("ResourceCollider/VisualRoot");
            if (_visualRoot != null)
                _mainSpriteRenderer = _visualRoot.GetComponent<SpriteRenderer>();
        }

        private void RefreshPolygonCollider()
        {
            if (_visualRoot == null) return;
            var poly = _visualRoot.GetComponent<PolygonCollider2D>();
            if (poly == null) return;

            // Preserve isTrigger — the visual collider is a trigger so the player
            // can walk behind the sprite. Only ResourceCollider (parent) is solid.
            bool wasTrigger = poly.isTrigger;
            Destroy(poly);
            var fresh = _visualRoot.gameObject.AddComponent<PolygonCollider2D>();
            fresh.isTrigger = wasTrigger;
        }

        private void RegisterWithWind()
        {
            if (Flynn.World.WindManager.Instance == null) return;
            if (_config != null && !_config.swaysInWind) return;

            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
                Flynn.World.WindManager.Instance.Register(sr, transform.position, Weight);
        }

        private void OnEnable()
        {
            if (GameEventBus.Instance != null)
                GameEventBus.Instance.Subscribe<ResourceHit>(OnResourceHit);
        }

        private void OnDisable()
        {
            if (GameEventBus.Instance != null)
                GameEventBus.Instance.Unsubscribe<ResourceHit>(OnResourceHit);
        }

        private void OnResourceHit(ResourceHit evt)
        {
            if (evt.Node != this) return;

            float weight = _config != null ? _config.weight : 10f;
            float torque = -evt.HitDirection.x * 30f / weight;
            AddVisualTorque(torque);

            // Per-hit white flash (StagePop already flashes on stage swaps).
            if (_stagePopRoutine == null && _mainSpriteRenderer != null)
            {
                if (_hitFlashRoutine != null) StopCoroutine(_hitFlashRoutine);
                _hitFlashRoutine = StartCoroutine(HitFlash());
            }

            Hit(evt.Damage);
        }

        private Coroutine _hitFlashRoutine;

        private System.Collections.IEnumerator HitFlash()
        {
            var mat = _mainSpriteRenderer.material;
            const float duration = 0.12f;
            float t = 0f;
            mat.SetFloat(FlashAmountID, 1f);
            while (t < duration)
            {
                t += Time.deltaTime;
                mat.SetFloat(FlashAmountID, 1f - Mathf.Clamp01(t / duration));
                yield return null;
            }
            mat.SetFloat(FlashAmountID, 0f);
            _hitFlashRoutine = null;
        }

        // ── Interaction prompt is handled by the Interactable component ──

        private static void Publish<T>(T evt) where T : struct
        {
            if (GameEventBus.Instance != null) GameEventBus.Instance.Publish(evt);
        }

        public void Hit() => Hit(1);

        /// <summary>Apply damage points (charged swings/throws deal more than 1).</summary>
        public void Hit(int damage)
        {
            if (_isDepleted || _config == null) return;

            damage = Mathf.Max(1, damage);
            _currentHealth = Mathf.Max(0, _currentHealth - damage);
            float healthFraction = (float)_currentHealth / _config.maxHealth;

            RaiseHit(_currentHealth, healthFraction);
            Publish(new ResourceDamaged(this, damage, _currentHealth));

            UpdateStageSprite(healthFraction);

            if (_config.echoShardItem != null && Random.value < _config.echoShardChancePerHit)
            {
                Vector3 pos = ScatterPos();
                WorldItemSpawner.SpawnArcToPlayer(_config.echoShardItem, 1, pos);
                Publish(new ResourceDropSpawned(_config.echoShardItem, 1, pos));
            }

            if (_currentHealth <= 0)
            {
                SpawnDrops();
                Publish(new ResourceDepleted(this));
                RaiseDeath();
                _isDepleted = true;
                Destroy(gameObject);
            }
        }

        private void SpawnDrops()
        {
            if (_config == null) return;
            foreach (var drop in _config.drops)
            {
                if (drop.item == null || Random.value > drop.dropChance) continue;

                int count = Random.Range(drop.minCount, drop.maxCount + 1);
                Vector3 pos = transform.position;
                WorldItemSpawner.SpawnArcToPlayer(drop.item, count, pos);
                Publish(new ResourceDropSpawned(drop.item, count, pos));
            }
        }

        private Vector3 ScatterPos() => transform.position;

        [Header("Stage Transition")]
        [Tooltip("Cross-fade duration between stage sprites. 0 = instant swap.")]
        [SerializeField] private float _stageFadeDuration = 0.2f;

        private Coroutine _stageTransitionRoutine;

        // ── Health stage sprite swap (cross-fade) ──────────────────────────

        private void UpdateStageSprite(float healthFraction)
        {
            if (_config == null || _mainSpriteRenderer == null) return;

            Sprite target = _config.sprite;

            if (_config.healthStages != null)
            {
                for (int i = 0; i < _config.healthStages.Length; i++)
                {
                    if (healthFraction >= _config.healthStages[i].healthThreshold)
                    {
                        target = _config.healthStages[i].sprite;
                        break;
                    }
                }
            }

            if (target == null || target == _mainSpriteRenderer.sprite) return;

            bool isStageChange = _mainSpriteRenderer.sprite != null;

            if (isStageChange)
            {
                if (_stageTransitionRoutine != null) StopCoroutine(_stageTransitionRoutine);
                _stageTransitionRoutine = StartCoroutine(CrossFadeStage(target));
                RefreshPolygonCollider();
            }
            else
            {
                _mainSpriteRenderer.sprite = target;
            }

            if (isStageChange && _stagePopRoutine == null)
                _stagePopRoutine = StartCoroutine(StagePop());
        }

        private System.Collections.IEnumerator CrossFadeStage(Sprite newSprite)
        {
            Sprite oldSprite = _mainSpriteRenderer.sprite;

            // Create a temporary ghost for the old sprite
            var ghostGO = new GameObject("StageGhost");
            ghostGO.transform.SetParent(_visualRoot != null ? _visualRoot : transform, false);
            ghostGO.transform.localPosition = Vector3.zero;
            ghostGO.transform.localRotation = Quaternion.identity;
            ghostGO.transform.localScale = Vector3.one;

            var ghostSR = ghostGO.AddComponent<SpriteRenderer>();
            ghostSR.sprite = oldSprite;
            ghostSR.color = _mainSpriteRenderer.color;
            ghostSR.flipX = _mainSpriteRenderer.flipX;
            ghostSR.flipY = _mainSpriteRenderer.flipY;
            ghostSR.sortingOrder = _mainSpriteRenderer.sortingOrder + 1;
            ghostSR.material = _mainSpriteRenderer.material;

            // Set new sprite on main renderer, start at 0 alpha
            _mainSpriteRenderer.sprite = newSprite;
            Color mainColor = _mainSpriteRenderer.color;
            Color mainStart = new Color(mainColor.r, mainColor.g, mainColor.b, 0f);
            _mainSpriteRenderer.color = mainStart;

            float t = 0f;
            while (t < _stageFadeDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / _stageFadeDuration);

                // Fade in new sprite
                _mainSpriteRenderer.color = Color.Lerp(mainStart, mainColor, p);

                // Fade out ghost
                var gc = ghostSR.color;
                gc.a = 1f - p;
                ghostSR.color = gc;

                // Shrink ghost slightly
                ghostGO.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.8f, p);

                yield return null;
            }

            // Finalize
            _mainSpriteRenderer.color = mainColor;
            Destroy(ghostGO);
            _stageTransitionRoutine = null;
        }

        private System.Collections.IEnumerator StagePop()
        {
            if (_visualRoot == null) yield break;

            var mat = _mainSpriteRenderer.material;
            mat.SetFloat(FlashAmountID, 1f);

            float duration = 0.2f;
            float t = 0f;
            Vector3 baseScale = _visualRoot.localScale;
            Vector3 popScale = baseScale * 1.15f;
            _visualRoot.localScale = popScale;

            while (t < duration)
            {
                t += Time.deltaTime;
                float lerp = t / duration;
                _visualRoot.localScale = Vector3.Lerp(popScale, baseScale, lerp);
                mat.SetFloat(FlashAmountID, 1f - lerp);
                yield return null;
            }

            _visualRoot.localScale = baseScale;
            mat.SetFloat(FlashAmountID, 0f);
            _stagePopRoutine = null;
        }

        [Header("Events")]

        [Tooltip("Fired once when this node spawns.")]
        [SerializeField] private UnityEvent _onSpawn = new UnityEvent();

        [Tooltip("Fired on every hit that deals damage. Arg = remaining health (int).")]
        [SerializeField] private IntEvent _onHit = new IntEvent();

        [Tooltip("Fired on every hit. Arg = remaining health as 0–1 fraction.")]
        [SerializeField] private FloatEvent _onHealthChanged = new FloatEvent();

        [Tooltip("Fired when health drops to zero, before the node is destroyed.")]
        [SerializeField] private UnityEvent _onDeath = new UnityEvent();

        [Tooltip("Fired when the wrong tool is used on this node.")]
        [SerializeField] private UnityEvent _onWrongTool = new UnityEvent();

        private void RaiseSpawn() => _onSpawn?.Invoke();
        private void RaiseHit(int remainingHealth, float health01)
        {
            _onHit?.Invoke(remainingHealth);
            _onHealthChanged?.Invoke(health01);
        }
        private void RaiseDeath() => _onDeath?.Invoke();

        [System.Serializable] public class IntEvent   : UnityEvent<int>   { }
        [System.Serializable] public class FloatEvent : UnityEvent<float> { }

        // ── Visual helpers ──

        public void AddVisualForceX(float force)
        {
            if (_visualRigidbody != null)
                _visualRigidbody.AddForce(new Vector2(force, 0f), ForceMode2D.Impulse);
        }

        public void AddVisualForceY(float force)
        {
            if (_visualRigidbody != null)
                _visualRigidbody.AddForce(new Vector2(0f, force), ForceMode2D.Impulse);
        }

        public void AddVisualTorque(float torque)
        {
            if (_visualRigidbody != null)
                _visualRigidbody.AddTorque(torque, ForceMode2D.Impulse);
        }
    }
}
