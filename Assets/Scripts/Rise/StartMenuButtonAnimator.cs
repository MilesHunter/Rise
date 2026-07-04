using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

namespace Rise
{
    public sealed class StartMenuButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private Image background;
        [SerializeField] private Text label;
        [SerializeField] private Color normalBackground = new Color(0.08f, 0.11f, 0.15f, 0.78f);
        [SerializeField] private Color hoverBackground = new Color(0.16f, 0.27f, 0.34f, 0.92f);
        [SerializeField] private Color pressedBackground = new Color(0.8f, 0.66f, 0.38f, 0.95f);
        [SerializeField] private Color normalText = new Color(0.86f, 0.91f, 0.93f, 1f);
        [SerializeField] private Color hoverText = Color.white;
        [SerializeField] private Color pressedText = new Color(0.06f, 0.08f, 0.1f, 1f);

        private Vector3 baseScale = Vector3.one;
        private bool pointerInside;
        private bool selected;

        public void Initialize(RectTransform buttonTarget, Image buttonBackground, Text buttonLabel)
        {
            target = buttonTarget;
            background = buttonBackground;
            label = buttonLabel;
            baseScale = target != null ? target.localScale : Vector3.one;
            ApplyInstant(false, false);
        }

        private void Awake()
        {
            if (target == null)
            {
                target = transform as RectTransform;
            }

            baseScale = target != null ? target.localScale : Vector3.one;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            AnimateState(true, false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            AnimateState(selected, false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            AnimateState(true, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            AnimateState(pointerInside || selected, false);
        }

        public void PlayClickPulse()
        {
            if (target == null)
            {
                return;
            }

            target.DOKill();
            target.localScale = baseScale;
            target.DOPunchScale(new Vector3(0.045f, 0.045f, 0f), 0.22f, 6, 0.55f)
                .SetUpdate(true);
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
            AnimateState(true, false);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            AnimateState(pointerInside, false);
        }

        private void ApplyInstant(bool highlighted, bool pressed)
        {
            if (target != null)
            {
                target.localScale = baseScale * (pressed ? 0.98f : highlighted ? 1.045f : 1f);
            }

            if (background != null)
            {
                background.color = pressed ? pressedBackground : highlighted ? hoverBackground : normalBackground;
            }

            if (label != null)
            {
                label.color = pressed ? pressedText : highlighted ? hoverText : normalText;
            }
        }

        private void AnimateState(bool highlighted, bool pressed)
        {
            float scale = pressed ? 0.98f : highlighted ? 1.045f : 1f;
            Color backgroundColor = pressed ? pressedBackground : highlighted ? hoverBackground : normalBackground;
            Color textColor = pressed ? pressedText : highlighted ? hoverText : normalText;

            if (target != null)
            {
                target.DOKill();
                target.DOScale(baseScale * scale, 0.18f).SetEase(Ease.OutCubic).SetUpdate(true);
            }

            if (background != null)
            {
                background.DOKill();
                background.DOColor(backgroundColor, 0.16f).SetEase(Ease.OutCubic).SetUpdate(true);
            }

            if (label != null)
            {
                label.DOKill();
                label.DOColor(textColor, 0.16f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }
    }
}
