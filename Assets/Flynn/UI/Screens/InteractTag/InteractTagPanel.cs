using UnityEngine;
using UnityEngine.UIElements;


using Flynn.Core;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.UI.Screens.InteractTag
{
    /// <summary>
    /// Screen-space interaction tag that follows the mouse cursor while an
    /// interactable is hovered. Driven by <see cref="WorldInteractTagPresenter"/>
    /// when a hover is active the presenter calls <see cref="Show"/> with the
    /// prompt text; the tag then follows the cursor each frame until
    /// <see cref="HideTag"/> is called.
    ///
    /// Lives as a pre-existing UIDocument under the UI manager so designers can
    /// customise the UXML/USS. Uses the Flynn mono palette (black bg, 1px white outline).
    /// </summary>
    public class InteractTagPanel : UIToolkitPanel
    {
        private Label _label;
        private VisualElement _root;

        protected override bool Bind(VisualElement root)
        {
            _root = root.Q<VisualElement>("InteractTagRoot");
            _label = root.Q<Label>("InteractTagLabel");
            return _root != null && _label != null;
        }

        private void LateUpdate()
        {
            if (!IsVisible || _root == null) return;

            var panel = _root.panel;
            if (panel == null) return;

            var mp = MousePointer.Instance;
            Vector2 mouse = mp != null ? mp.ScreenPosition : Input.mousePosition;
            var contentRect = panel.visualTree.layout;
            float scaleX = contentRect.width > 0 ? Screen.width / contentRect.width : 1f;
            float scaleY = contentRect.height > 0 ? Screen.height / contentRect.height : 1f;

            _root.style.left = mouse.x / scaleX;
            _root.style.top = (Screen.height - mouse.y) / scaleY;
        }

        /// <summary>Show the tag with the given prompt text. It will follow the mouse cursor.</summary>
        public void Show(string text)
        {
            if (_label != null) _label.text = text;
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            IsVisible = true;
            ApplyRootDisplay();
        }

        /// <summary>Hide the tag.</summary>
        public void HideTag()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            IsVisible = false;
            ApplyRootDisplay();
        }

        private void ApplyRootDisplay()
        {
            if (Root != null)
                Root.style.display = IsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

}
