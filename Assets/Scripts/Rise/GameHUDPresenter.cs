using UnityEngine;
using UnityEngine.UI;

namespace Rise
{
    public sealed class GameHUDPresenter : MonoBehaviour
    {
        private PlayerClimbController controller;
        private PlayerVitals vitals;
        private PlayerInventory inventory;
        private Image staminaFill;
        private Text staminaText;
        private Text toolText;
        private Text promptText;
        private Text statusText;
        private Text controlsText;
        private Text summitText;
        private bool built;

        public void Initialize(PlayerClimbController player)
        {
            controller = player;
            vitals = player.Vitals;
            inventory = player.Inventory;
            EnsureBuilt();
        }

        private void Start()
        {
            if (controller == null)
            {
                controller = Object.FindAnyObjectByType<PlayerClimbController>();
                vitals = controller != null ? controller.Vitals : null;
                inventory = controller != null ? controller.Inventory : null;
            }

            EnsureBuilt();
        }

        private void Update()
        {
            if (controller == null || vitals == null || inventory == null || staminaFill == null || staminaText == null || toolText == null ||
                promptText == null || statusText == null || controlsText == null || summitText == null)
            {
                return;
            }

            staminaFill.fillAmount = vitals.Stamina / vitals.MaxStamina;
            staminaText.text = $"Stamina {Mathf.CeilToInt(vitals.Stamina)}/100";
            toolText.text = $"Tool {(controller.Tools.ToolMode ? "[Armed]" : "[Idle]")} {controller.Tools.SelectedTool}  Anchor {inventory.CountSmall(PlayerInventory.AnchorItemId)}  Rope {inventory.CountSmall(PlayerInventory.RopeItemId)}";
            promptText.text = controller.CurrentPrompt;
            statusText.text = $"Health {Mathf.CeilToInt(vitals.Health)}  Hunger {Mathf.CeilToInt(vitals.Hunger)}  Warmth {Mathf.CeilToInt(vitals.Warmth)}  Sanity {Mathf.CeilToInt(vitals.Sanity)}  Load {inventory.WeightClass} {inventory.TotalWeight:0.#}";
            controlsText.text = "Mouse aim  LMB/RMB hands  Q/E kick  W tool  Wheel switch  I pack  F rest/tool place";
            summitText.gameObject.SetActive(controller.HasWon);
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
            ClearChildren(transform);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Image rootPanel = CreatePanel("TopPanel", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(340f, 122f));
            rootPanel.color = new Color(0.06f, 0.08f, 0.12f, 0.65f);

            Image staminaBackground = CreatePanel("StaminaBg", rootPanel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(200f, 13f));
            staminaBackground.color = new Color(0f, 0f, 0f, 0.5f);

            staminaFill = CreatePanel("StaminaFill", staminaBackground.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            staminaFill.type = Image.Type.Filled;
            staminaFill.fillMethod = Image.FillMethod.Horizontal;
            staminaFill.color = new Color(0.35f, 0.88f, 0.48f, 1f);

            staminaText = CreateText("StaminaText", rootPanel.transform, font, 14, new Vector2(14f, -31f), new Vector2(190f, 20f));
            toolText = CreateText("ToolText", rootPanel.transform, font, 14, new Vector2(14f, -53f), new Vector2(305f, 20f));
            promptText = CreateText("PromptText", rootPanel.transform, font, 14, new Vector2(14f, -75f), new Vector2(305f, 20f));
            statusText = CreateText("StatusText", rootPanel.transform, font, 12, new Vector2(14f, -96f), new Vector2(310f, 18f));
            controlsText = CreateText("Controls", transform, font, 14, new Vector2(16f, 16f), new Vector2(620f, 22f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            summitText = CreateText("SummitText", transform, font, 34, new Vector2(-170f, -35f), new Vector2(340f, 70f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            summitText.alignment = TextAnchor.MiddleCenter;
            summitText.text = "Summit reached";
            summitText.color = new Color(0.97f, 0.92f, 0.45f);
            summitText.gameObject.SetActive(false);
            built = true;
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

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }
    }
}
