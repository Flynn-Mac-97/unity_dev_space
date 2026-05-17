using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class PrototypeActionFeedbackSystem : MonoBehaviour
{
    public enum FeedbackTone
    {
        Info,
        Success,
        Warning,
        Error
    }

    private readonly struct FeedbackRequest
    {
        public readonly FeedbackTone Tone;
        public readonly Transform Target;

        public FeedbackRequest(FeedbackTone tone, Transform target)
        {
            Tone = tone;
            Target = target;
        }
    }

    public static PrototypeActionFeedbackSystem Instance { get; private set; }

    [Header("Prototype Demo")]
    [SerializeField] private bool playDemoAfterIntro = true;
    [SerializeField] private float demoDelayAfterIntroSeconds = 0.45f;
    [SerializeField] private FeedbackTone demoTone = FeedbackTone.Success;

    [Header("Debug")]
    [SerializeField] private bool allowDebugTriggerKey = true;
    [SerializeField] private KeyCode debugTriggerKey = KeyCode.F;
    [SerializeField] private Transform debugFeedbackTarget;
    [SerializeField] private bool autoFindPrototypeRunnerTarget = true;

    [Header("Timing")]
    [SerializeField] private float fadeInSeconds = 0.10f;
    [SerializeField] private float holdSeconds = 0.90f;
    [SerializeField] private float fadeOutSeconds = 0.55f;
    [SerializeField] private float risePixels = 34f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI feedbackLabel;
    [SerializeField] private RectTransform feedbackRect;
    [SerializeField] private CanvasGroup feedbackGroup;
    [SerializeField] private Canvas feedbackCanvas;

    [Header("World Space")]
    [SerializeField] private bool keepCanvasWorldSpace = true;
    [SerializeField] private Transform followTarget;
    [SerializeField] private bool autoFindIntroSequencePlayer = true;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.8f, 0f);
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private float worldCanvasScale = 0.0025f;

    [Header("Appearance")]
    [SerializeField] private Color infoColor = new Color(0.88f, 0.95f, 1f, 1f);
    [SerializeField] private Color successColor = new Color(0.70f, 1f, 0.78f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.86f, 0.46f, 1f);
    [SerializeField] private Color errorColor = new Color(1f, 0.48f, 0.48f, 1f);

    private readonly Queue<FeedbackRequest> _queue = new Queue<FeedbackRequest>();

    private RectTransform _feedbackRect;
    private TextMeshProUGUI _feedbackLabel;
    private CanvasGroup _group;
    private Canvas _canvas;
    private Coroutine _processRoutine;
    private Vector2 _baseAnchoredPosition;
    private Transform _activeRequestTarget;

    public static void Show(FeedbackTone tone = FeedbackTone.Info)
    {
        if (Instance == null)
            return;

        Instance.ShowFeedback(tone);
    }

    public static void Show(Transform target, FeedbackTone tone = FeedbackTone.Info)
    {
        if (Instance == null)
            return;

        Instance.ShowFeedback(target, tone);
    }

    public void ShowFeedback(FeedbackTone tone = FeedbackTone.Info)
    {
        ShowFeedback(null, tone);
    }

    public void ShowFeedback(Transform target, FeedbackTone tone = FeedbackTone.Info)
    {
        _queue.Enqueue(new FeedbackRequest(tone, target));
        if (_processRoutine == null)
            _processRoutine = StartCoroutine(ProcessQueue());
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
        if (_feedbackLabel == null || _feedbackRect == null || _group == null || _canvas == null)
        {
            Debug.LogError("PrototypeActionFeedbackSystem requires a child TextMeshProUGUI with RectTransform, CanvasGroup, and parent Canvas.", this);
            enabled = false;
            return;
        }

        if (keepCanvasWorldSpace)
            _canvas.renderMode = RenderMode.WorldSpace;

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (_canvas.renderMode == RenderMode.WorldSpace)
        {
            if (worldCanvasScale > 0f)
                _canvas.transform.localScale = Vector3.one * worldCanvasScale;

            if (worldCamera != null)
                _canvas.worldCamera = worldCamera;
        }

        if (followTarget == null)
            followTarget = ResolveFollowTarget();

        _baseAnchoredPosition = _feedbackRect.anchoredPosition;
        _group.alpha = 0f;

        if (playDemoAfterIntro)
            StartCoroutine(PlaySceneDemoAfterIntro());
    }

    private void Update()
    {
        if (allowDebugTriggerKey && Input.GetKeyDown(debugTriggerKey))
            ShowFeedback(ResolveDebugFeedbackTarget(), FeedbackTone.Info);
    }

    private void LateUpdate()
    {
        UpdateCanvasWorldAnchor();
    }

    private IEnumerator PlaySceneDemoAfterIntro()
    {
        IntroSequencePlayer intro = FindFirstObjectByType<IntroSequencePlayer>();
        while (intro != null && intro.IsSequencePlaying)
            yield return null;

        if (demoDelayAfterIntroSeconds > 0f)
            yield return Wait(demoDelayAfterIntroSeconds);

        ShowFeedback(demoTone);
    }

    private IEnumerator ProcessQueue()
    {
        while (_queue.Count > 0)
        {
            FeedbackRequest request = _queue.Dequeue();
            yield return AnimateRequest(request);
        }

        _processRoutine = null;
    }

    private IEnumerator AnimateRequest(FeedbackRequest request)
    {
        _activeRequestTarget = request.Target;
        _feedbackLabel.color = GetToneColor(request.Tone);

        Vector2 startPos = _baseAnchoredPosition;
        Vector2 endPos = _baseAnchoredPosition + new Vector2(0f, risePixels);

        _feedbackRect.anchoredPosition = startPos;
        _group.alpha = 0f;

        float inDuration = Mathf.Max(0.01f, fadeInSeconds);
        float elapsed = 0f;
        while (elapsed < inDuration)
        {
            float t = Mathf.Clamp01(elapsed / inDuration);
            _group.alpha = t;
            _feedbackRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t * 0.45f);

            elapsed += DeltaTime();
            yield return null;
        }

        _group.alpha = 1f;

        if (holdSeconds > 0f)
            yield return Wait(holdSeconds);

        float outDuration = Mathf.Max(0.01f, fadeOutSeconds);
        elapsed = 0f;
        while (elapsed < outDuration)
        {
            float t = Mathf.Clamp01(elapsed / outDuration);
            float eased = t * t;
            _group.alpha = 1f - eased;
            _feedbackRect.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);

            elapsed += DeltaTime();
            yield return null;
        }

        _group.alpha = 0f;
        _feedbackRect.anchoredPosition = startPos;
        _activeRequestTarget = null;
    }

    private Color GetToneColor(FeedbackTone tone)
    {
        switch (tone)
        {
            case FeedbackTone.Success:
                return successColor;
            case FeedbackTone.Warning:
                return warningColor;
            case FeedbackTone.Error:
                return errorColor;
            default:
                return infoColor;
        }
    }

    private void ResolveReferences()
    {
        _feedbackLabel = feedbackLabel != null ? feedbackLabel : GetComponentInChildren<TextMeshProUGUI>(true);
        _feedbackRect = feedbackRect != null ? feedbackRect : (_feedbackLabel != null ? _feedbackLabel.rectTransform : null);
        _group = feedbackGroup != null ? feedbackGroup : (_feedbackLabel != null ? _feedbackLabel.GetComponent<CanvasGroup>() : null);
        _canvas = feedbackCanvas != null ? feedbackCanvas : (_feedbackLabel != null ? _feedbackLabel.GetComponentInParent<Canvas>() : null);

        if (_group == null && _feedbackLabel != null)
            _group = _feedbackLabel.gameObject.AddComponent<CanvasGroup>();
    }

    private Transform ResolveFollowTarget()
    {
        if (!autoFindIntroSequencePlayer)
            return followTarget;

        IntroSequencePlayer introPlayer = FindFirstObjectByType<IntroSequencePlayer>(FindObjectsInactive.Include);
        return introPlayer != null ? introPlayer.transform : followTarget;
    }

    private Transform ResolveDebugFeedbackTarget()
    {
        if (debugFeedbackTarget != null)
            return debugFeedbackTarget;

        if (autoFindPrototypeRunnerTarget)
        {
            SimpleSquareRunner2D prototypeRunner = FindFirstObjectByType<SimpleSquareRunner2D>(FindObjectsInactive.Include);
            if (prototypeRunner != null)
                return prototypeRunner.transform;
        }

        GameObject taggedPlayer = GameObject.FindWithTag("Player");
        if (taggedPlayer != null)
            return taggedPlayer.transform;

        if (followTarget == null)
            followTarget = ResolveFollowTarget();

        return followTarget;
    }

    private void UpdateCanvasWorldAnchor()
    {
        if (_canvas == null || _canvas.renderMode != RenderMode.WorldSpace)
            return;

        Transform target = _activeRequestTarget;
        if (target == null)
        {
            if (followTarget == null)
                followTarget = ResolveFollowTarget();

            target = followTarget;
        }

        if (target == null)
            return;

        Transform canvasTransform = _canvas.transform;
        canvasTransform.position = target.position + worldOffset;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
            return;

        _canvas.worldCamera = cam;

        if (!faceCamera)
            return;

        Vector3 toCamera = canvasTransform.position - cam.transform.position;
        if (toCamera.sqrMagnitude > 0.0001f)
            canvasTransform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
    }

    private float DeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private IEnumerator Wait(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += DeltaTime();
            yield return null;
        }
    }
}
