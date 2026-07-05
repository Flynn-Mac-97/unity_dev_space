using UnityEngine;
using UnityEngine.UIElements;
using Flynn.Core;
using Flynn.Events;
using Flynn.Interactables;

namespace Flynn.Tutorial
{
    /// <summary>
    /// UI Toolkit overlay for scanning: shows a progress bar while scanning
    /// and displays info/lore lines when the scan completes.
    /// Also shows the signal relay activation message.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ScanUIController : MonoBehaviour
    {
        private VisualElement _root;
        private VisualElement _progressPanel;
        private Label _titleLabel;
        private VisualElement _progressFill;
        private VisualElement _resultPanel;
        private Label _resultLabel;
        private float _progressHideTimer;
        private float _resultHideTimer;
        private const float k_ProgressHideDelay = 1.5f;
        private const float k_ResultDisplayDuration = 8f;

        private void Start()
        {
            var doc = GetComponent<UIDocument>();
            if (doc == null) { Debug.LogError("[ScanUI] No UIDocument"); return; }
            _root = doc.rootVisualElement;
            BuildUi();
        }

        private void OnEnable()
        {
            var bus = GameEventBus.Instance;
            if (bus == null) return;
            bus.Subscribe<ScanStarted>(OnScanStarted);
            bus.Subscribe<ScanProgressed>(OnScanProgressed);
            bus.Subscribe<ScanRevealed>(OnScanRevealed);
            bus.Subscribe<ScanCompleted>(OnScanCompleted);
        }

        private void OnDisable()
        {
            var bus = GameEventBus.Instance;
            if (bus == null) return;
            bus.Unsubscribe<ScanStarted>(OnScanStarted);
            bus.Unsubscribe<ScanProgressed>(OnScanProgressed);
            bus.Unsubscribe<ScanRevealed>(OnScanRevealed);
            bus.Unsubscribe<ScanCompleted>(OnScanCompleted);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_progressHideTimer > 0f)
            {
                _progressHideTimer -= dt;
                if (_progressHideTimer <= 0f && _progressPanel != null)
                    _progressPanel.style.display = DisplayStyle.None;
            }

            if (_resultHideTimer > 0f)
            {
                _resultHideTimer -= dt;
                if (_resultHideTimer <= 0f && _resultPanel != null)
                    _resultPanel.style.display = DisplayStyle.None;
            }
        }

        // ── Scan events ──

        private void OnScanStarted(ScanStarted evt)
        {
            if (_progressPanel == null) return;
            _progressPanel.style.display = DisplayStyle.Flex;
            _progressFill.style.width = 0f;
            _titleLabel.text = evt.Target != null && evt.Target.Config != null
                ? "Scanning: " + evt.Target.Config.displayName
                : "Scanning...";
        }

        private void OnScanProgressed(ScanProgressed evt)
        {
            if (_progressFill != null)
                _progressFill.style.width = new Length(evt.Percent * 100f, LengthUnit.Percent);
        }

        private void OnScanCompleted(ScanCompleted evt)
        {
            if (_progressFill != null)
                _progressFill.style.width = new Length(100f, LengthUnit.Percent);
            if (_titleLabel != null)
                _titleLabel.text = "Scan Complete";
            _progressHideTimer = k_ProgressHideDelay;
        }

        private void OnScanRevealed(ScanRevealed evt)
        {
            if (_resultLabel == null || _resultPanel == null) return;

            var sb = new System.Text.StringBuilder();
            if (evt.Lines != null)
            {
                for (int i = 0; i < evt.Lines.Length; i++)
                {
                    if (i > 0) sb.Append('\n');
                    sb.Append(evt.Lines[i]);
                }
            }
            _resultLabel.text = sb.ToString();
            _resultPanel.style.display = DisplayStyle.Flex;
            _resultHideTimer = k_ResultDisplayDuration;
        }

        // ── Public API for other systems (e.g. SignalRelay) ──

        public void ShowMessage(string text, float duration = 8f)
        {
            if (_resultLabel == null || _resultPanel == null) return;
            _resultLabel.text = text;
            _resultPanel.style.display = DisplayStyle.Flex;
            _resultHideTimer = duration;
        }

        // ── UI construction ──

        private void BuildUi()
        {
            // ── Progress panel ──
            _progressPanel = new VisualElement { name = "scan-progress-panel" };
            _progressPanel.style.position = Position.Absolute;
            _progressPanel.style.left = new Length(50, LengthUnit.Percent);
            _progressPanel.style.top = new Length(15, LengthUnit.Percent);
            _progressPanel.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            _progressPanel.style.backgroundColor = new Color(0, 0, 0, 0.85f);
            _progressPanel.style.borderTopWidth = 1f;
            _progressPanel.style.borderBottomWidth = 1f;
            _progressPanel.style.borderLeftWidth = 1f;
            _progressPanel.style.borderRightWidth = 1f;
            _progressPanel.style.borderTopColor = Color.white;
            _progressPanel.style.borderBottomColor = Color.white;
            _progressPanel.style.borderLeftColor = Color.white;
            _progressPanel.style.borderRightColor = Color.white;
            _progressPanel.style.paddingTop = 12;
            _progressPanel.style.paddingBottom = 12;
            _progressPanel.style.paddingLeft = 20;
            _progressPanel.style.paddingRight = 20;
            _progressPanel.style.minWidth = 300;
            _progressPanel.style.display = DisplayStyle.None;

            _titleLabel = new Label("Scanning...");
            _titleLabel.style.color = Color.white;
            _titleLabel.style.fontSize = 16;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.marginBottom = 8;
            _progressPanel.Add(_titleLabel);

            var track = new VisualElement();
            track.style.height = 8;
            track.style.backgroundColor = new Color(1, 1, 1, 0.2f);
            _progressPanel.Add(track);

            _progressFill = new VisualElement();
            _progressFill.style.height = new Length(100, LengthUnit.Percent);
            _progressFill.style.backgroundColor = new Color(0.4f, 1f, 0.4f, 1f);
            _progressFill.style.width = 0f;
            track.Add(_progressFill);

            _root.Add(_progressPanel);

            // ── Result/message panel ──
            _resultPanel = new VisualElement { name = "scan-result-panel" };
            _resultPanel.style.position = Position.Absolute;
            _resultPanel.style.left = new Length(50, LengthUnit.Percent);
            _resultPanel.style.bottom = 120;
            _resultPanel.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            _resultPanel.style.backgroundColor = new Color(0, 0, 0, 0.9f);
            _resultPanel.style.borderTopWidth = 1f;
            _resultPanel.style.borderBottomWidth = 1f;
            _resultPanel.style.borderLeftWidth = 1f;
            _resultPanel.style.borderRightWidth = 1f;
            _resultPanel.style.borderTopColor = Color.white;
            _resultPanel.style.borderBottomColor = Color.white;
            _resultPanel.style.borderLeftColor = Color.white;
            _resultPanel.style.borderRightColor = Color.white;
            _resultPanel.style.paddingTop = 16;
            _resultPanel.style.paddingBottom = 16;
            _resultPanel.style.paddingLeft = 24;
            _resultPanel.style.paddingRight = 24;
            _resultPanel.style.maxWidth = 500;
            _resultPanel.style.minWidth = 300;
            _resultPanel.style.display = DisplayStyle.None;

            _resultLabel = new Label("");
            _resultLabel.style.color = Color.white;
            _resultLabel.style.fontSize = 18;
            _resultLabel.style.whiteSpace = WhiteSpace.Normal;
            _resultLabel.style.unityTextAlign = TextAnchor.UpperLeft;
            _resultPanel.Add(_resultLabel);

            _root.Add(_resultPanel);
        }
    }
}
