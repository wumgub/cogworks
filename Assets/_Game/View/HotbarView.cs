using Cogworks.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cogworks.View
{
    /// <summary>In-game HUD: tool hotbar with item counts, hover info, and toast messages.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HotbarView : MonoBehaviour
    {
        const string SelectedClass = "slot--selected";
        const string EmptyClass = "slot--empty";
        const float ToastSeconds = 3f;

        [SerializeField] BuildController build;

        VisualElement _root;
        VisualElement[] _slots;
        Label[] _counts;
        Label _selectedName, _hoverInfo, _toast;
        IPanel _panel;
        float _toastUntil;
        ResearchState _research;

        // Start, not OnEnable: UIDocument builds its tree in its own OnEnable, and order between components isn't guaranteed.
        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _root = root.Q<VisualElement>("hud-root");
            _panel = root.panel;
            _selectedName = root.Q<Label>("selected-name");
            _hoverInfo = root.Q<Label>("hover-info");
            _toast = root.Q<Label>("toast");
            var bar = root.Q<VisualElement>("hotbar");
            bar.Clear();

            var slots = BuildController.HotbarSlots;
            _slots = new VisualElement[slots.Length];
            _counts = new Label[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var spec = slots[i];
                var slot = new VisualElement { tooltip = spec.DisplayName };
                slot.AddToClassList("slot");

                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("slot-icon");
                icon.style.backgroundImage = new StyleBackground(GameArt.BuildingSprite(spec));
                slot.Add(icon);

                var key = new Label(((i + 1) % 10).ToString()) { pickingMode = PickingMode.Ignore };
                key.AddToClassList("slot-key");
                slot.Add(key);

                var count = new Label { pickingMode = PickingMode.Ignore };
                count.AddToClassList("slot-count");
                slot.Add(count);

                int index = i;
                slot.RegisterCallback<ClickEvent>(_ => build.SelectSlot(index));
                bar.Add(slot);
                _slots[i] = slot;
                _counts[i] = count;
            }

            build.SelectionChanged += Refresh;
            build.Message += Toast;
            _toast.style.display = DisplayStyle.None;
            Refresh();
        }

        void OnDestroy()
        {
            if (build == null) return;
            build.SelectionChanged -= Refresh;
            build.Message -= Toast;
        }

        public void Toast(string message)
        {
            _toast.text = message;
            _toast.style.display = DisplayStyle.Flex;
            _toastUntil = Time.unscaledTime + ToastSeconds;
        }

        void Update()
        {
            var bootstrap = GameBootstrap.Instance;
            var sim = bootstrap != null ? bootstrap.Sim : null;
            if (_root == null) return;
            // Hidden on the main menu and while a client waits for its world.
            _root.style.display = sim != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (sim == null) return;

            WatchResearch(sim.World.Research);

            for (int i = 0; i < _slots.Length; i++)
            {
                int n = build.Available(BuildController.HotbarSlots[i]);
                _counts[i].text = n == int.MaxValue ? "" : n.ToString();
                _slots[i].EnableInClassList(EmptyClass, n == 0);
            }

            string hover = build.Hovered != null ? Describe.Hover(sim.World, build.Hovered)
                : build.Selected == null ? Describe.Tile(sim.World, build.CursorTile) : "";
            _hoverInfo.text = hover;
            _hoverInfo.style.display = hover.Length > 0 && build.InputEnabled ? DisplayStyle.Flex : DisplayStyle.None;

            if (_toast.style.display == DisplayStyle.Flex && Time.unscaledTime > _toastUntil)
                _toast.style.display = DisplayStyle.None;
        }

        void WatchResearch(ResearchState research)
        {
            if (research == _research) return;
            if (_research != null) _research.Completed -= OnResearchDone;
            _research = research;
            _research.Completed += OnResearchDone;
        }

        void OnResearchDone(Tech tech) => Toast($"Research complete: {tech.DisplayName}");

        void Refresh()
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i].EnableInClassList(SelectedClass, BuildController.HotbarSlots[i] == build.Selected);
            _selectedName.text = build.Selected?.DisplayName ?? "";
            _selectedName.style.display = build.Selected != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>True when the screen-space pointer is over an interactive element of any game UI panel.</summary>
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
