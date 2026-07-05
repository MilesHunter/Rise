using UnityEngine;
using UnityEngine.UI;

namespace Rise
{
    public sealed class GameHUDPresenter : MonoBehaviour
    {
        private const string HealthIconGuid = "fd37dd972ddb43c43a06765d1aa6a5f9";
        private const string HungerIconGuid = "b82870376ff74f0469ce7cfcfb7a019f";
        private const string WarmthIconGuid = "97f38603f04dc5e44909f357bb8cd0e8";
        private const string SanityIconGuid = "4227bb643ba894342aa1e36ae9c2a59c";

        private PlayerClimbController controller;
        private PlayerVitals vitals;
        private PlayerInventory inventory;
        private Image staminaFill;
        private Text staminaText;
        private Text staminaValueText;
        private Text toolText;
        private Text promptText;
        private Text statusText;
        private Text controlsText;
        private Text summitText;
        private RectTransform statusOverlayRect;
        private Image healthOverlay;
        private Image hungerOverlay;
        private Image warmthOverlay;
        private Image sanityOverlay;
        private GameObject resourceSearchPanel;
        private Image resourceSearchFill;
        private Text resourceSearchText;
        private ResourceIconDisplay healthDisplay;
        private ResourceIconDisplay hungerDisplay;
        private ResourceIconDisplay warmthDisplay;
        private ResourceIconDisplay sanityDisplay;
        private bool built;

        public void Initialize(PlayerClimbController player)
        {
            if (player == null)
            {
                return;
            }

            controller = player;
            vitals = player.Vitals != null ? player.Vitals : player.GetComponent<PlayerVitals>();
            inventory = player.Inventory != null ? player.Inventory : player.GetComponent<PlayerInventory>();
            EnsureBuilt();
        }

        private void Start()
        {
            if (controller == null)
            {
                controller = Object.FindAnyObjectByType<PlayerClimbController>();
                vitals = controller != null ? (controller.Vitals != null ? controller.Vitals : controller.GetComponent<PlayerVitals>()) : null;
                inventory = controller != null ? (controller.Inventory != null ? controller.Inventory : controller.GetComponent<PlayerInventory>()) : null;
            }

            EnsureBuilt();
        }

        private void Update()
        {
            if (controller == null || vitals == null || inventory == null)
            {
                return;
            }

            if (staminaFill != null)
            {
                staminaFill.fillAmount = vitals.Stamina / vitals.MaxStamina;
            }

            string staminaValue = $"{Mathf.CeilToInt(vitals.Stamina)}/{Mathf.CeilToInt(vitals.MaxStamina)}";
            if (staminaText != null)
            {
                staminaText.text = $"Stamina {staminaValue}";
            }

            if (staminaValueText != null)
            {
                staminaValueText.text = staminaValue;
                staminaValueText.color = vitals.LowStamina
                    ? Color.Lerp(new Color(1f, 0.18f, 0.12f, 1f), new Color(1f, 0.85f, 0.24f, 1f), Mathf.PingPong(Time.unscaledTime * 6f, 1f))
                    : Color.white;
            }

            if (toolText != null)
            {
                toolText.text = $"Tool {(controller.Tools.ToolMode ? "[Armed]" : "[Idle]")} {controller.Tools.SelectedTool}  Anchor {inventory.CountSmall(PlayerInventory.AnchorItemId)}  Rope {inventory.CountSmall(PlayerInventory.RopeItemId)}";
            }

            if (promptText != null)
            {
                promptText.text = controller.CurrentPrompt;
            }

            if (statusText != null)
            {
                statusText.text = BuildStatusSummary();
            }

            if (controlsText != null)
            {
                controlsText.text = "Mouse aim  LMB/RMB hands  Q/E kick  W tool  Wheel switch  I pack  F rest/tool place";
            }

            healthDisplay?.UpdateValue(vitals.Health, vitals.WarningHealth, vitals.LowHealth);
            hungerDisplay?.UpdateValue(vitals.Hunger, vitals.WarningHunger, vitals.LowHunger);
            warmthDisplay?.UpdateValue(vitals.Warmth, vitals.WarningWarmth, vitals.LowWarmth);
            sanityDisplay?.UpdateValue(vitals.Sanity, vitals.WarningSanity, vitals.LowSanity);
            UpdateStatusOverlays();
            UpdateResourceSearchPanel();

            if (summitText != null)
            {
                summitText.gameObject.SetActive(controller.HasWon);
            }
        }

        private void BuildCanvas()
        {
            if (built)
            {
                return;
            }

            Canvas canvas = GetOrAddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            GetOrAddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            GetOrAddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (transform.childCount > 0)
            {
                BindSceneHud(font);
                EnsureStatusOverlay();
                built = true;
                return;
            }

            Image rootPanel = CreatePanel("TopPanel", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(340f, 122f));
            rootPanel.color = new Color(0.06f, 0.08f, 0.12f, 0.65f);

            Image staminaBackground = CreatePanel("StaminaBg", rootPanel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(200f, 13f));
            staminaBackground.color = new Color(0f, 0f, 0f, 0.5f);

            staminaFill = CreatePanel("StaminaFill", staminaBackground.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            staminaFill.type = Image.Type.Filled;
            staminaFill.fillMethod = Image.FillMethod.Horizontal;
            staminaFill.color = new Color(0.35f, 0.88f, 0.48f, 1f);

            staminaValueText = CreateStaminaValueText(rootPanel.transform, font, staminaBackground.rectTransform);
            staminaText = CreateText("StaminaText", rootPanel.transform, font, 14, new Vector2(14f, -31f), new Vector2(190f, 20f));
            toolText = CreateText("ToolText", rootPanel.transform, font, 14, new Vector2(14f, -53f), new Vector2(305f, 20f));
            promptText = CreateText("PromptText", rootPanel.transform, font, 14, new Vector2(14f, -75f), new Vector2(305f, 20f));
            statusText = CreateText("StatusText", rootPanel.transform, font, 12, new Vector2(14f, -96f), new Vector2(310f, 18f));

            BuildResourceIconPanel(font);
            EnsureResourceSearchPanel(font);
            EnsureStatusOverlay();
            controlsText = CreateText("Controls", transform, font, 14, new Vector2(16f, 108f), new Vector2(620f, 22f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            summitText = CreateText("SummitText", transform, font, 34, new Vector2(-170f, -35f), new Vector2(340f, 70f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            summitText.alignment = TextAnchor.MiddleCenter;
            summitText.text = "Summit reached";
            summitText.color = new Color(0.97f, 0.92f, 0.45f);
            summitText.gameObject.SetActive(false);
            built = true;
        }

        private void BindSceneHud(Font font)
        {
            Transform topPanel = transform.Find("TopPanel");
            Transform staminaBackground = topPanel != null ? topPanel.Find("StaminaBg") : null;
            Transform staminaFillTransform = staminaBackground != null ? staminaBackground.Find("StaminaFill") : null;
            staminaFill = staminaFillTransform != null ? staminaFillTransform.GetComponent<Image>() : null;

            staminaText = FindText(topPanel, "StaminaText");
            staminaValueText = FindText(topPanel, "StaminaValue");
            if (topPanel != null && staminaValueText == null)
            {
                staminaValueText = CreateStaminaValueText(topPanel, font, staminaBackground as RectTransform);
            }

            toolText = FindText(topPanel, "ToolText");
            promptText = FindText(topPanel, "PromptText");
            statusText = FindText(topPanel, "StatusText");
            controlsText = FindText(transform, "Controls");
            summitText = FindText(transform, "SummitText");

            Transform resources = transform.Find("ResourceIcons");
            healthDisplay = BindResourceDisplay(resources, "Health", HealthIconGuid);
            hungerDisplay = BindResourceDisplay(resources, "Hunger", HungerIconGuid);
            warmthDisplay = BindResourceDisplay(resources, "Warmth", WarmthIconGuid);
            sanityDisplay = BindResourceDisplay(resources, "Sanity", SanityIconGuid);
            BindStatusOverlay();
            EnsureResourceSearchPanel(font);
        }

        private void EnsureStatusOverlay()
        {
            Transform overlay = transform.Find("StatusOverlay");
            if (overlay == null)
            {
                GameObject overlayObject = new GameObject("StatusOverlay", typeof(RectTransform));
                overlayObject.transform.SetParent(transform, false);
                ConfigureFullScreenRect(overlayObject.GetComponent<RectTransform>());
                overlay = overlayObject.transform;
            }

            overlay.SetAsFirstSibling();
            statusOverlayRect = overlay.GetComponent<RectTransform>();
            healthOverlay = FindOrCreateOverlayImage(overlay, "HealthOverlay", new Color(0.75f, 0f, 0f, 0f));
            hungerOverlay = FindOrCreateOverlayImage(overlay, "HungerOverlay", new Color(0.65f, 0.42f, 0.05f, 0f));
            warmthOverlay = FindOrCreateOverlayImage(overlay, "WarmthOverlay", new Color(0.05f, 0.35f, 1f, 0f));
            sanityOverlay = FindOrCreateOverlayImage(overlay, "SanityOverlay", new Color(0.35f, 0f, 0.55f, 0f));
        }

        private void BindStatusOverlay()
        {
            Transform overlay = transform.Find("StatusOverlay");
            if (overlay == null)
            {
                return;
            }

            statusOverlayRect = overlay.GetComponent<RectTransform>();
            Transform health = overlay.Find("HealthOverlay");
            Transform hunger = overlay.Find("HungerOverlay");
            Transform warmth = overlay.Find("WarmthOverlay");
            Transform sanity = overlay.Find("SanityOverlay");
            healthOverlay = health != null ? health.GetComponent<Image>() : null;
            hungerOverlay = hunger != null ? hunger.GetComponent<Image>() : null;
            warmthOverlay = warmth != null ? warmth.GetComponent<Image>() : null;
            sanityOverlay = sanity != null ? sanity.GetComponent<Image>() : null;
        }

        private void UpdateStatusOverlays()
        {
            SetOverlayAlpha(healthOverlay, new Color(0.85f, 0f, 0f, 1f), vitals.LowHealth ? 0.18f : vitals.WarningHealth ? 0.07f : 0f, 4.5f);
            SetOverlayAlpha(hungerOverlay, new Color(0.62f, 0.40f, 0.05f, 1f), vitals.LowHunger ? 0.13f : vitals.WarningHunger ? 0.05f : 0f, 3.2f);
            SetOverlayAlpha(warmthOverlay, new Color(0.05f, 0.38f, 1f, 1f), vitals.LowWarmth ? 0.14f : vitals.WarningWarmth ? 0.05f : 0f, 2.8f);
            SetOverlayAlpha(sanityOverlay, new Color(0.34f, 0f, 0.55f, 1f), vitals.LowSanity ? 0.12f : vitals.WarningSanity ? 0.04f : 0f, 6f);
            UpdateStatusOverlayShake();
        }

        private void UpdateStatusOverlayShake()
        {
            if (statusOverlayRect == null)
            {
                return;
            }

            float strength = 0f;
            if (vitals.LowHealth) strength += 0.9f;
            if (vitals.LowHunger) strength += 0.45f;
            if (vitals.LowWarmth) strength += 0.65f;
            if (vitals.LowSanity) strength += 1.1f;

            if (strength <= 0f)
            {
                statusOverlayRect.anchoredPosition = Vector2.zero;
                return;
            }

            float x = Mathf.PerlinNoise(Time.unscaledTime * 18f, 2.7f) - 0.5f;
            float y = Mathf.PerlinNoise(7.4f, Time.unscaledTime * 18f) - 0.5f;
            statusOverlayRect.anchoredPosition = new Vector2(x, y) * Mathf.Min(strength, 2.2f);
        }

        private string BuildStatusSummary()
        {
            string text = $"Load {inventory.WeightClass} {inventory.TotalWeight:0.#}";
            if (vitals.LowHealth) text += "  injured";
            else if (vitals.WarningHealth) text += "  hurt";

            if (vitals.LowHunger) text += "  starving";
            else if (vitals.WarningHunger) text += "  hungry";

            if (vitals.LowWarmth) text += "  freezing";
            else if (vitals.WarningWarmth) text += "  cold";

            if (vitals.LowSanity) text += "  shaken";
            else if (vitals.WarningSanity) text += "  uneasy";

            return text;
        }

        private void EnsureResourceSearchPanel(Font font)
        {
            Transform existing = transform.Find("ResourceSearchPanel");
            if (existing == null)
            {
                Image panel = CreatePanel("ResourceSearchPanel", transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 148f), new Vector2(250f, 54f));
                panel.color = new Color(0.035f, 0.045f, 0.05f, 0.78f);
                panel.raycastTarget = false;
                panel.rectTransform.pivot = new Vector2(0f, 1f);

                Image background = CreatePanel("ProgressBg", panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -38f), new Vector2(230f, 8f));
                background.color = new Color(0f, 0f, 0f, 0.45f);
                background.raycastTarget = false;

                resourceSearchFill = CreatePanel("ProgressFill", background.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                resourceSearchFill.type = Image.Type.Filled;
                resourceSearchFill.fillMethod = Image.FillMethod.Horizontal;
                resourceSearchFill.color = new Color(0.42f, 0.82f, 0.62f, 0.95f);
                resourceSearchFill.raycastTarget = false;

                resourceSearchText = CreateText("Label", panel.transform, font, 12, new Vector2(10f, -5f), new Vector2(230f, 32f));
                resourceSearchText.raycastTarget = false;
                resourceSearchPanel = panel.gameObject;
                resourceSearchPanel.SetActive(false);
                return;
            }

            resourceSearchPanel = existing.gameObject;
            Transform progressBackground = existing.Find("ProgressBg");
            Transform progressFill = progressBackground != null ? progressBackground.Find("ProgressFill") : existing.Find("ProgressFill");
            resourceSearchFill = progressFill != null ? progressFill.GetComponent<Image>() : null;
            resourceSearchText = FindText(existing, "Label");

            if (resourceSearchFill == null && progressBackground != null)
            {
                resourceSearchFill = CreatePanel("ProgressFill", progressBackground, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                resourceSearchFill.type = Image.Type.Filled;
                resourceSearchFill.fillMethod = Image.FillMethod.Horizontal;
                resourceSearchFill.color = new Color(0.42f, 0.82f, 0.62f, 0.95f);
            }

            if (resourceSearchText == null)
            {
                resourceSearchText = CreateText("Label", existing, font, 13, new Vector2(10f, -5f), new Vector2(230f, 18f));
            }

            resourceSearchPanel.SetActive(false);
        }

        private void UpdateResourceSearchPanel()
        {
            if (resourceSearchPanel == null)
            {
                return;
            }

            ResourceNode node = controller != null ? controller.CurrentResourceNode : null;
            bool visible = node != null;
            resourceSearchPanel.SetActive(visible);
            if (!visible)
            {
                return;
            }

            if (resourceSearchFill != null)
            {
                resourceSearchFill.fillAmount = node.SearchProgress01;
                resourceSearchFill.color = node.RevealedCount > 0
                    ? new Color(0.95f, 0.78f, 0.28f, 0.95f)
                    : new Color(0.42f, 0.82f, 0.62f, 0.95f);
            }

            if (resourceSearchText != null)
            {
                string suffix = node.RevealedCount > 0 ? "  F take" : node.IsSearching ? string.Empty : "  F search";
                resourceSearchText.text = $"{node.SearchLabel}{suffix}\n{node.BuildSlotSummary()}";
            }
        }

        private static void SetOverlayAlpha(Image image, Color baseColor, float maxAlpha, float pulseSpeed)
        {
            if (image == null)
            {
                return;
            }

            float pulse = maxAlpha > 0f ? Mathf.Lerp(0.55f, 1f, Mathf.PingPong(Time.unscaledTime * pulseSpeed, 1f)) : 0f;
            baseColor.a = maxAlpha * pulse;
            image.color = baseColor;
        }

        private void BuildResourceIconPanel(Font font)
        {
            Image panel = CreatePanel("ResourceIcons", transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 16f), new Vector2(250f, 78f));
            panel.color = new Color(0.045f, 0.055f, 0.065f, 0.72f);
            panel.rectTransform.pivot = new Vector2(0f, 0f);

            healthDisplay = CreateResourceDisplay("Health", panel.transform, font, new Vector2(12f, 44f), LoadSpriteFramesByGuid(HealthIconGuid));
            hungerDisplay = CreateResourceDisplay("Hunger", panel.transform, font, new Vector2(132f, 44f), LoadSpriteFramesByGuid(HungerIconGuid));
            warmthDisplay = CreateResourceDisplay("Warmth", panel.transform, font, new Vector2(12f, 10f), LoadSpriteFramesByGuid(WarmthIconGuid));
            sanityDisplay = CreateResourceDisplay("Sanity", panel.transform, font, new Vector2(132f, 10f), LoadSpriteFramesByGuid(SanityIconGuid));
        }

        private static Text CreateStaminaValueText(Transform parent, Font font, RectTransform staminaBackground)
        {
            Vector2 anchoredPosition = new Vector2(222f, -19f);
            if (staminaBackground != null)
            {
                anchoredPosition = staminaBackground.anchoredPosition + new Vector2(staminaBackground.sizeDelta.x + 8f, -4f);
            }

            Text text = CreateText("StaminaValue", parent, font, 16, anchoredPosition, new Vector2(74f, 22f));
            text.alignment = TextAnchor.MiddleLeft;
            text.fontStyle = FontStyle.Bold;
            text.text = "100/100";
            return text;
        }

        private static ResourceIconDisplay CreateResourceDisplay(string name, Transform parent, Font font, Vector2 anchoredPosition, Sprite[] frames)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(100f, 24f);

            Image icon = CreateIconImage("Icon", root.transform, frames != null && frames.Length > 0 ? frames[0] : null);
            Text value = CreateText("Value", root.transform, font, 20, new Vector2(33f, 1f), new Vector2(58f, 24f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            value.alignment = TextAnchor.MiddleLeft;
            value.fontStyle = FontStyle.Bold;
            value.text = "100";
            return new ResourceIconDisplay(icon, value, frames);
        }

        private static ResourceIconDisplay BindResourceDisplay(Transform resources, string childName, string iconGuid)
        {
            Transform root = resources != null ? resources.Find(childName) : null;
            Transform iconTransform = root != null ? root.Find("Icon") : null;
            Transform valueTransform = root != null ? root.Find("Value") : null;
            Image icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            Text value = valueTransform != null ? valueTransform.GetComponent<Text>() : null;
            return icon != null && value != null ? new ResourceIconDisplay(icon, value, LoadSpriteFramesByGuid(iconGuid)) : null;
        }

        private static Text FindText(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            return child != null ? child.GetComponent<Text>() : null;
        }

        private static Image CreateIconImage(string name, Transform parent, Sprite sprite)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(28f, 28f);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = Color.white;
            return image;
        }

        private static Image FindOrCreateOverlayImage(Transform parent, string name, Color color)
        {
            Transform existing = parent.Find(name);
            Image image;
            if (existing != null)
            {
                image = existing.GetComponent<Image>();
                if (image == null)
                {
                    image = existing.gameObject.AddComponent<Image>();
                }
            }
            else
            {
                GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                imageObject.transform.SetParent(parent, false);
                ConfigureFullScreenRect(imageObject.GetComponent<RectTransform>());
                image = imageObject.GetComponent<Image>();
            }

            image.raycastTarget = false;
            image.color = color;
            return image;
        }

        private static void ConfigureFullScreenRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static Sprite[] LoadSpriteFramesByGuid(string assetGuid)
        {
#if UNITY_EDITOR
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(assetGuid);
            Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (assets != null && assets.Length > 0)
            {
                System.Collections.Generic.List<Sprite> sprites = new System.Collections.Generic.List<Sprite>();
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is Sprite sprite)
                    {
                        sprites.Add(sprite);
                    }
                }

                sprites.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
                return sprites.ToArray();
            }
#endif
            return System.Array.Empty<Sprite>();
        }

        private void EnsureBuilt()
        {
            if (controller != null && vitals != null && inventory != null && !built)
            {
                BuildCanvas();
            }
        }

        private T GetOrAddComponent<T>() where T : Component
        {
            T component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static Image CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return panel.GetComponent<Image>();
        }

        private static Text CreateText(string name, Transform parent, Font font, int fontSize, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            return CreateText(name, parent, font, fontSize, anchoredPosition, sizeDelta, new Vector2(0f, 1f), new Vector2(0f, 1f));
        }

        private static Text CreateText(string name, Transform parent, Font font, int fontSize, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin == anchorMax && anchorMin == new Vector2(0.5f, 0.5f) ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private sealed class ResourceIconDisplay
        {
            private const float FrameDuration = 0.13f;

            private readonly Image icon;
            private readonly Text valueText;
            private readonly Sprite[] frames;
            private float timer;
            private int frameIndex;

            public ResourceIconDisplay(Image icon, Text valueText, Sprite[] frames)
            {
                this.icon = icon;
                this.valueText = valueText;
                this.frames = frames ?? System.Array.Empty<Sprite>();
            }

            public void UpdateValue(float value, bool warning, bool low)
            {
                valueText.text = Mathf.CeilToInt(value).ToString();
                Color targetColor = Color.white;
                if (low)
                {
                    float pulse = Mathf.PingPong(Time.unscaledTime * 5f, 1f);
                    targetColor = Color.Lerp(new Color(1f, 0.18f, 0.12f, 1f), new Color(1f, 0.78f, 0.2f, 1f), pulse);
                }
                else if (warning)
                {
                    targetColor = new Color(1f, 0.82f, 0.28f, 1f);
                }

                icon.color = targetColor;
                valueText.color = targetColor;

                if (frames.Length == 0)
                {
                    return;
                }

                timer += Time.unscaledDeltaTime;
                if (timer < FrameDuration)
                {
                    return;
                }

                timer = 0f;
                frameIndex = (frameIndex + 1) % frames.Length;
                icon.sprite = frames[frameIndex];
            }
        }
    }
}
