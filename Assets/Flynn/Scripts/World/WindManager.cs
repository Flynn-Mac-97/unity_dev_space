using System.Collections.Generic;
using UnityEngine;

namespace Flynn.World
{
    /// <summary>
    /// Singleton that drives global wind shader properties.
    /// Place on a GameObject under MANAGERS. Tune wind in the Inspector at runtime.
    /// FloraRuntimeManager batch-registers spawned sprites via Register().
    /// </summary>
    public class WindManager : MonoBehaviour
    {
        public static WindManager Instance { get; private set; }

        [Header("Wind")]
        [Tooltip("How far the top of the sprite sways (object-space units).")]
        [SerializeField] private float _strength = 0.05f;
        [Tooltip("Sway cycles per second.")]
        [SerializeField] private float _speed = 2f;
        [Tooltip("Wind direction in XY. Normalized in shader.")]
        [SerializeField] private Vector2 _direction = new Vector2(1f, 0f);

        private static readonly int WindStrengthID = Shader.PropertyToID("_WindStrength");
        private static readonly int WindSpeedID = Shader.PropertyToID("_WindSpeed");
        private static readonly int WindDirID = Shader.PropertyToID("_WindDirection");
        private static readonly int WindWeightID = Shader.PropertyToID("_WindWeight");
        private static readonly int WindPivotID = Shader.PropertyToID("_WindPivot");

        private void Awake() => Instance = this;

        private void Update()
        {
            Shader.SetGlobalFloat(WindStrengthID, _strength);
            Shader.SetGlobalFloat(WindSpeedID, _speed);
            Shader.SetGlobalVector(WindDirID, _direction);
        }

        /// <summary>
        /// Registers a sprite for wind with per-sprite phase and weight.
        /// worldPos: sprite's pivot in world space (for per-sprite phase variation).
        /// weight: ResourceNodeConfig.weight (higher = stiffer, less sway).
        /// </summary>
        public void Register(SpriteRenderer sr, Vector3 worldPos, float weight = 1f)
        {
            if (sr == null) return;

            float windWeight = Mathf.Clamp01(1f / Mathf.Max(1f, weight));

            var mpb = new MaterialPropertyBlock();
            sr.GetPropertyBlock(mpb);
            mpb.SetFloat(WindWeightID, windWeight);
            mpb.SetVector(WindPivotID, worldPos);
            sr.SetPropertyBlock(mpb);
        }

        public void Unregister(SpriteRenderer sr)
        {
            if (sr == null) return;
            sr.SetPropertyBlock(null);
        }
    }
}
