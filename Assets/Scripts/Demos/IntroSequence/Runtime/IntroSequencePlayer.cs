using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
public class IntroSequencePlayer : MonoBehaviour
{
    [Serializable]
    public class IntroTextBox
    {
        [TextArea(2, 6)]
        public string text;

        [Tooltip("Total time this text is on screen (typewriter reveal + hold). If the typewriter takes longer than this, there is no extra hold.")]
        [Min(0f)]
        public float displaySeconds = 4f;
    }

    [Header("Sequence Content")]
    [Tooltip("Add as many intro text boxes as needed. They play in order.")]
    [SerializeField] private List<IntroTextBox> textBoxes = new List<IntroTextBox>();

    [Header("Sequence Timing")]
    [Tooltip("Characters revealed per second for the typewriter effect.")]
    [Min(0.1f)]
    [FormerlySerializedAs("wordsPerSecond")]
    [SerializeField] private float charactersPerSecond = 18f;

    [Tooltip("Portion of each character step used for fade-in. 1 = full step fades, 0 = hard switch.")]
    [Range(0f, 1f)]
    [FormerlySerializedAs("perWordFadePortion")]
    [SerializeField] private float perCharacterFadePortion = 0.75f;

    [Tooltip("Subtle fade duration used between each intro text box.")]
    [Min(0f)]
    [SerializeField] private float lineFadeSeconds = 0.22f;

    [Tooltip("Full-screen fade duration after the intro finishes, revealing gameplay.")]
    [Min(0f)]
    [SerializeField] private float fadeToGameSeconds = 1.15f;

    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Skip")]
    [SerializeField] private bool allowSkip = true;
    [SerializeField] private KeyCode skipKey = KeyCode.Space;

    [Header("UI Toolkit")]
    [SerializeField] private UIDocument uiDocument;
    [Tooltip("Optional fallback layout used when the bound UIDocument does not contain intro elements.")]
    [SerializeField] private VisualTreeAsset introLayoutAsset;
    [Tooltip("Optional font override for intro labels (story text + skip hint).")]
    [SerializeField] private Font introFont;
    [SerializeField] private bool rebuildFromLayoutIfBindingsMissing = true;
    [SerializeField] private int compactModeHeightThreshold = 720;

    private const string IntroRootName = "intro-sequence-root";
    private const string IntroFrameName = "intro-sequence-frame";
    private const string StoryLabelName = "intro-story-label";
    private const string SkipHintLabelName = "intro-skip-hint-label";
    private const string StoryFadedClassName = "story-faded";
    private const string FrameFadedClassName = "frame-faded";

    private Coroutine _sequenceRoutine;
    private bool _skipRequested;
    private bool _uiBound;

    private VisualElement _introRoot;
    private VisualElement _introFrame;
    private Label _storyLabel;
    private Label _skipHintLabel;

    public bool IsSequencePlaying => _sequenceRoutine != null;

    private void Reset()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (textBoxes.Count > 0)
            return;

        textBoxes.Add(new IntroTextBox { text = "Night fell before the colony could seal the outer gates. For the first time in forty-seven years, the perimeter went dark.", displaySeconds = 13f });
        textBoxes.Add(new IntroTextBox { text = "Without the relay station, the heating grid would fail by morning. The children's hab-block would be first.", displaySeconds = 11f });
        textBoxes.Add(new IntroTextBox { text = "Three volunteers stepped into the storm. They left behind photographs, coordinates, and nothing else.", displaySeconds = 11f });
        textBoxes.Add(new IntroTextBox { text = "Command tracked their transponders across the dark — three green lights edging through static toward the relay.", displaySeconds = 11f });
        textBoxes.Add(new IntroTextBox { text = "At dawn, only one signal came back.", displaySeconds = 8f });
    }

    private void Awake()
    {
        if (!playOnStart)
            return;

        if (EnsureUiBound())
            SetPanelVisible(true);
    }

    private void Start()
    {
        if (playOnStart)
            PlaySequence();
    }

    private void OnDisable()
    {
        if (_sequenceRoutine != null)
        {
            StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = null;
        }

        _skipRequested = false;

        if (_uiBound)
            SetPanelVisible(false);
    }

    private void Update()
    {
        if (!allowSkip || _sequenceRoutine == null)
            return;

        if (Input.GetKeyDown(skipKey))
            _skipRequested = true;
    }

    public void PlaySequence()
    {
        if (_sequenceRoutine != null)
            return;

        if (!EnsureUiBound())
        {
            Debug.LogWarning("[IntroSequence] UI Toolkit references are missing or invalid.");
            return;
        }

        List<IntroTextBox> activeBoxes = GetActiveTextBoxes();
        if (activeBoxes.Count == 0)
        {
            Debug.LogWarning("[IntroSequence] No intro text boxes configured.");
            return;
        }

        _skipRequested = false;
        _sequenceRoutine = StartCoroutine(RunSequence(activeBoxes));
    }

    [ContextMenu("Add Another Intro Text Box")]
    public void AddIntroTextBox()
    {
        textBoxes.Add(new IntroTextBox { text = "New intro line...", displaySeconds = 4f });
    }

    private bool EnsureUiBound()
    {
        if (_uiBound && _introRoot != null && _introFrame != null && _storyLabel != null && _skipHintLabel != null)
            return true;

        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("[IntroSequence] Missing UIDocument reference.");
            return false;
        }

        VisualElement documentRoot = uiDocument.rootVisualElement;
        if (documentRoot == null)
        {
            Debug.LogError("[IntroSequence] UIDocument rootVisualElement is null.");
            return false;
        }

        BindFromRoot(documentRoot);

        if ((_introRoot == null || _introFrame == null || _storyLabel == null || _skipHintLabel == null)
            && rebuildFromLayoutIfBindingsMissing
            && introLayoutAsset != null)
        {
            documentRoot.Clear();
            introLayoutAsset.CloneTree(documentRoot);
            BindFromRoot(documentRoot);
        }

        if (_introRoot == null || _introFrame == null || _storyLabel == null || _skipHintLabel == null)
        {
            Debug.LogError("[IntroSequence] Could not find required UI elements: intro-sequence-root, intro-sequence-frame, intro-story-label, intro-skip-hint-label.");
            return false;
        }

        _storyLabel.enableRichText = true;
        ApplyFontOverride();

        _uiBound = true;
        SetPanelVisible(false);
        return true;
    }

    private void ApplyFontOverride()
    {
        if (introFont == null)
            return;

        if (_storyLabel != null)
            _storyLabel.style.unityFont = introFont;

        if (_skipHintLabel != null)
            _skipHintLabel.style.unityFont = introFont;
    }

    private void BindFromRoot(VisualElement root)
    {
        _introRoot = root.Q<VisualElement>(IntroRootName);
        _introFrame = root.Q<VisualElement>(IntroFrameName);
        _storyLabel = root.Q<Label>(StoryLabelName);
        _skipHintLabel = root.Q<Label>(SkipHintLabelName);
    }

    private IEnumerator RunSequence(List<IntroTextBox> activeBoxes)
    {
        ApplyResponsiveClass();
        SetPanelVisible(true);

        if (_skipHintLabel != null)
        {
            _skipHintLabel.text = $"Press {skipKey} to skip";
            _skipHintLabel.EnableInClassList("skip-hint-hidden", !allowSkip);
        }

        float revealRate = Mathf.Max(0.1f, charactersPerSecond);

        _storyLabel?.EnableInClassList(StoryFadedClassName, false);
        _introFrame?.EnableInClassList(FrameFadedClassName, false);

        for (int i = 0; i < activeBoxes.Count; i++)
        {
            if (_skipRequested)
                break;

            IntroTextBox box = activeBoxes[i];

            if (i > 0 && lineFadeSeconds > 0f)
                yield return FadeStoryOutIn();

            float typewriterSeconds = CountCharactersToReveal(box.text) / revealRate;
            yield return TypeWords(box.text, revealRate);

            float holdSeconds = Mathf.Max(0f, box.displaySeconds - typewriterSeconds);
            if (holdSeconds > 0f)
                yield return WaitWithSkip(holdSeconds);
        }

        yield return FadeToGame();
        _sequenceRoutine = null;
    }

    private IEnumerator FadeToGame()
    {
        if (_introRoot == null)
        {
            SetPanelVisible(false);
            yield break;
        }

        float duration = Mathf.Max(0f, fadeToGameSeconds);
        if (duration <= 0f)
        {
            SetPanelVisible(false);
            yield break;
        }

        _introRoot.style.opacity = 1f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            _introRoot.style.opacity = 1f - eased;

            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        _introRoot.style.opacity = 0f;
        SetPanelVisible(false);
    }

    private IEnumerator TypeWords(string fullText, float revealRate)
    {
        if (_storyLabel == null)
            yield break;

        string source = fullText ?? string.Empty;
        List<string> tokens = GetCharacterTokens(source);
        string escapedSource = EscapeRichText(source);

        if (tokens.Count == 0)
        {
            _storyLabel.text = escapedSource;
            yield break;
        }

        float[] tokenOpacity = new float[tokens.Count];
        for (int i = 0; i < tokenOpacity.Length; i++)
            tokenOpacity[i] = 0f;

        // Render all words up-front as transparent so line wrapping/layout is stable.
        _storyLabel.text = BuildTokenFrame(tokens, tokenOpacity);

        float perCharacterDelay = 1f / Mathf.Max(0.1f, revealRate);
        float fadeDuration = Mathf.Max(0f, perCharacterDelay * Mathf.Clamp01(perCharacterFadePortion));
        float settleDuration = Mathf.Max(0f, perCharacterDelay - fadeDuration);

        for (int i = 0; i < tokens.Count; i++)
        {
            if (_skipRequested)
            {
                _storyLabel.text = escapedSource;
                yield break;
            }

            tokenOpacity[i] = 0f;

            if (fadeDuration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    if (_skipRequested)
                    {
                        _storyLabel.text = escapedSource;
                        yield break;
                    }

                    float t = Mathf.Clamp01(elapsed / fadeDuration);
                    float eased = t * t * (3f - 2f * t);
                    tokenOpacity[i] = eased;
                    _storyLabel.text = BuildTokenFrame(tokens, tokenOpacity);

                    elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    yield return null;
                }
            }

            tokenOpacity[i] = 1f;
            _storyLabel.text = BuildTokenFrame(tokens, tokenOpacity);

            if (settleDuration > 0f)
                yield return WaitWithSkip(settleDuration);
        }

        _storyLabel.text = escapedSource;
    }

    private IEnumerator FadeStoryOutIn()
    {
        if (_storyLabel == null || lineFadeSeconds <= 0f)
            yield break;

        _introFrame?.EnableInClassList(FrameFadedClassName, true);
        _storyLabel.EnableInClassList(StoryFadedClassName, true);
        yield return WaitWithSkip(lineFadeSeconds);

        if (_skipRequested)
            yield break;

        _storyLabel.text = string.Empty;
        _introFrame?.EnableInClassList(FrameFadedClassName, false);
        _storyLabel.EnableInClassList(StoryFadedClassName, false);
        yield return WaitWithSkip(lineFadeSeconds);
    }

    private IEnumerator WaitWithSkip(float duration)
    {
        if (duration <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (_skipRequested)
                yield break;

            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }
    }

    private void ApplyResponsiveClass()
    {
        if (_introRoot == null)
            return;

        bool useCompact = Screen.height <= Mathf.Max(1, compactModeHeightThreshold);
        _introRoot.EnableInClassList("compact", useCompact);
    }

    private void SetPanelVisible(bool visible)
    {
        if (_introRoot == null)
            return;

        _introRoot.EnableInClassList("is-visible", visible);
        _introRoot.EnableInClassList("is-hidden", !visible);
        _introRoot.style.opacity = visible ? 1f : 0f;

        if (!visible && _storyLabel != null)
        {
            _storyLabel.text = string.Empty;
            _storyLabel.EnableInClassList(StoryFadedClassName, false);
        }

        if (!visible && _introFrame != null)
            _introFrame.EnableInClassList(FrameFadedClassName, false);
    }

    private List<IntroTextBox> GetActiveTextBoxes()
    {
        var result = new List<IntroTextBox>();
        for (int i = 0; i < textBoxes.Count; i++)
        {
            IntroTextBox box = textBoxes[i];
            if (box == null || string.IsNullOrWhiteSpace(box.text))
                continue;

            result.Add(box);
        }

        return result;
    }

    private static int CountCharactersToReveal(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return text.Length;
    }

    private static List<string> GetCharacterTokens(string text)
    {
        var tokens = new List<string>();
        if (string.IsNullOrEmpty(text))
            return tokens;

        for (int i = 0; i < text.Length; i++)
            tokens.Add(text[i].ToString());

        return tokens;
    }

    private static string EscapeRichText(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static string BuildAlphaWordTag(string escapedWordToken, byte alpha)
    {
        if (alpha >= 255)
            return escapedWordToken;

        return string.Format("<color=#FFFFFF{0}>{1}</color>", alpha.ToString("X2"), escapedWordToken);
    }

    private static string BuildTokenFrame(IReadOnlyList<string> tokens, IReadOnlyList<float> tokenOpacity)
    {
        if (tokens == null || tokenOpacity == null)
            return string.Empty;

        int count = Mathf.Min(tokens.Count, tokenOpacity.Count);
        var sb = new StringBuilder(count * 12);

        for (int i = 0; i < count; i++)
        {
            string token = EscapeRichText(tokens[i]);
            byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(tokenOpacity[i]));
            sb.Append(BuildAlphaWordTag(token, alpha));
        }

        return sb.ToString();
    }
}
