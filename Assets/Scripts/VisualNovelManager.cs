using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class VisualNovelManager : MonoBehaviour
{
    [System.Serializable]
    public class DialogueLine
    {
        public string speaker;
        public string text;
        public Color  nameColor;
    }

    [Header("Dialogue")]
    [SerializeField] private TextAsset dialogueFile;

    [Header("Settings")]
    public float typewriterSpeed = 0.04f;

    [Header("Font")]
    public TMP_FontAsset font;

    [Header("Character Colors")]
    public Color wakiColor   = new Color(0.2f, 0.6f, 1f);
    public Color foreignerColor = new Color(1f, 0.5f, 0.2f);

    // Auto-found at runtime
    private Image           portraitLeft;
    private Image           portraitRight;
    private Image           namePlate;
    private Image           nameUnderline;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI dialogueText;
    private TextMeshProUGUI continueHint;
    private GameObject      dialogueBox;

    private AudioSource audioSource;

    private DialogueLine[] lines;
    private int  currentIndex = 0;
    private bool isTyping     = false;
    private bool isEnded      = false;
    private bool _confirmOpen = false;
    private GameObject _confirmPanel;
    private Coroutine typewriterCoroutine;

    void Awake()
    {
        portraitLeft  = GameObject.Find("PortraitLeft")?.GetComponent<Image>();
        portraitRight = GameObject.Find("PortraitRight")?.GetComponent<Image>();
        namePlate     = GameObject.Find("NamePlate")?.GetComponent<Image>();
        nameText      = GameObject.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        dialogueText  = GameObject.Find("DialogueText")?.GetComponent<TextMeshProUGUI>();
        continueHint  = GameObject.Find("ContinueHint")?.GetComponent<TextMeshProUGUI>();
        nameUnderline = GameObject.Find("NameUnderline")?.GetComponent<Image>();
        dialogueBox   = GameObject.Find("DialogueBox");

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    void Start()
    {
        if (font != null)
        {
            if (nameText != null) nameText.font = font;
            if (dialogueText != null) dialogueText.font = font;
            if (continueHint != null) continueHint.font = font;
        }

        // Hide all UI initially, then reveal in stages
        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (portraitLeft != null) portraitLeft.gameObject.SetActive(false);
        if (portraitRight != null) portraitRight.gameObject.SetActive(false);

        CreateBackButton();

        lines = ParseDialogue();
        StartCoroutine(StagedEntrance());
    }

    private void CreateBackButton()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Ensure Canvas has a GraphicRaycaster (needed for Button clicks)
        if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Ensure an EventSystem exists in the scene
        if (EventSystem.current == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // Back button — top-left
        GameObject btnObj = new GameObject("BackButton");
        btnObj.transform.SetParent(canvas.transform, false);
        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = new Color(0f, 0f, 0f, 0.4f);
        Button btn = btnObj.AddComponent<Button>();

        RectTransform btnRT = btnObj.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0f, 1f);
        btnRT.anchorMax = new Vector2(0f, 1f);
        btnRT.pivot = new Vector2(0f, 1f);
        btnRT.anchoredPosition = new Vector2(15f, -15f);
        btnRT.sizeDelta = new Vector2(100f, 40f);

        GameObject txtObj = new GameObject("BackText");
        txtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.text = "< 返回";
        txt.fontSize = 22f;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        if (font != null) txt.font = font;
        RectTransform txtRT = txtObj.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;

        btn.onClick.AddListener(ShowConfirmPanel);
    }

    private void ShowConfirmPanel()
    {
        if (_confirmOpen) return;
        _confirmOpen = true;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        _confirmPanel = new GameObject("ConfirmPanel");
        _confirmPanel.transform.SetParent(canvas.transform, false);
        _confirmPanel.transform.SetAsLastSibling();

        // Full-screen dim overlay
        Image overlay = _confirmPanel.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.6f);
        RectTransform overlayRT = _confirmPanel.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        // Dialog box
        GameObject box = new GameObject("DialogBox");
        box.transform.SetParent(_confirmPanel.transform, false);
        Image boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);
        RectTransform boxRT = box.GetComponent<RectTransform>();
        boxRT.anchorMin = new Vector2(0.5f, 0.5f);
        boxRT.anchorMax = new Vector2(0.5f, 0.5f);
        boxRT.sizeDelta = new Vector2(360f, 180f);
        boxRT.anchoredPosition = Vector2.zero;

        // Question text
        GameObject qObj = new GameObject("QuestionText");
        qObj.transform.SetParent(box.transform, false);
        TextMeshProUGUI qTxt = qObj.AddComponent<TextMeshProUGUI>();
        qTxt.text = "確認返回選單?";
        qTxt.fontSize = 28f;
        qTxt.alignment = TextAlignmentOptions.Center;
        qTxt.color = Color.white;
        if (font != null) qTxt.font = font;
        RectTransform qRT = qObj.GetComponent<RectTransform>();
        qRT.anchorMin = new Vector2(0f, 0.5f);
        qRT.anchorMax = new Vector2(1f, 1f);
        qRT.offsetMin = new Vector2(10f, 0f);
        qRT.offsetMax = new Vector2(-10f, -10f);

        // Confirm button
        GameObject confirmObj = new GameObject("ConfirmButton");
        confirmObj.transform.SetParent(box.transform, false);
        Image confirmBg = confirmObj.AddComponent<Image>();
        confirmBg.color = new Color(0.2f, 0.7f, 0.3f, 1f);
        Button confirmBtn = confirmObj.AddComponent<Button>();
        RectTransform confirmRT = confirmObj.GetComponent<RectTransform>();
        confirmRT.anchorMin = new Vector2(0.1f, 0.08f);
        confirmRT.anchorMax = new Vector2(0.45f, 0.42f);
        confirmRT.offsetMin = Vector2.zero;
        confirmRT.offsetMax = Vector2.zero;

        GameObject confirmTxtObj = new GameObject("Text");
        confirmTxtObj.transform.SetParent(confirmObj.transform, false);
        TextMeshProUGUI confirmTxt = confirmTxtObj.AddComponent<TextMeshProUGUI>();
        confirmTxt.text = "確認";
        confirmTxt.fontSize = 24f;
        confirmTxt.alignment = TextAlignmentOptions.Center;
        confirmTxt.color = Color.white;
        if (font != null) confirmTxt.font = font;
        RectTransform confirmTxtRT = confirmTxtObj.GetComponent<RectTransform>();
        confirmTxtRT.anchorMin = Vector2.zero;
        confirmTxtRT.anchorMax = Vector2.one;
        confirmTxtRT.offsetMin = Vector2.zero;
        confirmTxtRT.offsetMax = Vector2.zero;

        confirmBtn.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));

        // Cancel button
        GameObject cancelObj = new GameObject("CancelButton");
        cancelObj.transform.SetParent(box.transform, false);
        Image cancelBg = cancelObj.AddComponent<Image>();
        cancelBg.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        Button cancelBtn = cancelObj.AddComponent<Button>();
        RectTransform cancelRT = cancelObj.GetComponent<RectTransform>();
        cancelRT.anchorMin = new Vector2(0.55f, 0.08f);
        cancelRT.anchorMax = new Vector2(0.9f, 0.42f);
        cancelRT.offsetMin = Vector2.zero;
        cancelRT.offsetMax = Vector2.zero;

        GameObject cancelTxtObj = new GameObject("Text");
        cancelTxtObj.transform.SetParent(cancelObj.transform, false);
        TextMeshProUGUI cancelTxt = cancelTxtObj.AddComponent<TextMeshProUGUI>();
        cancelTxt.text = "取消";
        cancelTxt.fontSize = 24f;
        cancelTxt.alignment = TextAlignmentOptions.Center;
        cancelTxt.color = Color.white;
        if (font != null) cancelTxt.font = font;
        RectTransform cancelTxtRT = cancelTxtObj.GetComponent<RectTransform>();
        cancelTxtRT.anchorMin = Vector2.zero;
        cancelTxtRT.anchorMax = Vector2.one;
        cancelTxtRT.offsetMin = Vector2.zero;
        cancelTxtRT.offsetMax = Vector2.zero;

        cancelBtn.onClick.AddListener(HideConfirmPanel);
    }

    private void HideConfirmPanel()
    {
        if (_confirmPanel != null)
        {
            Destroy(_confirmPanel);
            _confirmPanel = null;
        }
        _confirmOpen = false;
    }

    private IEnumerator StagedEntrance()
    {
        // Stage 1: brief buffer
        yield return new WaitForSeconds(0.5f);

        // Stage 2: show character portraits with fade-in
        if (portraitLeft != null)
        {
            portraitLeft.gameObject.SetActive(true);
            portraitLeft.color = new Color(1f, 1f, 1f, 0f);
        }
        if (portraitRight != null)
        {
            portraitRight.gameObject.SetActive(true);
            portraitRight.color = new Color(1f, 1f, 1f, 0f);
        }

        float fadeDuration = 0.6f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Clamp01(elapsed / fadeDuration);
            if (portraitLeft != null) portraitLeft.color = new Color(1f, 1f, 1f, a);
            if (portraitRight != null) portraitRight.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }

        // Stage 3: pause, then show dialogue box and start dialogue
        yield return new WaitForSeconds(0.5f);

        if (dialogueBox != null) dialogueBox.SetActive(true);
        ShowLine(currentIndex);
    }

    DialogueLine[] ParseDialogue()
    {
        if (dialogueFile == null)
        {
            Debug.LogWarning("VisualNovelManager: no dialogue file assigned.");
            return new DialogueLine[0];
        }

        var result = new System.Collections.Generic.List<DialogueLine>();
        string[] rawLines = dialogueFile.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string raw in rawLines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            int sep = line.IndexOf('|');
            if (sep < 0) continue;

            string speaker = line.Substring(0, sep).Trim();
            string text    = line.Substring(sep + 1).Trim();

            result.Add(new DialogueLine
            {
                speaker   = speaker,
                text      = text,
                nameColor = speaker == "和記" ? wakiColor : foreignerColor
            });
        }

        return result.ToArray();
    }

    void ShowLine(int index)
    {
        if (index >= lines.Length)
        {
            continueHint.text = "— END —  ▼ 返回選單";
            isEnded = true;
            return;
        }

        DialogueLine line = lines[index];
        nameText.text  = line.speaker;
        nameText.color = line.nameColor;
        if (nameUnderline != null) nameUnderline.color = line.nameColor;

        bool isWaki = line.speaker == "和記";
        if (portraitLeft  != null)
            portraitLeft.color  = isWaki ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
        if (portraitRight != null)
            portraitRight.color = isWaki ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;

        AudioClip clip = Resources.Load<AudioClip>("DialogueClips/" + (index + 1).ToString("D2"));
        if (clip != null)
        {
            audioSource.clip = clip;
            audioSource.Play();
        }

        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);
        typewriterCoroutine = StartCoroutine(TypewriterEffect(line.text));
    }

    IEnumerator TypewriterEffect(string text)
    {
        isTyping          = true;
        continueHint.text = "";
        dialogueText.text = "";

        foreach (char c in text)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(typewriterSpeed);
        }

        isTyping = false;
        continueHint.text = currentIndex >= lines.Length - 1
            ? "— END —  ▼ 返回選單"
            : "▼  點擊繼續";
    }

    void Update()
    {
        if (_confirmOpen) return;

        var keyboard = Keyboard.current;
        var mouse    = Mouse.current;

        bool mouseClick = mouse != null && mouse.leftButton.wasPressedThisFrame;
        if (mouseClick && IsPointerOverButton())
            mouseClick = false;

        bool advance = mouseClick
                    || (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                    || (keyboard != null && keyboard.enterKey.wasPressedThisFrame);

        if (advance) Advance();
    }

    private bool IsPointerOverButton()
    {
        if (EventSystem.current == null || Mouse.current == null) return false;
        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        foreach (var result in results)
        {
            if (result.gameObject.GetComponent<Button>() != null)
                return true;
        }
        return false;
    }

    void Advance()
    {
        if (isEnded)
        {
            SceneManager.LoadScene("MainMenu");
            return;
        }

        if (isTyping)
        {
            StopCoroutine(typewriterCoroutine);
            isTyping          = false;
            dialogueText.text = lines[currentIndex].text;
            audioSource.Stop();
            continueHint.text = currentIndex >= lines.Length - 1
                ? "— END —  ▼ 返回選單"
                : "▼  點擊繼續";
            return;
        }

        currentIndex++;
        ShowLine(currentIndex);
    }
}
