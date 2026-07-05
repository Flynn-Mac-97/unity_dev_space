using System.Collections;
using UnityEngine;

namespace Flynn.Player
{
    /// <summary>
    /// Demo script that plays each animation state for a set duration while
    /// smoothly rotating through all 16 directions. Disables PlayerController2D.
    /// Press Space to start/stop.
    /// </summary>
    public class PlayerAnimationDemo : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Seconds per animation state (directions rotate within this time).")]
        [SerializeField] private float _stateDuration = 4f;

        private Animator _animator;
        private PlayerController2D _controller;
        private Coroutine _demoRoutine;
        private bool _running;
        private string _currentState;
        private int _currentDir;

        // 16 unit-circle directions (dir00..dir15)
        private static readonly Vector2[] Directions =
        {
            new Vector2(0f, -1f),       // 0  Down
            new Vector2(0.383f, -0.924f),
            new Vector2(0.707f, -0.707f),
            new Vector2(0.924f, -0.383f),
            new Vector2(1f, 0f),         // 4  Right
            new Vector2(0.924f, 0.383f),
            new Vector2(0.707f, 0.707f),
            new Vector2(0.383f, 0.924f),
            new Vector2(0f, 1f),         // 8  Up
            new Vector2(-0.383f, 0.924f),
            new Vector2(-0.707f, 0.707f),
            new Vector2(-0.924f, 0.383f),
            new Vector2(-1f, 0f),        // 12 Left
            new Vector2(-0.924f, -0.383f),
            new Vector2(-0.707f, -0.707f),
            new Vector2(-0.383f, -0.924f),
        };

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _controller = GetComponent<PlayerController2D>();
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Space)) return;
            if (_running) Stop();
            else StartDemo();
        }

        public void StartDemo()
        {
            if (_running) return;
            _running = true;
            if (_controller != null) _controller.enabled = false;
            _demoRoutine = StartCoroutine(DemoSequence());
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;
            if (_demoRoutine != null) StopCoroutine(_demoRoutine);
            _demoRoutine = null;
            ResetAnimator();
            if (_controller != null) _controller.enabled = true;
        }

        private IEnumerator DemoSequence()
        {
            // ── Idle ──
            _currentState = "Idle";
            yield return PlayStateRotating(speed: 0f);

            // ── Run ──
            _currentState = "Run";
            yield return PlayStateRotating(speed: 1f);

            // ── Swim ──
            _currentState = "Swim";
            yield return PlayStateRotating(speed: 1f, boolParam: "Swimming");

            // ── GrappleFly ──
            _currentState = "GrappleFly";
            yield return PlayStateRotating(speed: 1f, boolParam: "Grappling");

            // ── CarryHeavy ──
            _currentState = "CarryHeavy";
            yield return PlayStateRotating(speed: 0f, boolParam: "Carrying");

            // ── Jump ──
            _currentState = "Jump";
            yield return PlayStateRotating(speed: 0f, trigger: "Jump");

            // ── Throw ──
            _currentState = "Throw";
            yield return PlayStateRotating(speed: 0f, trigger: "Throw");

            Stop();
        }

        /// <summary>
        /// Plays one animation state for _stateDuration seconds while smoothly
        /// rotating MoveX/MoveY through all 16 directions.
        /// </summary>
        private IEnumerator PlayStateRotating(float speed, string boolParam = null, string trigger = null)
        {
            // Reset all bools
            _animator.SetBool("Swimming", false);
            _animator.SetBool("Grappling", false);
            _animator.SetBool("Carrying", false);

            if (boolParam != null)
                _animator.SetBool(boolParam, true);

            _animator.SetFloat("Speed", speed);

            if (trigger != null)
                _animator.SetTrigger(trigger);

            float elapsed = 0f;
            while (elapsed < _stateDuration)
            {
                float t = elapsed / _stateDuration;
                // Smoothly sweep full circle: 0 → 15 → 0
                float angle = t * Directions.Length;
                int i0 = Mathf.FloorToInt(angle) % Directions.Length;
                int i1 = (i0 + 1) % Directions.Length;
                float blend = angle - Mathf.Floor(angle);

                Vector2 dir = Vector2.Lerp(Directions[i0], Directions[i1], blend).normalized;
                _currentDir = i0;
                SetDirection(dir);

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (boolParam != null)
                _animator.SetBool(boolParam, false);
        }

        private void SetDirection(Vector2 dir)
        {
            _animator.SetFloat("MoveX", dir.x);
            _animator.SetFloat("MoveY", dir.y);
        }

        private void ResetAnimator()
        {
            _animator.SetBool("Swimming", false);
            _animator.SetBool("Grappling", false);
            _animator.SetBool("Carrying", false);
            _animator.SetFloat("Speed", 0f);
            SetDirection(Vector2.down);
        }

        private void OnGUI()
        {
            if (!_running || string.IsNullOrEmpty(_currentState)) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            // Shadow
            style.normal.textColor = new Color(0, 0, 0, 0.8f);
            GUI.Label(new Rect(Screen.width / 2 - 151, 19, 302, 42), _currentState, style);

            // Text
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width / 2 - 150, 20, 300, 40), _currentState, style);

            // Hint
            style.fontSize = 14;
            style.normal.textColor = new Color(1, 1, 1, 0.6f);
            GUI.Label(new Rect(Screen.width / 2 - 100, 60, 200, 20), "Space to stop", style);
        }
    }
}
