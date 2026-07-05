using UnityEngine;


using Flynn.Core;

namespace Flynn.UI.Core
{
    /// <summary>
    /// Stable identity for a reusable UI screen/widget. Enum instead of a string key so routing
    /// through <see cref="UIManager"/> has no magic strings and shows up in the Inspector as a dropdown.
    /// Extend as screens are added.
    /// </summary>
    public enum UIPanelId
    {
        None = 0,
        Hud,
        Inventory,
        Dialogue,
        Pause,
        InteractTag,
        ResourceHP,
    }

    /// <summary>
    /// A reusable UI component the <see cref="UIManager"/> can show/hide by <see cref="UIPanelId"/>.
    /// Implemented as an interface so the manager never depends on a concrete screen type.
    /// </summary>
    public interface IUIPanel
    {
        UIPanelId PanelId { get; }
        bool IsVisible { get; }
        void Show();
        void Hide();
    }

    /// <summary>
    /// Base class for reusable UI screens driven by <see cref="UIManager"/>. Default Show/Hide just
    /// toggles the GameObject; override <see cref="OnShow"/> / <see cref="OnHide"/> for animation,
    /// data binding, focus, etc. A concrete screen (e.g. an inventory panel) extends this, picks its
    /// <see cref="UIPanelId"/> in the Inspector, and gets registered with the manager — either by
    /// being dropped into the manager's serialized list, or via runtime <see cref="UIManager.Register"/>.
    /// </summary>
    public abstract class UIPanel : MonoBehaviour, IUIPanel
    {
        [SerializeField] private UIPanelId _panelId = UIPanelId.None;

        public UIPanelId PanelId => _panelId;
        public bool IsVisible { get; protected set; }

        public virtual void Show()
        {
            if (IsVisible) return;
            gameObject.SetActive(true);
            IsVisible = true;
            OnShow();
        }

        public virtual void Hide()
        {
            if (!IsVisible) return;
            OnHide();
            IsVisible = false;
            gameObject.SetActive(false);
        }

        /// <summary>Called after the panel becomes visible. Bind data / play intro here.</summary>
        protected virtual void OnShow() { }

        /// <summary>Called before the panel hides. Clean up / play outro here.</summary>
        protected virtual void OnHide() { }
    }

}
