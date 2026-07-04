using System.Collections.Generic;
using UnityEngine;

namespace Rise
{
    public sealed class InventoryItemStack
    {
        private readonly InventoryItemDefinition definition;
        private int quantity;
        private bool rotated;

        public InventoryItemDefinition Definition => definition;
        public int Quantity
        {
            get => quantity;
            set => quantity = Mathf.Clamp(value, 0, MaxStack);
        }
        public bool Rotated
        {
            get => rotated;
            set => rotated = value;
        }
        public int Width => rotated ? definition.Height : definition.Width;
        public int Height => rotated ? definition.Width : definition.Height;
        public int MaxStack => definition != null ? definition.MaxStack : 1;
        public float TotalWeight => definition != null ? definition.Weight * quantity : 0f;

        public InventoryItemStack(InventoryItemDefinition definition, int quantity, bool rotated = false)
        {
            this.definition = definition;
            this.quantity = Mathf.Clamp(quantity, 0, definition != null ? definition.MaxStack : 1);
            this.rotated = rotated;
        }

        public InventoryItemStack Clone(int? overrideQuantity = null)
        {
            return new InventoryItemStack(definition, overrideQuantity ?? quantity, rotated);
        }
    }

    public sealed class InventoryGrid
    {
        private readonly InventoryItemStack[,] cells;
        private readonly List<InventoryItemStack> stacks = new List<InventoryItemStack>();
        private readonly Dictionary<InventoryItemStack, Vector2Int> origins = new Dictionary<InventoryItemStack, Vector2Int>();

        public InventoryGrid(int width, int height, string label)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            Label = label;
            cells = new InventoryItemStack[Width, Height];
        }

        public int Width { get; }
        public int Height { get; }
        public string Label { get; }
        public IReadOnlyList<InventoryItemStack> Stacks => stacks;

        public bool CanPlace(InventoryItemStack stack, int x, int y, bool ignoreSelf = false)
        {
            if (stack == null || stack.Definition == null || x < 0 || y < 0 || x + stack.Width > Width || y + stack.Height > Height)
            {
                return false;
            }

            for (int ix = x; ix < x + stack.Width; ix++)
            {
                for (int iy = y; iy < y + stack.Height; iy++)
                {
                    InventoryItemStack occupying = cells[ix, iy];
                    if (occupying != null && (!ignoreSelf || occupying != stack))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool TryPlace(InventoryItemStack stack, int x, int y)
        {
            if (!CanPlace(stack, x, y))
            {
                return false;
            }

            stacks.Add(stack);
            origins[stack] = new Vector2Int(x, y);
            FillCells(stack, x, y);
            return true;
        }

        public bool TryMove(InventoryItemStack stack, int x, int y)
        {
            if (stack == null || !origins.ContainsKey(stack))
            {
                return false;
            }

            Vector2Int oldOrigin = origins[stack];
            ClearCells(stack);
            if (!CanPlace(stack, x, y, true))
            {
                FillCells(stack, oldOrigin.x, oldOrigin.y);
                return false;
            }

            origins[stack] = new Vector2Int(x, y);
            FillCells(stack, x, y);
            return true;
        }

        public bool TryRotate(InventoryItemStack stack)
        {
            if (stack == null || !origins.TryGetValue(stack, out Vector2Int origin))
            {
                return false;
            }

            ClearCells(stack);
            stack.Rotated = !stack.Rotated;
            if (!CanPlace(stack, origin.x, origin.y, true))
            {
                stack.Rotated = !stack.Rotated;
                FillCells(stack, origin.x, origin.y);
                return false;
            }

            FillCells(stack, origin.x, origin.y);
            return true;
        }

        public bool Remove(InventoryItemStack stack)
        {
            if (stack == null || !origins.ContainsKey(stack))
            {
                return false;
            }

            ClearCells(stack);
            origins.Remove(stack);
            stacks.Remove(stack);
            return true;
        }

        public InventoryItemStack GetAt(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height ? cells[x, y] : null;
        }

        public Vector2Int GetOrigin(InventoryItemStack stack)
        {
            return origins.TryGetValue(stack, out Vector2Int origin) ? origin : new Vector2Int(-1, -1);
        }

        public int CountItem(string itemId)
        {
            int count = 0;
            foreach (InventoryItemStack stack in stacks)
            {
                if (stack.Definition != null && stack.Definition.ItemId == itemId)
                {
                    count += stack.Quantity;
                }
            }
            return count;
        }

        public int AddItem(InventoryItemDefinition definition, int quantity)
        {
            if (definition == null || quantity <= 0)
            {
                return quantity;
            }

            int remaining = quantity;
            foreach (InventoryItemStack stack in stacks)
            {
                if (stack.Definition == definition && stack.Quantity < stack.MaxStack)
                {
                    int move = Mathf.Min(remaining, stack.MaxStack - stack.Quantity);
                    stack.Quantity += move;
                    remaining -= move;
                    if (remaining <= 0)
                    {
                        return 0;
                    }
                }
            }

            while (remaining > 0)
            {
                InventoryItemStack stack = new InventoryItemStack(definition, Mathf.Min(remaining, definition.MaxStack));
                if (!TryFindPlacement(stack, out Vector2Int origin) || !TryPlace(stack, origin.x, origin.y))
                {
                    break;
                }

                remaining -= stack.Quantity;
            }

            return remaining;
        }

        public bool TryRemoveItems(string itemId, int quantity)
        {
            if (CountItem(itemId) < quantity)
            {
                return false;
            }

            int remaining = quantity;
            for (int i = stacks.Count - 1; i >= 0 && remaining > 0; i--)
            {
                InventoryItemStack stack = stacks[i];
                if (stack.Definition == null || stack.Definition.ItemId != itemId)
                {
                    continue;
                }

                int remove = Mathf.Min(remaining, stack.Quantity);
                stack.Quantity -= remove;
                remaining -= remove;
                if (stack.Quantity <= 0)
                {
                    Remove(stack);
                }
            }

            return true;
        }

        public bool TryQuickTransferTo(InventoryGrid target, InventoryItemStack stack)
        {
            if (target == null || stack == null || !stacks.Contains(stack))
            {
                return false;
            }

            InventoryItemStack moving = stack.Clone();
            int leftover = target.AddItem(moving.Definition, moving.Quantity);
            int moved = moving.Quantity - leftover;
            if (moved <= 0)
            {
                return false;
            }

            stack.Quantity -= moved;
            if (stack.Quantity <= 0)
            {
                Remove(stack);
            }
            return true;
        }

        public float TotalWeight()
        {
            float weight = 0f;
            foreach (InventoryItemStack stack in stacks)
            {
                weight += stack.TotalWeight;
            }
            return weight;
        }

        private bool TryFindPlacement(InventoryItemStack stack, out Vector2Int origin)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (CanPlace(stack, x, y))
                    {
                        origin = new Vector2Int(x, y);
                        return true;
                    }
                }
            }

            origin = default;
            return false;
        }

        private void FillCells(InventoryItemStack stack, int x, int y)
        {
            for (int ix = x; ix < x + stack.Width; ix++)
            {
                for (int iy = y; iy < y + stack.Height; iy++)
                {
                    cells[ix, iy] = stack;
                }
            }
        }

        private void ClearCells(InventoryItemStack stack)
        {
            for (int ix = 0; ix < Width; ix++)
            {
                for (int iy = 0; iy < Height; iy++)
                {
                    if (cells[ix, iy] == stack)
                    {
                        cells[ix, iy] = null;
                    }
                }
            }
        }
    }
}
