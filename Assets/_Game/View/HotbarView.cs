using Cogworks.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cogworks.View
{
    /// <summary>UI Toolkit hotbar bound to <see cref="BuildController"/> selection.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HotbarView : MonoBehaviour
    {
        const string SelectedClass = "slot--selected";

        [SerializeField] BuildController build;

        VisualElement _root;
        VisualElement[] _slots;
        Label _selectedName;
        IPanel _panel;

        // Start, not OnEnable: UIDocument builds its tree in its own OnEnable, and order between components isn't guaranteed.
        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _root = root.Q<VisualElement>("hud-root");
            _panel = root.panel;
            _selectedName = root.Q<Label>("selected-name");
            var bar = root.Q<VisualElement>("hotbar");
            bar.Clear();

            _slots = new VisualElement[BuildingCatalog.All.Count];
            for (int i = 0; i < _slots.Length; i++)
            {
                var spec = BuildingCatalog.All[i];
                var slot = new VisualElement { tooltip = spec.DisplayName };
                slot.AddToClassList("slot");

                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("slot-icon");
                icon.style.backgroundImage = new StyleBackground(PlaceholderArt.BuildingSprite(spec));
                slot.Add(icon);

                var key = new Label((i + 1).ToString()) { pickingMode = PickingMode.Ignore };
                key.AddToClassList("slot-key");
                slot.Add(key);

                int index = i;
                slot.RegisterCallback<ClickEvent>(_ => build.Select(index));
                bar.Add(slot);
                _slots[i] = slot;
            }

            build.SelectionChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (build != null) build.SelectionChanged -= Refresh;
        }

        void Update()
        {
            // Hidden on the main menu and while a client waits for its world.
            if (_root != null)
                _root.style.display = GameBootstrap.Instance?.Sim != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void Refresh()
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i].EnableInClassList(SelectedClass, i == build.SelectedIndex);
            _selectedName.text = build.Selected?.DisplayName ?? "";
        }

        /// <summary>True when the screen-space pointer is over an interactive HUD element.</summary>
        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            if (_panel == null) return false;
            // Input System screen space has y up; panels have y down.
            var flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_panel, flipped);
            return _panel.Pick(panelPos) != null;
        }
    }
}
