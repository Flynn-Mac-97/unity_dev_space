using System.Globalization;
using System.IO;
using UnityEngine;
using Flynn.Core;
using Flynn.Events;

namespace Flynn.Debugging
{
    /// <summary>
    /// Records a play session to telemetry/feel_&lt;timestamp&gt;.csv for offline
    /// feel analysis: per-frame player position/speed/battery plus timestamped
    /// input and game events (swing press, hit, break, pickup, jump).
    /// Editor tooling only — no gameplay effect. F9 toggles recording.
    /// </summary>
    public class FeelTelemetry : MonoBehaviour
    {
        [SerializeField] private bool _recordOnPlay = true;
        [Tooltip("Position samples per second (events are always exact-time).")]
        [SerializeField] private float _sampleRate = 30f;

        private StreamWriter _w;
        private Transform _player;
        private Vector3 _lastPos;
        private float _nextSample;
        private bool _subscribed;

        private void Start()
        {
            var playerGO = GameObject.Find("Player");
            _player = playerGO != null ? playerGO.transform : null;
            if (_recordOnPlay) Begin();
            TrySubscribe();
        }

        private void OnEnable() => TrySubscribe();

        private void TrySubscribe()
        {
            if (_subscribed || GameEventBus.Instance == null) return;
            var bus = GameEventBus.Instance;
            bus.Subscribe<ToolSwingStarted>(OnSwing);
            bus.Subscribe<ResourceDamaged>(OnDamaged);
            bus.Subscribe<ResourceDepleted>(OnDepleted);
            bus.Subscribe<ItemPickedUp>(OnPickup);
            bus.Subscribe<PlayerJumped>(OnJump);
            bus.Subscribe<PlayerLanded>(OnLand);
            bus.Subscribe<BatteryChanged>(OnBattery);
            _subscribed = true;
        }

        private void Begin()
        {
            if (_w != null) return;
            string dir = Path.Combine(Application.dataPath, "..", "telemetry");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"feel_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
            _w = new StreamWriter(path, false);
            _w.WriteLine("type,t,x,y,speed,extra");
            Debug.Log("[FeelTelemetry] Recording to " + path);
        }

        private void End()
        {
            if (_w == null) return;
            _w.Flush();
            _w.Close();
            _w = null;
            Debug.Log("[FeelTelemetry] Recording stopped.");
        }

        private void OnDisable() => End();
        private void OnDestroy() => End();

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (_w == null) Begin();
                else End();
            }
            if (_w == null) return;

            if (Input.GetMouseButtonDown(0)) Row("press_lmb");
            if (Input.GetMouseButtonDown(1)) Row("press_rmb");
            if (Input.GetKeyDown(KeyCode.Space)) Row("press_jump");
            if (Input.GetKeyDown(KeyCode.E)) Row("press_e");

            if (Time.unscaledTime >= _nextSample && _player != null)
            {
                _nextSample = Time.unscaledTime + 1f / Mathf.Max(1f, _sampleRate);
                float speed = Time.deltaTime > 0f
                    ? ((_player.position - _lastPos).magnitude / Time.deltaTime) : 0f;
                _lastPos = _player.position;
                Row("pos", speed.ToString("F3", CultureInfo.InvariantCulture));
            }
        }

        private void Row(string type, string extra = "")
        {
            if (_w == null) return;
            Vector3 p = _player != null ? _player.position : Vector3.zero;
            _w.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1:F4},{2:F3},{3:F3},,{4}", type, Time.unscaledTime, p.x, p.y, extra));
        }

        private void OnSwing(ToolSwingStarted e) => Row("swing_hit_fired");
        private void OnDamaged(ResourceDamaged e) => Row("node_damaged");
        private void OnDepleted(ResourceDepleted e) => Row("node_break");
        private void OnPickup(ItemPickedUp e) => Row("pickup", e.Item != null ? e.Item.itemId : "");
        private void OnJump(PlayerJumped e) => Row("jump");
        private void OnLand(PlayerLanded e) => Row("land");
        private void OnBattery(BatteryChanged e) => Row("battery", e.Current.ToString());
    }
}
