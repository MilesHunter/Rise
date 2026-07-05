using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

namespace Rise
{
    public sealed class StartMenuController : MonoBehaviour
    {
        private const string GameplaySceneName = "FinalPlayable";
        private const string CoverResourcePath = "UI/Rise_Cover";
        private const string TitleResourcePath = "UI/Rise_Title";
        private readonly RectTransform[] entranceItems = new RectTransform[6];
        private readonly CanvasGroup[] entranceGroups = new CanvasGroup[6];

        private RectTransform menuRoot;
        private CanvasGroup menuGroup;
        private Text statusText;
        private Button startButton;
        private Button continueButton;
        private Button settingsButton;
        private Button exitButton;
        private Sequence entranceSequence;
        private bool isTransitioning;

        private void Awake()
        {
            Build();
        }

        private void Start()
        {
            PlayEntrance();
            if (EventSystem.current != null && startButton != null)
            {
                EventSystem.current.SetSelectedGameObject(startButton.gameObject);
            }
        }

        private void OnDestroy()
        {
            entranceSequence?.Kill();
            if (menuRoot != null)
            {
                menuRoot.DOKill();
            }
        }

        private void Build()
        {
            Canvas canvas = GetOrAddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = GetOrAddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GetOrAddComponent<GraphicRaycaster>();
            ClearChildren(transform);

            Font font = ResolveFont();
            CreateCoverBackground();

            Image shade = CreatePanel("LeftShade", transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(560f, 0f));
            shade.color = new Color(0.025f, 0.035f, 0.045f, 0.9f);

            Image accent = CreatePanel("AccentLine", shade.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-2f, 0f), new Vector2(2f, 0f));
            accent.color = new Color(0.72f, 0.82f, 0.8f, 0.28f);

            GameObject rootObject = new GameObject("StartMenuLeft", typeof(RectTransform), typeof(CanvasGroup));
            rootObject.transform.SetParent(transform, false);
            menuRoot = rootObject.GetComponent<RectTransform>();
            menuRoot.anchorMin = new Vector2(0f, 0.5f);
            menuRoot.anchorMax = new Vector2(0f, 0.5f);
            menuRoot.pivot = new Vector2(0f, 0.5f);
            menuRoot.anchoredPosition = new Vector2(86f, 10f);
            menuRoot.sizeDelta = new Vector2(390f, 620f);
            menuGroup = rootObject.GetComponent<CanvasGroup>();

            RectTransform titleRect = CreateTitleArt(menuRoot, font);

            Text subtitle = CreateText("Subtitle", menuRoot, font, "A mountain survival climb", 22, new Vector2(2f, 128f), new Vector2(390f, 38f));
            subtitle.color = new Color(0.66f, 0.76f, 0.78f, 1f);

            startButton = CreateMenuButton("StartButton", "开始游戏", new Vector2(0f, 54f), OnStartGame);
            continueButton = CreateMenuButton("ContinueButton", "继续游戏", new Vector2(0f, -18f), OnContinueGame);
            settingsButton = CreateMenuButton("SettingsButton", "设置", new Vector2(0f, -90f), OnSettings);
            exitButton = CreateMenuButton("ExitButton", "退出", new Vector2(0f, -162f), OnExit);

            statusText = CreateText("Status", menuRoot, font, string.Empty, 17, new Vector2(2f, -228f), new Vector2(390f, 40f));
            statusText.color = new Color(0.62f, 0.72f, 0.72f, 0f);

            entranceItems[0] = titleRect;
            entranceItems[1] = subtitle.rectTransform;
            entranceItems[2] = startButton.GetComponent<RectTransform>();
            entranceItems[3] = continueButton.GetComponent<RectTransform>();
            entranceItems[4] = settingsButton.GetComponent<RectTransform>();
            entranceItems[5] = exitButton.GetComponent<RectTransform>();

            for (int i = 0; i < entranceItems.Length; i++)
            {
                CanvasGroup group = entranceItems[i].gameObject.AddComponent<CanvasGroup>();
                entranceGroups[i] = group;
            }
        }

        private void CreateCoverBackground()
        {
            Image background = CreatePanel("CoverBackground", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            background.sprite = LoadSpriteFromResources(CoverResourcePath);
            background.color = background.sprite != null ? Color.white : new Color(0.035f, 0.047f, 0.055f, 1f);
            background.type = Image.Type.Simple;
            background.preserveAspect = false;
        }

        private RectTransform CreateTitleArt(Transform parent, Font font)
        {
            Sprite titleSprite = LoadSpriteFromResources(TitleResourcePath);
            if (titleSprite != null)
            {
                Image title = CreatePanel("TitleArt", parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-18f, 194f), new Vector2(440f, 178f));
                title.sprite = titleSprite;
                title.type = Image.Type.Simple;
                title.preserveAspect = true;
                title.color = Color.white;
                return title.rectTransform;
            }

            Text fallbackTitle = CreateText("Title", parent, font, "RISE", 76, new Vector2(0f, 208f), new Vector2(390f, 92f));
            fallbackTitle.color = new Color(0.94f, 0.97f, 0.96f, 1f);
            fallbackTitle.fontStyle = FontStyle.Bold;
            return fallbackTitle.rectTransform;
        }

        private void PlayEntrance()
        {
            entranceSequence?.Kill();
            menuGroup.alpha = 1f;
            menuRoot.anchoredPosition = new Vector2(62f, 10f);

            entranceSequence = DOTween.Sequence().SetUpdate(true);
            entranceSequence.Append(menuRoot.DOAnchorPosX(86f, 0.55f).SetEase(Ease.OutCubic));

            for (int i = 0; i < entranceItems.Length; i++)
            {
                RectTransform item = entranceItems[i];
                CanvasGroup group = entranceGroups[i];
                Vector2 finalPosition = item.anchoredPosition;

                item.anchoredPosition = finalPosition + new Vector2(-26f, 0f);
                group.alpha = 0f;

                float at = 0.08f + i * 0.07f;
                entranceSequence.Insert(at, item.DOAnchorPos(finalPosition, 0.42f).SetEase(Ease.OutCubic));
                entranceSequence.Insert(at, group.DOFade(1f, 0.32f).SetEase(Ease.OutSine));
            }
        }

        private void OnStartGame()
        {
            BeginLoad(GameplaySceneName, startButton);
        }

        private void OnContinueGame()
        {
            ShowStatus("继续游戏：载入最近路线");
            BeginLoad(GameplaySceneName, continueButton, 0.18f);
        }

        private void OnSettings()
        {
            PlayButtonFeedback(settingsButton);
            ShowStatus("设置入口已响应");
        }

        private void OnExit()
        {
            PlayButtonFeedback(exitButton);
            ShowStatus("正在退出");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void BeginLoad(string sceneName, Button sourceButton, float delay = 0f)
        {
            if (isTransitioning)
            {
                return;
            }

            isTransitioning = true;
            SetButtonsInteractable(false);
            ShowStatus("正在进入山路");
            PlayButtonFeedback(sourceButton);

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.AppendInterval(delay);
            sequence.Append(menuRoot.DOAnchorPosX(54f, 0.28f).SetEase(Ease.InCubic));
            sequence.Join(menuGroup.DOFade(0f, 0.28f).SetEase(Ease.InSine));
            sequence.OnComplete(() => SceneManager.LoadScene(sceneName));
        }

        private void ShowStatus(string message)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.DOKill();
            statusText.text = message;
            statusText.color = new Color(statusText.color.r, statusText.color.g, statusText.color.b, 0f);
            statusText.DOFade(1f, 0.18f).SetUpdate(true);
        }

        private void PlayButtonFeedback(Button button)
        {
            if (button == null)
            {
                return;
            }

            StartMenuButtonAnimator animator = button.GetComponent<StartMenuButtonAnimator>();
            if (animator != null)
            {
                animator.PlayClickPulse();
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            startButton.interactable = interactable;
            continueButton.interactable = interactable;
            settingsButton.interactable = interactable;
            exitButton.interactable = interactable;
        }

        private Button CreateMenuButton(string objectName, string labelText, Vector2 anchoredPosition, UnityEngine.Events.UnityAction clicked)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(StartMenuButtonAnimator));
            buttonObject.transform.SetParent(menuRoot, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(320f, 52f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.08f, 0.11f, 0.15f, 0.78f);

            Button button = buttonObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(clicked);

            Text label = CreateText("Label", rect, ResolveFont(), labelText, 22, Vector2.zero, new Vector2(280f, 44f));
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            label.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.anchoredPosition = new Vector2(26f, 0f);

            buttonObject.GetComponent<StartMenuButtonAnimator>().Initialize(rect, image, label);
            return button;
        }

        private static Image CreatePanel(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return panelObject.GetComponent<Image>();
        }

        private static Text CreateText(string objectName, Transform parent, Font font, string value, int fontSize, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            return text;
        }

        private static Sprite LoadSpriteFromResources(string resourcePath)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                return sprite;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return null;
            }

            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Font ResolveFont()
        {
            string[] preferredFonts =
            {
                "Microsoft YaHei UI",
                "Microsoft YaHei",
                "SimHei",
                "Arial"
            };

            Font font = Font.CreateDynamicFontFromOSFont(preferredFonts, 24);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private T GetOrAddComponent<T>() where T : Component
        {
            T component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }
    }
}
