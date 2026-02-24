using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

public class DistrictMapManager : MonoBehaviour
{
    [Header("Raycast")]
    public Camera mainCamera;
    public LayerMask districtLayer;

    [Header("Materials")]
    public Material districtDefaultMaterial;
    public Material districtHoverMaterial;
    public Material districtSelectedMaterial;

    [Header("UI")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI hoverTooltip;
    public RectTransform districtPanel;
    public TextMeshProUGUI panelTitle;
    public Transform sceneListParent;
    public Button sceneButtonPrefab;
    public TextMeshProUGUI comingSoonLabel;
    public Button closeButton;
    public Button goButton;
    public Button backButton;

    [Header("Start Screen")]
    public RawImage startScreenBG;
    public GameObject mapRoot;
    public TMP_FontAsset titleFont;

    [Header("Audio")]
    public AudioSource bgmSource;

    private bool _gameStarted;
    private bool _startTriggered;
    private bool _titleReady;
    private Material _blurMaterial;
    private GameObject _titleRoot;
    private GameObject _bgCanvasObj;
    private Transform _originalBGParent;
    private string _fullTitle;
    private TextMeshProUGUI _startHint;
    private GameObject _exitButtonObj;
    private List<TextMeshProUGUI> _glowTexts = new List<TextMeshProUGUI>();

    private DistrictInteractable _hovered;
    private DistrictInteractable _selected;

    private string _selectedSceneName;
    private Button _selectedSceneButton;
    private static readonly Color SceneButtonDefault = new Color(0.102f, 0.141f, 0.251f, 1f);
    private static readonly Color SceneButtonHighlight = new Color(0.2f, 0.4f, 0.7f, 1f);

    private const float PanelHiddenX = 560f;
    private const float PanelVisibleX = -40f;
    private bool _panelOpen;

    private void Awake()
    {
        Application.runInBackground = true;
        DistrictInteractable.DefaultMaterial = districtDefaultMaterial;
        DistrictInteractable.HoverMaterial = districtHoverMaterial;
        DistrictInteractable.SelectedMaterial = districtSelectedMaterial;
    }

    private void Start()
    {
        ApplyNeonGlow();

        // Apply title font (WenKai) to all panel UI texts
        if (titleFont != null)
        {
            panelTitle.font = titleFont;
            hoverTooltip.font = titleFont;
            comingSoonLabel.font = titleFont;
            var closeBtnText = closeButton.GetComponentInChildren<TextMeshProUGUI>();
            if (closeBtnText != null) closeBtnText.font = titleFont;
        }

        hoverTooltip.gameObject.SetActive(false);
        districtPanel.anchoredPosition = new Vector2(PanelHiddenX, 0);
        closeButton.onClick.AddListener(ClosePanel);

        if (goButton != null)
        {
            goButton.onClick.AddListener(() => {
                if (!string.IsNullOrEmpty(_selectedSceneName))
                    StartCoroutine(LoadSceneWithFade(_selectedSceneName));
            });
            goButton.gameObject.SetActive(false);

            var goText = goButton.GetComponentInChildren<TextMeshProUGUI>();
            if (goText != null && titleFont != null)
                goText.font = titleFont;
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(() => StartCoroutine(ReturnToTitle()));
            backButton.gameObject.SetActive(false);

            var backText = backButton.GetComponentInChildren<TextMeshProUGUI>();
            if (backText != null && titleFont != null) backText.font = titleFont;
        }

        // Start screen setup
        if (startScreenBG != null)
        {
            _blurMaterial = Instantiate(startScreenBG.material);
            startScreenBG.material = _blurMaterial;
            _blurMaterial.SetFloat("_BlurAmount", 0f);
        }

        if (mapRoot != null)
            mapRoot.SetActive(false);

        if (titleText != null)
            titleText.gameObject.SetActive(true);

        StartCoroutine(TitleTypewriter());
    }

    private IEnumerator StartGame()
    {
        _startTriggered = true;

        float duration = 0.7f;
        float elapsed = 0f;

        CanvasGroup titleGroup = null;
        if (_titleRoot != null)
        {
            titleGroup = _titleRoot.GetComponent<CanvasGroup>();
            if (titleGroup == null)
                titleGroup = _titleRoot.AddComponent<CanvasGroup>();
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            if (_blurMaterial != null)
                _blurMaterial.SetFloat("_BlurAmount", t);

            if (titleGroup != null)
                titleGroup.alpha = 1f - t;

            yield return null;
        }

        if (_blurMaterial != null)
            _blurMaterial.SetFloat("_BlurAmount", 1f);

        if (_titleRoot != null)
            _titleRoot.SetActive(false);

        // Move blurred background to a canvas that renders behind 3D content
        if (startScreenBG != null)
        {
            startScreenBG.raycastTarget = false;
            _originalBGParent = startScreenBG.transform.parent;

            _bgCanvasObj = new GameObject("BackgroundCanvas");
            Canvas bgCanvas = _bgCanvasObj.AddComponent<Canvas>();
            bgCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            bgCanvas.worldCamera = mainCamera;
            bgCanvas.planeDistance = mainCamera.farClipPlane - 1f;
            bgCanvas.sortingOrder = -1;

            startScreenBG.transform.SetParent(_bgCanvasObj.transform, false);
            RectTransform rt = startScreenBG.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        if (mapRoot != null)
            mapRoot.SetActive(true);

        _gameStarted = true;

        if (backButton != null)
            backButton.gameObject.SetActive(true);

        // Default-select 中西區 so the side panel is open on first load
        if (mapRoot != null)
        {
            var cwTransform = mapRoot.transform.Find("central_western");
            if (cwTransform != null)
            {
                var cwInteractable = cwTransform.GetComponent<DistrictInteractable>();
                if (cwInteractable != null)
                {
                    _selected = cwInteractable;
                    _selected.Select();

                    if (DistrictData.Districts.TryGetValue("central_western", out DistrictData.DistrictDef data))
                        OpenPanel(data);
                }
            }
        }
    }

    private IEnumerator ReturnToTitle()
    {
        _gameStarted = false;

        // Close panel and deselect
        if (_panelOpen)
        {
            if (_selected != null) { _selected.Deselect(); _selected = null; }
            _selectedSceneName = null;
            _selectedSceneButton = null;
            if (goButton != null) goButton.gameObject.SetActive(false);
            _panelOpen = false;
            districtPanel.anchoredPosition = new Vector2(PanelHiddenX, 0f);
        }

        // Hide map and back button
        if (mapRoot != null) mapRoot.SetActive(false);
        if (backButton != null) backButton.gameObject.SetActive(false);
        hoverTooltip.gameObject.SetActive(false);

        // Move BG back to main canvas and unblur
        if (startScreenBG != null && _originalBGParent != null)
        {
            startScreenBG.transform.SetParent(_originalBGParent, false);
            RectTransform rt = startScreenBG.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            startScreenBG.raycastTarget = true;
        }
        if (_bgCanvasObj != null) { Destroy(_bgCanvasObj); _bgCanvasObj = null; }

        // Fade blur out
        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(1f, 0f, elapsed / duration);
            if (_blurMaterial != null) _blurMaterial.SetFloat("_BlurAmount", t);
            yield return null;
        }
        if (_blurMaterial != null) _blurMaterial.SetFloat("_BlurAmount", 0f);

        // Show title with full text (no typewriter)
        if (_titleRoot != null)
        {
            _titleRoot.SetActive(true);
            _titleRoot.transform.SetAsLastSibling();
            CanvasGroup titleGroup = _titleRoot.GetComponent<CanvasGroup>();
            if (titleGroup != null) titleGroup.alpha = 1f;
        }
        titleText.text = _fullTitle;
        foreach (var g in _glowTexts) g.text = _fullTitle;

        // Show start hint if not already present
        if (_startHint == null && _titleRoot != null)
        {
            GameObject hintObj = new GameObject("StartHint");
            hintObj.transform.SetParent(_titleRoot.transform, false);
            _startHint = hintObj.AddComponent<TextMeshProUGUI>();
            _startHint.text = "點擊任意位置開始";
            _startHint.font = titleFont;
            _startHint.fontSize = 24f;
            _startHint.alignment = TextAlignmentOptions.Center;
            _startHint.color = new Color(1f, 0.85f, 0.5f, 0.8f);
            _startHint.raycastTarget = false;
            RectTransform hintRT = hintObj.GetComponent<RectTransform>();
            hintRT.anchorMin = new Vector2(0.5f, 0.05f);
            hintRT.anchorMax = new Vector2(0.5f, 0.05f);
            hintRT.anchoredPosition = new Vector2(0f, 0f);
            hintRT.sizeDelta = new Vector2(400f, 40f);
        }
        else if (_startHint != null)
        {
            _startHint.gameObject.SetActive(true);
        }

        if (_exitButtonObj != null)
            _exitButtonObj.SetActive(true);
        else
            CreateExitButton();

        _startTriggered = false;
        _titleReady = true;
    }

    private void ApplyNeonGlow()
    {
        if (titleText == null) return;

        Transform canvas = titleText.transform.parent;
        RectTransform titleRT = titleText.GetComponent<RectTransform>();

        // Create a container to hold scrim + glow layers + title text
        _titleRoot = new GameObject("TitleRoot");
        _titleRoot.transform.SetParent(canvas, false);
        _titleRoot.transform.SetSiblingIndex(titleText.transform.GetSiblingIndex());
        RectTransform rootRT = _titleRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        // Full-screen dark overlay for contrast
        GameObject scrim = new GameObject("TitleScrim");
        scrim.transform.SetParent(_titleRoot.transform, false);
        Image scrimImg = scrim.AddComponent<Image>();
        scrimImg.color = new Color(0.03f, 0.01f, 0f, 0.6f);
        scrimImg.raycastTarget = false;
        RectTransform scrimRT = scrim.GetComponent<RectTransform>();
        scrimRT.anchorMin = Vector2.zero;
        scrimRT.anchorMax = Vector2.one;
        scrimRT.offsetMin = Vector2.zero;
        scrimRT.offsetMax = Vector2.zero;

        // Set title font before creating glow layers so material atlas matches
        if (titleFont != null)
            titleText.font = titleFont;

        // Save full title for typewriter, start empty
        _fullTitle = titleText.text;
        titleText.text = "";

        // Single soft glow layer behind text for subtle warmth
        {
            GameObject glowObj = new GameObject("TitleGlow");
            glowObj.transform.SetParent(_titleRoot.transform, false);
            TextMeshProUGUI g = glowObj.AddComponent<TextMeshProUGUI>();
            g.text = titleText.text;
            g.font = titleText.font;
            g.enableAutoSizing = true;
            g.fontSizeMin = 18f;
            g.fontSizeMax = 300f;
            g.fontStyle = titleText.fontStyle;
            g.alignment = titleText.alignment;
            g.characterSpacing = 20f;
            g.extraPadding = true;
            g.raycastTarget = false;
            RectTransform gRT = glowObj.GetComponent<RectTransform>();
            gRT.anchorMin = titleRT.anchorMin;
            gRT.anchorMax = titleRT.anchorMax;
            gRT.offsetMin = titleRT.offsetMin;
            gRT.offsetMax = titleRT.offsetMax;
            gRT.anchoredPosition = titleRT.anchoredPosition;
            gRT.localScale = Vector3.one * 1.04f;

            CanvasGroup cg = glowObj.AddComponent<CanvasGroup>();
            cg.alpha = 0.2f;

            Material gMat = Instantiate(g.fontMaterial);
            gMat.SetColor("_FaceColor", new Color(1f, 0.6f, 0.1f, 1f));
            gMat.SetFloat("_FaceDilate", 0.1f);
            gMat.SetFloat("_OutlineSoftness", 0.3f);
            gMat.SetColor("_OutlineColor", new Color(1f, 0.7f, 0.2f, 1f));
            gMat.SetFloat("_OutlineWidth", 0.1f);
            g.fontMaterial = gMat;
            _glowTexts.Add(g);
        }

        // Reparent the title text into the container (renders last = on top)
        titleText.transform.SetParent(_titleRoot.transform, false);

        // Main title: warm orange face with clean outline, minimal glow
        titleText.extraPadding = true;
        titleText.characterSpacing = 20f;

        Material mat = Instantiate(titleText.fontMaterial);
        mat.SetColor("_FaceColor", new Color(1f, 0.55f, 0.15f, 1f));
        mat.SetColor("_OutlineColor", new Color(1f, 0.85f, 0.3f, 1f));
        mat.SetFloat("_OutlineWidth", 0.1f);
        mat.SetColor("_GlowColor", new Color(1f, 0.6f, 0f, 0.2f));
        mat.SetFloat("_GlowOffset", 0.02f);
        mat.SetFloat("_GlowInner", 0.05f);
        mat.SetFloat("_GlowOuter", 0.15f);
        mat.SetFloat("_GlowPower", 0.15f);
        titleText.fontMaterial = mat;
    }

    private IEnumerator TitleTypewriter()
    {
        yield return new WaitForSeconds(1f);

        for (int i = 1; i <= _fullTitle.Length; i++)
        {
            string partial = _fullTitle.Substring(0, i);
            titleText.text = partial;
            foreach (var g in _glowTexts)
                g.text = partial;
            yield return new WaitForSeconds(0.5f);
        }

        _titleReady = true;

        // Show "press anywhere to start" hint below title
        if (_titleRoot != null)
        {
            GameObject hintObj = new GameObject("StartHint");
            hintObj.transform.SetParent(_titleRoot.transform, false);
            _startHint = hintObj.AddComponent<TextMeshProUGUI>();
            _startHint.text = "點擊任意位置開始";
            _startHint.font = titleFont;
            _startHint.fontSize = 24f;
            _startHint.alignment = TextAlignmentOptions.Center;
            _startHint.color = new Color(1f, 0.85f, 0.5f, 0.8f);
            _startHint.raycastTarget = false;

            RectTransform hintRT = hintObj.GetComponent<RectTransform>();
            hintRT.anchorMin = new Vector2(0.5f, 0.05f);
            hintRT.anchorMax = new Vector2(0.5f, 0.05f);
            hintRT.anchoredPosition = new Vector2(0f, 0f);
            hintRT.sizeDelta = new Vector2(400f, 40f);
        }

        CreateExitButton();
    }

    private void CreateExitButton()
    {
        if (_exitButtonObj != null || _titleRoot == null) return;

        _exitButtonObj = new GameObject("ExitButton");
        _exitButtonObj.transform.SetParent(_titleRoot.transform, false);

        RectTransform btnRT = _exitButtonObj.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0f, 1f);
        btnRT.anchorMax = new Vector2(0f, 1f);
        btnRT.pivot = new Vector2(0f, 1f);
        btnRT.anchoredPosition = new Vector2(40f, -30f);
        btnRT.sizeDelta = new Vector2(160f, 44f);

        Image btnImg = _exitButtonObj.AddComponent<Image>();
        btnImg.color = new Color(0.15f, 0.08f, 0.02f, 0.6f);

        Button btn = _exitButtonObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(1f, 0.7f, 0.3f, 0.3f);
        cb.pressedColor = new Color(1f, 0.5f, 0.1f, 0.4f);
        btn.colors = cb;
        btn.onClick.AddListener(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        GameObject textObj = new GameObject("ExitText");
        textObj.transform.SetParent(_exitButtonObj.transform, false);
        TextMeshProUGUI label = textObj.AddComponent<TextMeshProUGUI>();
        label.text = "離開遊戲";
        label.font = titleFont;
        label.fontSize = 22f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.85f, 0.5f, 0.9f);
        label.raycastTarget = false;

        RectTransform labelRT = textObj.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
    }

    private bool IsPointerOverButton()
    {
        if (EventSystem.current == null) return false;
        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        foreach (var result in results)
        {
            if (result.gameObject.GetComponent<Button>() != null)
                return true;
        }
        return false;
    }

    private void Update()
    {
        if (!_gameStarted)
        {
            if (_titleReady && !_startTriggered
                && (Mouse.current.leftButton.wasPressedThisFrame || Keyboard.current.anyKey.wasPressedThisFrame)
                && !IsPointerOverButton())
            {
                StartCoroutine(StartGame());
            }
            return;
        }

        HandleHover();
        HandleClick();
    }

    private void HandleHover()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        DistrictInteractable hovered = null;
        if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, districtLayer))
        {
            hovered = hitInfo.collider.GetComponentInParent<DistrictInteractable>();
        }

        if (hovered != _hovered)
        {
            if (_hovered != null)
                _hovered.HoverExit();

            _hovered = hovered;

            if (_hovered != null)
                _hovered.HoverEnter();
        }

        if (_hovered != null)
        {
            if (DistrictData.Districts.TryGetValue(_hovered.districtId, out DistrictData.DistrictDef def))
            {
                hoverTooltip.text = def.nameZH;
            }

            hoverTooltip.gameObject.SetActive(true);
            hoverTooltip.ForceMeshUpdate();

            RectTransform parentRect = hoverTooltip.transform.parent as RectTransform;
            RectTransform tooltipRect = (RectTransform)hoverTooltip.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, mousePos, null, out Vector2 localPos);

            // Auto-size tooltip to fit text
            Vector2 textSize = hoverTooltip.GetPreferredValues();
            tooltipRect.sizeDelta = textSize + new Vector2(20f, 10f);
            Vector2 tooltipSize = tooltipRect.sizeDelta;

            Rect parentBounds = parentRect.rect;
            const float offset = 20f;

            // Pivot is (0,0) so tooltip extends right and up from position
            float x = localPos.x + offset;
            float y = localPos.y + offset;

            // Flip if tooltip would go beyond edges
            if (x + tooltipSize.x > parentBounds.xMax)
                x = localPos.x - offset - tooltipSize.x;
            if (y + tooltipSize.y > parentBounds.yMax)
                y = localPos.y - offset - tooltipSize.y;

            // Final safety clamp
            x = Mathf.Clamp(x, parentBounds.xMin, parentBounds.xMax - tooltipSize.x);
            y = Mathf.Clamp(y, parentBounds.yMin, parentBounds.yMax - tooltipSize.y);

            tooltipRect.anchoredPosition = new Vector2(x, y);
        }
        else
        {
            hoverTooltip.gameObject.SetActive(false);
        }
    }

    private void HandleClick()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (_hovered == null) return;

        if (_selected != null && _selected != _hovered)
            _selected.Deselect();

        _selected = _hovered;
        _selected.Select();

        if (DistrictData.Districts.TryGetValue(_selected.districtId, out DistrictData.DistrictDef data))
        {
            OpenPanel(data);
        }
    }

    private void OpenPanel(DistrictData.DistrictDef data)
    {
        panelTitle.text = data.nameZH;

        _selectedSceneName = null;
        _selectedSceneButton = null;
        if (goButton != null) goButton.gameObject.SetActive(false);

        foreach (Transform child in sceneListParent)
            Destroy(child.gameObject);

        if (data.scenes.Length > 0)
        {
            comingSoonLabel.gameObject.SetActive(false);

            foreach (var scene in data.scenes)
            {
                Button btn = Instantiate(sceneButtonPrefab, sceneListParent);
                TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.font = titleFont;
                    label.text = string.IsNullOrEmpty(scene.episodeLabel)
                        ? scene.displayTitle
                        : $"{scene.displayTitle}  {scene.episodeLabel}";
                    label.overflowMode = TextOverflowModes.Ellipsis;
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.alignment = TextAlignmentOptions.MidlineLeft;

                    RectTransform labelRT = label.GetComponent<RectTransform>();
                    labelRT.anchorMin = Vector2.zero;
                    labelRT.anchorMax = Vector2.one;
                    labelRT.offsetMin = new Vector2(10f, 0f);
                    labelRT.offsetMax = new Vector2(-10f, 0f);
                }

                string captured = scene.sceneName;
                btn.onClick.AddListener(() => SelectSceneButton(btn, captured));
                btn.gameObject.SetActive(true);
            }
        }
        else
        {
            comingSoonLabel.gameObject.SetActive(true);
        }

        if (!_panelOpen)
        {
            _panelOpen = true;
            StopAllCoroutines();
            StartCoroutine(SlidePanel(PanelHiddenX, PanelVisibleX));
        }
    }

    private void SelectSceneButton(Button btn, string sceneName)
    {
        if (_selectedSceneButton != null)
        {
            var prevImg = _selectedSceneButton.GetComponent<Image>();
            if (prevImg != null) prevImg.color = SceneButtonDefault;
        }

        _selectedSceneButton = btn;
        _selectedSceneName = sceneName;

        var img = btn.GetComponent<Image>();
        if (img != null) img.color = SceneButtonHighlight;

        if (goButton != null) goButton.gameObject.SetActive(true);
    }

    private void ClosePanel()
    {
        if (!_panelOpen) return;

        if (_selected != null)
        {
            _selected.Deselect();
            _selected = null;
        }

        _selectedSceneName = null;
        _selectedSceneButton = null;
        if (goButton != null) goButton.gameObject.SetActive(false);

        _panelOpen = false;
        StopAllCoroutines();
        StartCoroutine(SlidePanel(PanelVisibleX, PanelHiddenX));
    }

    private IEnumerator SlidePanel(float fromX, float toX)
    {
        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            districtPanel.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, t), 0f);
            yield return null;
        }

        districtPanel.anchoredPosition = new Vector2(toX, 0f);
    }

    private IEnumerator LoadSceneWithFade(string sceneName)
    {
        // Fade out BGM over 0.5s then load the scene
        if (bgmSource != null && bgmSource.isPlaying)
        {
            float startVol = bgmSource.volume;
            float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                yield return null;
            }
            bgmSource.Stop();
        }
        SceneManager.LoadScene(sceneName);
    }
}
