using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Flynn.Core;
using Flynn.Events;

namespace Flynn.Resources
{
    /// <summary>
    /// Spawns debris sprites on ResourceHit events. Each hit bursts a few scrap
    /// piece sprites with random velocity, gravity, and rotation that fade out.
    /// Assign to a ResourceNode and populate _debrisSprites with the particle sprites.
    /// </summary>
    public class HitDebrisBurst : MonoBehaviour
    {
        [Header("Debris")]
        [Tooltip("Sprites randomly chosen for each debris piece.")]
        [SerializeField] private Sprite[] _debrisSprites;

        [Tooltip("How many debris pieces spawn per hit.")]
        [SerializeField] private int _countPerHit = 4;

        [Tooltip("Random upward velocity range.")]
        [SerializeField] private float _burstForce = 3f;

        [Tooltip("Random horizontal spread.")]
        [SerializeField] private float _spread = 2f;

        [Tooltip("Gravity applied to debris.")]
        [SerializeField] private float _gravity = 8f;

        [Tooltip("How long debris lives before fading.")]
        [SerializeField] private float _lifetime = 0.8f;

        [Tooltip("How long the fade-out takes.")]
        [SerializeField] private float _fadeDuration = 0.3f;

        [Tooltip("Base scale of debris sprites.")]
        [SerializeField] private float _debrisScale = 0.3f;

        [Tooltip("Sorting order offset from the resource's main sprite.")]
        [SerializeField] private int _sortingOrderOffset = 50;

        [Tooltip("Hit direction influences debris arc. If false, debris flies randomly.")]
        [SerializeField] private bool _useHitDirection = true;

        [Header("Target")]
        [Tooltip("Which ResourceNode to listen to. If null, uses GetComponent.")]
        [SerializeField] private ResourceNode _targetNode;

        private ResourceNode _node;

        private void Awake()
        {
            _node = _targetNode != null ? _targetNode : GetComponent<ResourceNode>();
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
            if (_node == null || evt.Node != _node) return;
            if (_debrisSprites == null || _debrisSprites.Length == 0) return;

            Vector2 hitDir = _useHitDirection ? evt.HitDirection : Random.insideUnitCircle.normalized;
            SpawnDebris(hitDir);
        }

        private void SpawnDebris(Vector2 hitDir)
        {
            int baseSortingOrder = 0;
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) baseSortingOrder = sr.sortingOrder;

            for (int i = 0; i < _countPerHit; i++)
            {
                Sprite sprite = _debrisSprites[Random.Range(0, _debrisSprites.Length)];
                if (sprite == null) continue;

                var go = new GameObject("Debris");
                go.transform.SetParent(transform, false);
                go.transform.position = transform.position + (Vector3)(Random.insideUnitCircle * 0.15f);
                go.transform.localScale = Vector3.one * (_debrisScale * Random.Range(0.7f, 1.3f));
                go.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

                var debrisSR = go.AddComponent<SpriteRenderer>();
                debrisSR.sprite = sprite;
                debrisSR.sortingOrder = baseSortingOrder + _sortingOrderOffset;
                debrisSR.color = Color.white;

                StartCoroutine(DebrisLife(go, debrisSR, hitDir));
            }
        }

        private IEnumerator DebrisLife(GameObject go, SpriteRenderer sr, Vector2 hitDir)
        {
            // Physics: initial burst velocity
            Vector2 vel = new Vector2(
                -hitDir.x * _spread * Random.Range(0.5f, 1.5f) + Random.Range(-1f, 1f),
                _burstForce * Random.Range(0.7f, 1.3f));

            float spin = Random.Range(-360f, 360f);
            float alive = 0f;

            while (alive < _lifetime)
            {
                alive += Time.deltaTime;

                // Apply gravity
                vel.y -= _gravity * Time.deltaTime;

                // Move
                go.transform.position += (Vector3)(vel * Time.deltaTime);

                // Spin
                go.transform.Rotate(0, 0, spin * Time.deltaTime);

                yield return null;
            }

            // Fade out
            float fadeT = 0f;
            Color startColor = sr.color;
            while (fadeT < _fadeDuration)
            {
                fadeT += Time.deltaTime;
                float a = 1f - (fadeT / _fadeDuration);
                sr.color = new Color(startColor.r, startColor.g, startColor.b, a);
                go.transform.localScale *= 0.97f; // shrink slightly
                yield return null;
            }

            Destroy(go);
        }
    }
}
