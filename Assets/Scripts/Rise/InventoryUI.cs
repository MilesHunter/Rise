using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Rise
{
    public sealed class InventoryUI : MonoBehaviour
    {
        private const float SmallCellSize = 38f;
        private const float LargeCellSize = 30f;

        private readonly List<GameObject> gridVisuals = new List<GameObject>();
        private readonly List<HoverTarget> hoverTargets = new List<HoverTarget>();

        private PlayerClimbController controller;
        private PlayerInventory inventory;
        private RestSessionController rest;
        private CookingSystem cooking;
        private Canvas canvas;
        private RectTransform canvasRect;
        private Text smallPackLabel;
        private Text largePackLabel;
        private Text actionText;
        private Text restMenuText;
        private RectTransform smallGridRoot;
        private RectTransform largeGridRoot;
        private GameObject inventoryPanel;
        private GameObject restPanel;
        private GameObject hoverTooltip;
        private Text hoverTooltipText;
        private float detailTimer;
        private string restFeedback;
        private bool showLargePack;
        private InventoryGrid selectedGrid;
        private InventoryItemStack selectedStack;
        private InventoryGrid heldSourceGrid;
        private InventoryItemStack heldStack;
        private Vector2Int heldSourceOrigin;
        private Vector2 heldPointerScreenPosition;
        private float hoverTraceTimer;

        public bool BlocksClimbInput => inventoryPanel != null && inventoryPanel.activeSelf || rest != null && rest.IsResting;

        public void Initialize(PlayerClimbController player)
        {
            controller = player;
            inventory = player.GetComponent<PlayerInventory>();
            rest = player.GetComponent<RestSessionController>();
            cooking = player.GetComponent<CookingSystem>();
            Build();
        }

        private void Start()
        {
            if (controller == null)
            {
                controller = Object.FindAnyObjectByType<PlayerClimbController>();
                if (controller != null)
                {
                    Initialize(controller);
                }
            }
        }

        private void Update()
        {
            if (controller == null || inventory == null)
            {
                return;
            }

            HandleKeyboard();
            HandleMouse();
            UpdatePanelVisibility();
            RefreshView();
        }

        private void HandleKeyboard()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard.iKey.wasPressedThisFrame)
            {
                ToggleInventory(false);
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                ClearSelection();
                bool closed = CloseInventory();
                if (rest != null && rest.IsResting)
                {
                    if (closed)
                    {
                        rest.ExitRest();
                    }
                }
            }

            if (inventoryPanel.activeSelf)
            {
                if (keyboard.rKey.wasPressedThisFrame)
                {
                    TryRotateSelected();
                }

                if (keyboard.fKey.wasPressedThisFrame)
                {
                    TryQuickTransferSelected();
                }

                if (keyboard.digit1Key.wasPressedThisFrame)
                {
                    TryUseSelectedConsumable();
                }
            }

            if (rest != null && rest.IsResting)
            {
                if (keyboard.digit2Key.wasPressedThisFrame)
                {
                    ToggleInventory(rest.AllowsLargePack);
                }

                if (keyboard.digit3Key.wasPressedThisFrame && rest.AllowsCooking)
                {
                    cooking.TryCook(0, true);
                    restFeedback = cooking.LastMessage;
                    ShowDetail(cooking.LastMessage);
                }

                if (keyboard.digit4Key.wasPressedThisFrame)
                {
                    rest.FinishRest();
                    CloseInventory();
                }
            }
        }

        private void HandleMouse()
        {
            if (!inventoryPanel.activeSelf || Mouse.current == null)
            {
                return;
            }

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            heldPointerScreenPosition = screenPosition;
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (heldStack != null)
                {
                    TryPlaceHeldAtPointer(screenPosition);
                }
                else if (TryFindStackAtPointer(screenPosition, out InventoryGrid grid, out InventoryItemStack stack))
                {
                    PickUpStack(grid, stack);
                }
            }

            if (Mouse.current.rightButton.wasPressedThisFrame &&
                TryFindStackAtPointer(screenPosition, out InventoryGrid rightGrid, out InventoryItemStack rightStack))
            {
                if (heldStack != null)
                {
                    ShowDetail("Place held item before using another item");
                    return;
                }

                selectedGrid = rightGrid;
                selectedStack = rightStack;
                TryUseSelectedConsumable();
            }
        }

        private void ToggleInventory(bool includeLarge)
        {
            bool largePackModeChanged = showLargePack != includeLarge;
            showLargePack = includeLarge;
            inventoryPanel.SetActive(!inventoryPanel.activeSelf || largePackModeChanged);
            if (!inventoryPanel.activeSelf)
            {
                if (!ReturnHeldStack())
                {
                    inventoryPanel.SetActive(true);
                    ShowDetail("No safe slot to return held item");
                    return;
                }

                ClearSelection();
            }
        }

        private bool CloseInventory()
        {
            if (!ReturnHeldStack())
            {
                if (inventoryPanel != null)
                {
                    inventoryPanel.SetActive(true);
                }

                ShowDetail("No safe slot to return held item");
                return false;
            }

            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(false);
            }
            ClearSelection();
            return true;
        }

        private void UpdatePanelVisibility()
        {
            bool isResting = rest != null && rest.IsResting;
            restPanel.SetActive(isResting);
            if (isResting)
            {
                RefreshRestPanel();
            }
            else
            {
                restFeedback = null;
            }

            if (!isResting && showLargePack)
            {
                showLargePack = false;
                if (selectedGrid == inventory.LargePack)
                {
                    ClearSelection();
                }
                if (heldSourceGrid == inventory.LargePack || heldStack != null && !ReturnHeldStack())
                {
                    ClearSelection();
                }
            }

            detailTimer = Mathf.Max(0f, detailTimer - Time.deltaTime);
        }

        private void TryRotateSelected()
        {
            if (selectedGrid == null || selectedStack == null)
            {
                if (heldStack != null)
                {
                    heldStack.Rotated = !heldStack.Rotated;
                    ShowDetail($"Rotated {heldStack.Definition.DisplayName}");
                    return;
                }

                ShowDetail("Pick up or select an item first");
                return;
            }

            if (selectedGrid.TryRotate(selectedStack))
            {
                inventory.NotifyChanged();
                ShowDetail($"Rotated {selectedStack.Definition.DisplayName}");
            }
            else
            {
                ShowDetail("No room to rotate");
            }
        }

        private void TryQuickTransferSelected()
        {
            if (!showLargePack || selectedGrid == null || selectedStack == null)
            {
                if (heldStack != null)
                {
                    ShowDetail("Place held item before quick transfer");
                    return;
                }

                ShowDetail("Quick transfer is available at long rest");
                return;
            }

            InventoryGrid target = selectedGrid == inventory.SmallPack ? inventory.LargePack : inventory.SmallPack;
            if (selectedGrid.TryQuickTransferTo(target, selectedStack))
            {
                ClearSelection();
                inventory.NotifyChanged();
                ShowDetail("Transferred item");
            }
            else
            {
                ShowDetail("No room in target pack");
            }
        }

        private void TryUseSelectedConsumable()
        {
            if (selectedGrid == null || selectedStack == null || selectedStack.Definition == null)
            {
                if (heldStack != null)
                {
                    ShowDetail("Place held item before using it");
                    return;
                }

                ShowDetail("Select an item first");
                return;
            }

            if (!selectedStack.Definition.Consumable)
            {
                Vector2Int origin = selectedGrid.GetOrigin(selectedStack);
                ShowDetail($"{selectedStack.Definition.DisplayName} {selectedStack.Width}x{selectedStack.Height} @{origin.x},{origin.y}");
                return;
            }

            ApplyConsumable(selectedStack.Definition);
            if (controller != null)
            {
                AudioService.EnsureExists().Play3D(AudioCueId.ItemUse, controller.transform.position);
            }

            selectedStack.Quantity--;
            if (selectedStack.Quantity <= 0)
            {
                selectedGrid.Remove(selectedStack);
                ClearSelection();
            }

            inventory.NotifyChanged();
            ShowDetail("Used item");
        }

        private bool TryDropSelected()
        {
            if (selectedGrid == null || selectedStack == null)
            {
                ShowDetail("Select an item first");
                return false;
            }

            string displayName = selectedStack.Definition != null ? selectedStack.Definition.DisplayName : "Item";
            if (!selectedGrid.Remove(selectedStack))
            {
                ShowDetail("Could not discard item");
                return false;
            }

            ClearSelection();
            inventory.NotifyChanged();
            ShowDetail($"Discarded {displayName}");
            return true;
        }

        private void ShowSelectedDetails()
        {
            if (selectedGrid == null || selectedStack == null || selectedStack.Definition == null)
            {
                ShowDetail("Select an item first");
                return;
            }

            Vector2Int origin = selectedGrid.GetOrigin(selectedStack);
            ShowDetail($"{selectedStack.Definition.DisplayName} x{selectedStack.Quantity}  {selectedStack.Width}x{selectedStack.Height}  {selectedStack.TotalWeight:0.#}kg @{origin.x},{origin.y}");
        }

        private Vector2 ClampFloatingPanelPosition(Vector2 localPoint, Vector2 panelSize)
        {
            if (canvasRect == null)
            {
                return localPoint;
            }

            Rect rect = canvasRect.rect;
            float minX = rect.xMin + 8f;
            float maxX = rect.xMax - panelSize.x - 8f;
            float maxY = rect.yMax - 8f;
            float minY = rect.yMin + panelSize.y + 8f;
            return new Vector2(Mathf.Clamp(localPoint.x, minX, maxX), Mathf.Clamp(localPoint.y, minY, maxY));
        }

        private void PickUpStack(InventoryGrid grid, InventoryItemStack stack)
        {
            if (grid == null || stack == null)
            {
                return;
            }

            heldSourceGrid = grid;
            heldSourceOrigin = grid.GetOrigin(stack);
            heldStack = stack;
            selectedGrid = null;
            selectedStack = null;

            if (!grid.Remove(stack))
            {
                heldSourceGrid = null;
                heldStack = null;
                ShowDetail("Could not pick up item");
                return;
            }

            inventory.NotifyChanged();
            ShowDetail($"Holding {stack.Definition.DisplayName}");
        }

        private bool TryPlaceHeldAtPointer(Vector2 screenPosition)
        {
            if (heldStack == null)
            {
                return false;
            }

            if (!TryPointerToGridCell(screenPosition, out InventoryGrid targetGrid, out int x, out int y))
            {
                ShowDetail("Move over a pack grid");
                return false;
            }

            if (targetGrid == inventory.LargePack && !showLargePack)
            {
                ShowDetail("Large pack is locked outside long rest");
                return false;
            }

            if (!targetGrid.CanPlace(heldStack, x, y))
            {
                ShowDetail("Blocked or outside grid");
                return false;
            }

            if (!targetGrid.TryPlace(heldStack, x, y))
            {
                ShowDetail("Could not place item");
                return false;
            }

            heldSourceGrid = null;
            heldStack = null;
            ClearSelection();
            inventory.NotifyChanged();
            ShowDetail("Placed item");
            return true;
        }

        private void ApplyConsumable(InventoryItemDefinition definition)
        {
            if (controller == null || controller.Vitals == null)
            {
                return;
            }

            controller.Vitals.ApplyItemEffect(definition.UseEffect, definition.UseAmount);
        }

        private bool TryFindStackAtPointer(Vector2 screenPosition, out InventoryGrid grid, out InventoryItemStack stack)
        {
            stack = null;
            if (TryPointerToGridCell(screenPosition, out grid, out int x, out int y))
            {
                stack = grid.GetAt(x, y);
                return stack != null;
            }

            return false;
        }

        private bool TryPointerToGridCell(Vector2 screenPosition, out InventoryGrid grid, out int x, out int y)
        {
            grid = null;
            x = -1;
            y = -1;

            if (TryPointerToGridCell(screenPosition, smallGridRoot, inventory.SmallPack, SmallCellSize, out x, out y))
            {
                grid = inventory.SmallPack;
                return true;
            }

            if (showLargePack && TryPointerToGridCell(screenPosition, largeGridRoot, inventory.LargePack, LargeCellSize, out x, out y))
            {
                grid = inventory.LargePack;
                return true;
            }

            return false;
        }

        private bool TryPointerToGridCell(Vector2 screenPosition, RectTransform root, InventoryGrid grid, float cellSize, out int x, out int y)
        {
            x = -1;
            y = -1;
            if (root == null || grid == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPosition, null, out Vector2 localPoint))
            {
                return false;
            }

            float width = grid.Width * cellSize;
            float height = grid.Height * cellSize;
            if (localPoint.x < 0f || localPoint.y > 0f || localPoint.x >= width || localPoint.y <= -height)
            {
                return false;
            }

            x = Mathf.FloorToInt(localPoint.x / cellSize);
            y = Mathf.FloorToInt(-localPoint.y / cellSize);
            return true;
        }

        private void ClearSelection()
        {
            selectedGrid = null;
            selectedStack = null;
        }

        private bool ReturnHeldStack()
        {
            if (heldStack == null)
            {
                return true;
            }

            InventoryGrid target = heldSourceGrid ?? inventory.SmallPack;
            bool restored = target.TryPlace(heldStack, heldSourceOrigin.x, heldSourceOrigin.y);
            if (!restored)
            {
                restored = target.AddItem(heldStack.Definition, heldStack.Quantity) == 0;
            }

            if (restored)
            {
                heldSourceGrid = null;
                heldStack = null;
                inventory.NotifyChanged();
            }

            return restored;
        }

        private void ShowDetail(string message)
        {
            actionText.text = message;
            detailTimer = 3f;
        }

        private void RefreshView()
        {
            if (smallPackLabel == null)
            {
                return;
            }

            smallPackLabel.text = $"Small pack 4x4  {inventory.SmallPack.TotalWeight():0.#}";
            largePackLabel.text = showLargePack
                ? $"Camp pack 6x6  {inventory.LargePack.TotalWeight():0.#}"
                : "Camp pack locked until long rest";

            if (detailTimer <= 0f)
            {
                actionText.text = heldStack != null
                    ? "LMB place  R rotate held  Esc return"
                    : "LMB pick/place  RMB use/details  R rotate selected  F transfer at long rest";
            }

            RebuildGridVisuals();
            UpdateHoverTooltip();
        }

        private void RebuildGridVisuals()
        {
            for (int i = 0; i < gridVisuals.Count; i++)
            {
                if (gridVisuals[i] != null)
                {
                    Destroy(gridVisuals[i]);
                }
            }
            gridVisuals.Clear();
            hoverTargets.Clear();

            BuildGridVisual(inventory.SmallPack, smallGridRoot, SmallCellSize, true);
            BuildGridVisual(inventory.LargePack, largeGridRoot, LargeCellSize, showLargePack);
            BuildHeldPreview();
        }

        private void BuildGridVisual(InventoryGrid grid, RectTransform root, float cellSize, bool unlocked)
        {
            if (root == null)
            {
                return;
            }

            Image rootImage = root.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.color = unlocked ? new Color(0.02f, 0.025f, 0.03f, 0.9f) : new Color(0.02f, 0.02f, 0.02f, 0.45f);
            }

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Image cell = CreateChildImage($"Cell {x},{y}", root, new Vector2(x * cellSize, -y * cellSize), new Vector2(cellSize - 2f, cellSize - 2f), new Color(0.12f, 0.13f, 0.14f, unlocked ? 0.92f : 0.28f));
                    gridVisuals.Add(cell.gameObject);
                }
            }

            if (!unlocked)
            {
                return;
            }

            foreach (InventoryItemStack stack in grid.Stacks)
            {
                Vector2Int origin = grid.GetOrigin(stack);
                bool selected = stack == selectedStack;
                Color color = GetItemColor(stack.Definition != null ? stack.Definition.ItemId : string.Empty);
                Image item = CreateChildImage(stack.Definition != null ? stack.Definition.DisplayName : "Item", root,
                    new Vector2(origin.x * cellSize + 2f, -origin.y * cellSize - 2f),
                    new Vector2(stack.Width * cellSize - 6f, stack.Height * cellSize - 6f),
                    selected ? new Color(0.95f, 0.75f, 0.26f, 0.95f) : color);
                gridVisuals.Add(item.gameObject);
                hoverTargets.Add(new HoverTarget(grid, stack, item.rectTransform));

                Text label = CreateText("Label", item.transform, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 6,
                    new Vector2(3f, -2f), new Vector2(item.rectTransform.sizeDelta.x - 6f, item.rectTransform.sizeDelta.y - 4f));
                label.text = stack.Quantity > 1 ? $"{ShortName(stack.Definition.DisplayName)} x{stack.Quantity}" : ShortName(stack.Definition.DisplayName);
                label.alignment = TextAnchor.UpperLeft;
                label.color = Color.white;
                gridVisuals.Add(label.gameObject);
            }
        }

        private void BuildHeldPreview()
        {
            if (heldStack == null || canvasRect == null)
            {
                return;
            }

            if (TryPointerToGridCell(heldPointerScreenPosition, out InventoryGrid targetGrid, out int x, out int y))
            {
                RectTransform root = targetGrid == inventory.SmallPack ? smallGridRoot : largeGridRoot;
                float cellSize = targetGrid == inventory.SmallPack ? SmallCellSize : LargeCellSize;
                bool canPlace = targetGrid.CanPlace(heldStack, x, y);
                Image preview = CreateChildImage("HeldPlacementPreview", root,
                    new Vector2(x * cellSize + 2f, -y * cellSize - 2f),
                    new Vector2(heldStack.Width * cellSize - 6f, heldStack.Height * cellSize - 6f),
                    canPlace ? new Color(0.3f, 0.85f, 0.42f, 0.42f) : new Color(0.95f, 0.18f, 0.18f, 0.5f));
                gridVisuals.Add(preview.gameObject);
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, heldPointerScreenPosition, null, out Vector2 localPoint);
            Color color = GetItemColor(heldStack.Definition != null ? heldStack.Definition.ItemId : string.Empty);
            Image heldImage = CreateChildImage("HeldItem", canvasRect, localPoint + new Vector2(10f, -10f),
                new Vector2(heldStack.Width * SmallCellSize - 6f, heldStack.Height * SmallCellSize - 6f),
                new Color(color.r, color.g, color.b, 0.78f));
            heldImage.raycastTarget = false;
            gridVisuals.Add(heldImage.gameObject);

            Text label = CreateText("HeldLabel", heldImage.transform, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 6,
                new Vector2(3f, -2f), new Vector2(heldImage.rectTransform.sizeDelta.x - 6f, heldImage.rectTransform.sizeDelta.y - 4f));
            label.text = heldStack.Quantity > 1 ? $"{ShortName(heldStack.Definition.DisplayName)} x{heldStack.Quantity}" : ShortName(heldStack.Definition.DisplayName);
            label.raycastTarget = false;
            gridVisuals.Add(label.gameObject);
        }

        private void UpdateHoverTooltip()
        {
            if (hoverTooltip == null || hoverTooltipText == null || heldStack != null || !inventoryPanel.activeSelf || Mouse.current == null)
            {
                if (hoverTooltip != null)
                {
                    hoverTooltip.SetActive(false);
                }

                return;
            }

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            bool rectHit = TryFindHoverTarget(screenPosition, out InventoryGrid grid, out InventoryItemStack stack);
            bool gridHit = false;
            if (!rectHit)
            {
                gridHit = TryFindStackAtPointer(screenPosition, out grid, out stack);
            }

            if ((rectHit || gridHit) && stack != null && stack.Definition != null)
            {
                ShowHoverTooltip(screenPosition, grid, stack);
                TraceHover(screenPosition, rectHit, gridHit, stack, "shown");
                return;
            }

            TraceHover(screenPosition, rectHit, gridHit, stack, "no-stack");
            hoverTooltip.SetActive(false);
        }

        private void ShowHoverTooltip(Vector2 screenPosition, InventoryGrid grid, InventoryItemStack stack)
        {
            if (hoverTooltip == null || hoverTooltipText == null || stack == null || stack.Definition == null)
            {
                return;
            }

            Vector2Int origin = grid.GetOrigin(stack);
            hoverTooltipText.text = $"{stack.Definition.DisplayName}\nQty {stack.Quantity}  Size {stack.Width}x{stack.Height}\nWeight {stack.TotalWeight:0.#}kg  @{origin.x},{origin.y}\nRMB {(stack.Definition.Consumable ? "use" : "not usable")}";
            RectTransform tooltipRect = hoverTooltip.GetComponent<RectTransform>();
            tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
            tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
            tooltipRect.pivot = new Vector2(0f, 1f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 localPoint);
            tooltipRect.anchoredPosition = ClampFloatingPanelPosition(localPoint + new Vector2(-170f, 74f), tooltipRect.sizeDelta);
            hoverTooltip.SetActive(true);
            hoverTooltip.transform.SetAsLastSibling();
        }

        private void TraceHover(Vector2 screenPosition, bool rectHit, bool gridHit, InventoryItemStack stack, string reason)
        {
            hoverTraceTimer -= Time.unscaledDeltaTime;
            if (hoverTraceTimer > 0f)
            {
                return;
            }

            hoverTraceTimer = 0.5f;
            string item = stack != null && stack.Definition != null ? stack.Definition.ItemId : "none";
            bool tooltipActive = hoverTooltip != null && hoverTooltip.activeSelf;
            string tooltipPosition = "none";
            if (hoverTooltip != null)
            {
                tooltipPosition = hoverTooltip.GetComponent<RectTransform>().anchoredPosition.ToString();
            }
            Debug.Log($"RISE_INVENTORY_HOVER_TRACE reason={reason} mouse={screenPosition.x:0},{screenPosition.y:0} targets={hoverTargets.Count} rectHit={rectHit} gridHit={gridHit} item={item} tooltip={tooltipActive} tooltipPos={tooltipPosition} panel={inventoryPanel.activeSelf}");
        }

        private bool TryFindHoverTarget(Vector2 screenPosition, out InventoryGrid grid, out InventoryItemStack stack)
        {
            for (int i = hoverTargets.Count - 1; i >= 0; i--)
            {
                HoverTarget target = hoverTargets[i];
                if (target.Rect == null || target.Stack == null)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(target.Rect, screenPosition, null))
                {
                    grid = target.Grid;
                    stack = target.Stack;
                    return true;
                }
            }

            grid = null;
            stack = null;
            return false;
        }

        private static string ShortName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName) || displayName.Length <= 12)
            {
                return displayName;
            }

            return displayName.Substring(0, 12);
        }

        private static Color GetItemColor(string itemId)
        {
            switch (itemId)
            {
                case PlayerInventory.AnchorItemId:
                    return new Color(0.48f, 0.58f, 0.66f, 0.95f);
                case PlayerInventory.RopeItemId:
                    return new Color(0.70f, 0.52f, 0.28f, 0.95f);
                case "food_ration":
                    return new Color(0.38f, 0.55f, 0.34f, 0.95f);
                case "medkit":
                    return new Color(0.66f, 0.28f, 0.30f, 0.95f);
                case "fuel_canister":
                    return new Color(0.34f, 0.44f, 0.66f, 0.95f);
                default:
                    return new Color(0.45f, 0.40f, 0.58f, 0.95f);
            }
        }

        private void Build()
        {
            if (canvas != null)
            {
                return;
            }

            canvas = GetOrAddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = GetOrAddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            GetOrAddComponent<GraphicRaycaster>();
            canvasRect = GetComponent<RectTransform>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Transform existingInventoryPanel = transform.Find("InventoryPanel");
            inventoryPanel = existingInventoryPanel != null
                ? existingInventoryPanel.gameObject
                : CreatePanel("InventoryPanel", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(600f, 330f), new Color(0.06f, 0.07f, 0.08f, 0.9f)).gameObject;
            ConfigurePanelRect(inventoryPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(600f, 330f));
            ClearChildren(inventoryPanel.transform);

            smallPackLabel = CreateText("SmallPackLabel", inventoryPanel.transform, font, 14, new Vector2(18f, -16f), new Vector2(210f, 24f));
            largePackLabel = CreateText("LargePackLabel", inventoryPanel.transform, font, 14, new Vector2(250f, -16f), new Vector2(310f, 24f));
            smallGridRoot = CreateGridRoot("SmallGrid", inventoryPanel.transform, new Vector2(18f, -48f), new Vector2(inventory.SmallPack.Width * SmallCellSize, inventory.SmallPack.Height * SmallCellSize));
            largeGridRoot = CreateGridRoot("LargeGrid", inventoryPanel.transform, new Vector2(250f, -48f), new Vector2(inventory.LargePack.Width * LargeCellSize, inventory.LargePack.Height * LargeCellSize));
            actionText = CreateText("InventoryAction", inventoryPanel.transform, font, 13, new Vector2(18f, -295f), new Vector2(560f, 26f));
            CreateHoverTooltip(font);
            inventoryPanel.SetActive(false);

            Transform existingRestPanel = transform.Find("RestPanel");
            restPanel = existingRestPanel != null
                ? existingRestPanel.gameObject
                : CreatePanel("RestPanel", transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, -30f), new Vector2(250f, 155f), new Color(0.08f, 0.07f, 0.05f, 0.88f)).gameObject;
            ConfigurePanelRect(restPanel, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, -30f), new Vector2(250f, 155f));
            ClearChildren(restPanel.transform);
            restMenuText = CreateText("RestMenu", restPanel.transform, font, 13, new Vector2(14f, -12f), new Vector2(220f, 132f));
            restMenuText.text = "Rest menu";
            restPanel.SetActive(false);
        }

        private void RefreshRestPanel()
        {
            if (restMenuText == null || rest == null || !rest.IsResting)
            {
                return;
            }

            RestPoint active = rest.ActiveRestPoint;
            bool longRest = active != null && active.RestType == RestPointType.LongRest;
            string title = longRest ? "Long rest" : "Short rest";
            string cookLine = longRest ? "3 Cook meal" : "3 Cook locked";
            string feedback = !string.IsNullOrEmpty(restFeedback)
                ? restFeedback
                : !string.IsNullOrEmpty(rest.LastRestMessage) ? rest.LastRestMessage : active != null ? active.DetailText : string.Empty;
            restMenuText.text = $"{title}\n{rest.BuildRestPreview()}\n2 Organize pack\n{cookLine}\n4 Rest and recover\nEsc Exit\n{feedback}";
        }

        private T GetOrAddComponent<T>() where T : Component
        {
            T component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static RectTransform CreateGridRoot(string name, Transform parent, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private static Image CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            ConfigurePanelRect(panel, anchorMin, anchorMax, new Vector2(1f, 1f), anchoredPosition, sizeDelta);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Image CreateChildImage(string name, Transform parent, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private void CreateHoverTooltip(Font font)
        {
            hoverTooltip = CreatePanel("HoverTooltip", canvasRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(165f, 72f), new Color(0.035f, 0.04f, 0.045f, 0.94f)).gameObject;
            RectTransform rect = hoverTooltip.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0f, 1f);
            hoverTooltip.GetComponent<Image>().raycastTarget = false;
            hoverTooltipText = CreateText("HoverTooltipText", hoverTooltip.transform, font, 11, new Vector2(8f, -6f), new Vector2(150f, 60f));
            hoverTooltipText.raycastTarget = false;
            hoverTooltip.SetActive(false);
        }

        private static Text CreateText(string name, Transform parent, Font font, int size, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void ConfigurePanelRect(GameObject panel, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            RectTransform rect = panel.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private sealed class HoverTarget
        {
            public HoverTarget(InventoryGrid grid, InventoryItemStack stack, RectTransform rect)
            {
                Grid = grid;
                Stack = stack;
                Rect = rect;
            }

            public InventoryGrid Grid { get; }
            public InventoryItemStack Stack { get; }
            public RectTransform Rect { get; }
        }
    }
}
