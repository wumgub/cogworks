using System;
using System.Collections.Generic;
using Cogworks.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Cogworks.View
{
    /// <summary>
    /// The in-game windows: inventory with hand crafting (Tab), machine panels (click a building) and
    /// research (T). Everything shown is read from the sim; every action is a command.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(-10)]
    public sealed class PanelsController : MonoBehaviour
    {
        enum Panel
        {
            None,
            Inventory,
            Machine,
            Research,
        }

        const float RefreshInterval = 0.1f;

        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] BuildController build;

        VisualElement _inventory, _machine, _research;
        SlotGrid _inventoryGrid, _machinePlayerGrid;
        VisualElement _craftQueue, _machineBody;
        ScrollView _craftList, _techList;
        Label _machineTitle, _machineStatus, _machineNetwork;
        readonly List<CraftRow> _craftRows = new List<CraftRow>();
        readonly List<TechRow> _techRows = new List<TechRow>();

        Panel _open;
        int _machineId;
        Action<Simulation, Building> _refreshMachine;
        float _nextRefresh;

        public bool AnyOpen => _open != Panel.None;

        /// <summary>Set false while the pause/main menu is open so hotkeys don't open windows behind it.</summary>
        public bool KeysEnabled { get; set; } = true;

        /// <summary>Frame on which Esc closed a window, so the pause menu doesn't also open.</summary>
        public int EscapeConsumedFrame { get; private set; } = -1;

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _inventory = root.Q("inventory-panel");
            _machine = root.Q("machine-panel");
            _research = root.Q("research-panel");
            _craftQueue = root.Q("craft-queue");
            _craftList = root.Q<ScrollView>("craft-list");
            _techList = root.Q<ScrollView>("tech-list");
            _machineBody = root.Q("machine-body");
            _machineTitle = root.Q<Label>("machine-title");
            _machineStatus = root.Q<Label>("machine-status");
            _machineNetwork = root.Q<Label>("machine-network");

            _inventoryGrid = new SlotGrid(root.Q("inventory-grid"), PlayerState.InventorySlots);
            _inventoryGrid.Clicked += OnInventorySlotClicked;
            _machinePlayerGrid = new SlotGrid(root.Q("machine-player-grid"), PlayerState.InventorySlots);
            _machinePlayerGrid.Clicked += OnMachinePlayerSlotClicked;

            root.Q<Button>("inventory-close").clicked += Close;
            root.Q<Button>("machine-close").clicked += Close;
            root.Q<Button>("research-close").clicked += Close;

            foreach (var recipe in RecipeCatalog.All)
                if (recipe.Station == CraftStation.Assembler) _craftRows.Add(new CraftRow(this, recipe));
            foreach (var tech in TechCatalog.All) _techRows.Add(new TechRow(this, tech));

            build.BuildingClicked += OpenMachine;
            Show(Panel.None);
        }

        void OnDestroy()
        {
            if (build != null) build.BuildingClicked -= OpenMachine;
        }

        void Update()
        {
            var sim = bootstrap.Sim;
            if (sim == null)
            {
                if (_open != Panel.None) Show(Panel.None);
                return;
            }

            var kb = Keyboard.current;
            if (kb != null && KeysEnabled)
            {
                if (kb.tabKey.wasPressedThisFrame) Toggle(Panel.Inventory);
                if (kb.tKey.wasPressedThisFrame) Toggle(Panel.Research);
                if (kb.escapeKey.wasPressedThisFrame && _open != Panel.None)
                {
                    Close();
                    EscapeConsumedFrame = Time.frameCount;
                }
            }

            if (_open != Panel.None && Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshInterval;
                Refresh(sim);
            }
        }

        public void Close() => Show(Panel.None);

        public void OpenInventory() => Show(Panel.Inventory);

        public void OpenResearch() => Show(Panel.Research);

        void Toggle(Panel panel) => Show(_open == panel ? Panel.None : panel);

        void Show(Panel panel)
        {
            _open = panel;
            _inventory.style.display = panel == Panel.Inventory ? DisplayStyle.Flex : DisplayStyle.None;
            _machine.style.display = panel == Panel.Machine ? DisplayStyle.Flex : DisplayStyle.None;
            _research.style.display = panel == Panel.Research ? DisplayStyle.Flex : DisplayStyle.None;
            _nextRefresh = 0f;
        }

        void Submit(Command c)
        {
            bootstrap.Commands?.Submit(c);
            SoundFx.Instance?.Play(Sfx.Click, 0.7f);
        }

        void Refresh(Simulation sim)
        {
            var player = bootstrap.LocalPlayer;
            switch (_open)
            {
                case Panel.Inventory:
                    if (player != null) RefreshInventory(player);
                    break;
                case Panel.Machine:
                    var b = sim.World.Buildings.Get(_machineId);
                    if (b == null)
                    {
                        Close();
                        return;
                    }
                    _machineStatus.text = Describe.Status(sim.World, b);
                    _machineNetwork.text = b.Spec.Kinetic != KineticRole.None ? "Rotation: " + Describe.Network(sim.World, b) : "";
                    _machineNetwork.style.display = _machineNetwork.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                    _refreshMachine?.Invoke(sim, b);
                    if (player != null) _machinePlayerGrid.Show(player.Inventory);
                    break;
                case Panel.Research:
                    foreach (var row in _techRows) row.Refresh(sim.World.Research);
                    break;
            }
        }

        // ------------------------------------------------------------------ inventory & crafting

        void RefreshInventory(PlayerState player)
        {
            _inventoryGrid.Show(player.Inventory);
            var research = bootstrap.Sim.World.Research;
            foreach (var row in _craftRows) row.Refresh(player, research);

            _craftQueue.Clear();
            for (int i = 0; i < player.CraftQueue.Count && i < 20; i++)
            {
                var recipe = RecipeCatalog.Get(player.CraftQueue[i]);
                if (recipe == null) continue;
                var slot = SlotGrid.MakeSlot(out var icon, out var count);
                icon.style.backgroundImage = new StyleBackground(GameArt.ItemSprite(recipe.Output.Item));
                count.text = i == 0 ? $"{100 * player.CraftProgress / Math.Max(1, recipe.Ticks)}%" : "";
                slot.tooltip = recipe.DisplayName;
                _craftQueue.Add(slot);
            }
            if (player.CraftQueue.Count > 20) _craftQueue.Add(new Label($"+{player.CraftQueue.Count - 20}"));
        }

        void OnInventorySlotClicked(int index, EventBase evt)
        {
            var player = bootstrap.LocalPlayer;
            if (player == null) return;
            var stack = player.Inventory[index];
            var places = ItemCatalog.Get(stack.Item)?.Places;
            if (places == null) return;
            // Picking a building item makes it the active tool.
            if (build.Selected != places) build.Select(places);
            Close();
        }

        void Craft(Recipe recipe, int count) => Submit(new HandCraftCommand(recipe.Id, count));

        sealed class CraftRow
        {
            readonly Recipe _recipe;
            readonly VisualElement _row;
            readonly Label _name;

            public CraftRow(PanelsController owner, Recipe recipe)
            {
                _recipe = recipe;
                _row = new VisualElement();
                _row.AddToClassList("craft-row");
                var icon = new VisualElement();
                icon.AddToClassList("item-icon");
                icon.style.backgroundImage = new StyleBackground(GameArt.ItemSprite(recipe.Output.Item));
                _row.Add(icon);

                var text = new VisualElement();
                text.AddToClassList("craft-text");
                _name = new Label(recipe.DisplayName);
                _name.AddToClassList("craft-name");
                text.Add(_name);
                var inputs = new List<string>();
                foreach (var s in recipe.Inputs) inputs.Add($"{s.Count}× {ItemCatalog.NameOf(s.Item)}");
                var inputLabel = new Label(string.Join(",  ", inputs) + $"   ({recipe.Ticks / (float)Simulation.TicksPerSecond:0.#}s)");
                inputLabel.AddToClassList("craft-inputs");
                text.Add(inputLabel);
                _row.Add(text);

                _row.RegisterCallback<ClickEvent>(e => owner.Craft(recipe, e.shiftKey ? 5 : 1));
                _row.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button == 1) owner.Craft(recipe, 5);
                });
                owner._craftList.Add(_row);
            }

            public void Refresh(PlayerState player, ResearchState research)
            {
                bool unlocked = research.IsUnlocked(_recipe);
                _row.style.display = unlocked ? DisplayStyle.Flex : DisplayStyle.None;
                if (!unlocked) return;
                int can = int.MaxValue;
                foreach (var s in _recipe.Inputs) can = Math.Min(can, player.Inventory.Count(s.Item) / s.Count);
                _row.EnableInClassList("craft-row--unaffordable", can == 0);
                _name.text = _recipe.DisplayName + (can > 0 ? $"   (can make {can})" : "");
            }
        }

        // ------------------------------------------------------------------ machines

        public void OpenMachine(Building b)
        {
            _machineId = b.Id;
            _machineTitle.text = b.Spec.DisplayName;
            _machineBody.Clear();
            _refreshMachine = BuildMachineBody(b);
            _machineBody.style.display = _machineBody.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            Show(Panel.Machine);
        }

        void TakeOut(ushort item, int count)
        {
            var b = bootstrap.Sim?.World.Buildings.Get(_machineId);
            if (b != null && item != ItemCatalog.None && count > 0)
                Submit(new TransferItemsCommand(b.Origin, item, count, intoBuilding: false));
        }

        void OnMachinePlayerSlotClicked(int index, EventBase evt)
        {
            var player = bootstrap.LocalPlayer;
            var b = bootstrap.Sim?.World.Buildings.Get(_machineId);
            if (player == null || b == null) return;
            var stack = player.Inventory[index];
            if (stack.IsEmpty) return;
            bool all = evt is IPointerEvent p && p.shiftKey;
            int count = all ? player.Inventory.Count(stack.Item) : stack.Count;
            Submit(new TransferItemsCommand(b.Origin, stack.Item, count, intoBuilding: true));
        }

        /// <summary>Builds the type-specific part of the machine window; returns how to refresh it.</summary>
        Action<Simulation, Building> BuildMachineBody(Building b)
        {
            switch (b.Logic)
            {
                case ChestLogic _:
                {
                    var grid = new SlotGrid(_machineBody, 16);
                    grid.Clicked += (i, _) => TakeOut(grid.ItemAt(i), grid.CountAt(i));
                    return (sim, bb) => grid.Show(((ChestLogic)bb.Logic).Inventory);
                }
                case FurnaceLogic _:
                {
                    var row = Row();
                    var input = LabeledSlot(row, "Input");
                    var fuel = LabeledSlot(row, "Fuel");
                    var output = LabeledSlot(row, "Output");
                    foreach (var s in new[] { input, fuel, output }) s.Clicked += (i, _) => TakeOut(s.ItemAt(0), s.CountAt(0));
                    var progress = Bar("Smelting", "bar-fill");
                    var burn = Bar("Fuel left", "bar-fill--fuel");
                    return (sim, bb) =>
                    {
                        var f = (FurnaceLogic)bb.Logic;
                        input.Set(0, f.Input.Item, f.Input.Count);
                        fuel.Set(0, f.Fuel.Item, f.Fuel.Count);
                        output.Set(0, f.Output.Item, f.Output.Count);
                        SetBar(progress, f.CurrentRecipe != null ? f.Progress / (float)f.CurrentRecipe.Ticks : 0f);
                        SetBar(burn, f.BurnTotal > 0 ? f.BurnLeft / (float)f.BurnTotal : 0f);
                    };
                }
                case AssemblerLogic _:
                {
                    _machineBody.Add(SectionLabel("Recipe"));
                    var recipes = new VisualElement();
                    recipes.AddToClassList("recipe-grid");
                    _machineBody.Add(recipes);
                    var buttons = new List<(Recipe recipe, VisualElement btn)>();
                    foreach (var r in RecipeCatalog.All)
                    {
                        if (r.Station != CraftStation.Assembler) continue;
                        var btn = new VisualElement { tooltip = r.DisplayName };
                        btn.AddToClassList("recipe-btn");
                        var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                        icon.AddToClassList("item-icon");
                        icon.style.backgroundImage = new StyleBackground(GameArt.ItemSprite(r.Output.Item));
                        btn.Add(icon);
                        var recipe = r;
                        btn.RegisterCallback<ClickEvent>(_ =>
                        {
                            var bb = bootstrap.Sim?.World.Buildings.Get(_machineId);
                            if (bb != null) Submit(new SetRecipeCommand(bb.Origin, recipe.Id));
                        });
                        recipes.Add(btn);
                        buttons.Add((r, btn));
                    }
                    _machineBody.Add(SectionLabel("Ingredients → Output"));
                    var row = Row();
                    var inputs = new SlotGrid(row, 6);
                    inputs.Clicked += (i, _) => TakeOut(inputs.ItemAt(i), inputs.CountAt(i));
                    var output = LabeledSlot(row, "Output");
                    output.Clicked += (i, _) => TakeOut(output.ItemAt(0), output.CountAt(0));
                    var progress = Bar("Progress", "bar-fill");
                    return (sim, bb) =>
                    {
                        var a = (AssemblerLogic)bb.Logic;
                        foreach (var (recipe, btn) in buttons)
                        {
                            btn.style.display = sim.World.Research.IsUnlocked(recipe) ? DisplayStyle.Flex : DisplayStyle.None;
                            btn.EnableInClassList("recipe-btn--selected", recipe.Id == a.RecipeId);
                        }
                        inputs.Show(a.Inputs);
                        output.Set(0, a.Output.Item, a.Output.Count);
                        SetBar(progress, a.Crafting ? a.Progress / (float)a.WorkNeeded : 0f);
                    };
                }
                case DrillLogic _:
                {
                    var row = Row();
                    var output = LabeledSlot(row, "Output");
                    output.Clicked += (i, _) => TakeOut(output.ItemAt(0), output.CountAt(0));
                    var ore = new Label();
                    ore.AddToClassList("status-line");
                    row.Add(ore);
                    var progress = Bar("Mining", "bar-fill");
                    return (sim, bb) =>
                    {
                        var d = (DrillLogic)bb.Logic;
                        output.Set(0, d.Output, d.Output != ItemCatalog.None ? 1 : 0);
                        int left = 0;
                        foreach (var t in bb.Footprint())
                        {
                            sim.World.GetOre(t, out int amount);
                            left += amount;
                        }
                        ore.text = $"Ore remaining: {left}";
                        SetBar(progress, d.Progress / (float)DrillLogic.WorkPerItem);
                    };
                }
                case GeneratorLogic _ when b.Spec == BuildingCatalog.HandCrank:
                {
                    var crank = new Button(() =>
                    {
                        var bb = bootstrap.Sim?.World.Buildings.Get(_machineId);
                        if (bb != null) Submit(new CrankCommand(bb.Origin));
                    }) { text = "Crank it! (or press E while hovering)" };
                    crank.AddToClassList("action-btn");
                    _machineBody.Add(crank);
                    var left = Bar("Turning", "bar-fill");
                    return (sim, bb) => SetBar(left, ((GeneratorLogic)bb.Logic).CrankLeft / (float)GeneratorLogic.CrankTicks);
                }
                case GeneratorLogic _ when b.Spec == BuildingCatalog.SteamEngine:
                {
                    var row = Row();
                    var fuel = LabeledSlot(row, "Coal");
                    fuel.Clicked += (i, _) => TakeOut(fuel.ItemAt(0), fuel.CountAt(0));
                    var burn = Bar("Burning", "bar-fill--fuel");
                    return (sim, bb) =>
                    {
                        var g = (GeneratorLogic)bb.Logic;
                        fuel.Set(0, g.Fuel.Item, g.Fuel.Count);
                        SetBar(burn, g.BurnLeft / (float)ItemCatalog.Coal.FuelTicks);
                    };
                }
                case GeneratorLogic _:
                    _machineBody.Add(Hint($"Supplies {b.Spec.GeneratorRpm} rpm and {b.Spec.StressCapacity} SU while turning."));
                    return null;
                case LabLogic _:
                {
                    var row = Row();
                    var packs = new SlotGrid(row, 2);
                    packs.Clicked += (i, _) => TakeOut(packs.ItemAt(i), packs.CountAt(i));
                    var current = new Label();
                    current.AddToClassList("status-line");
                    _machineBody.Add(current);
                    var progress = Bar("Pack", "bar-fill--research");
                    var open = new Button(() => Show(Panel.Research)) { text = "Choose research (T)" };
                    open.AddToClassList("action-btn");
                    _machineBody.Add(open);
                    return (sim, bb) =>
                    {
                        var l = (LabLogic)bb.Logic;
                        packs.Show(l.Packs);
                        var tech = sim.World.Research.Current;
                        current.text = tech != null ? $"Researching: {tech.DisplayName}" : "No research selected";
                        SetBar(progress, l.Progress / (float)LabLogic.TicksPerPack);
                    };
                }
                case InserterLogic _:
                {
                    var row = Row();
                    var held = LabeledSlot(row, "Holding");
                    _machineBody.Add(Hint("Takes from the tile behind and drops on the tile in front (arrow)."));
                    return (sim, bb) =>
                    {
                        var ins = (InserterLogic)bb.Logic;
                        held.Set(0, ins.Held, ins.Held != ItemCatalog.None ? 1 : 0);
                    };
                }
                case ConveyorLogic _:
                    _machineBody.Add(Hint($"Moves {b.Spec.BeltSpeed * Simulation.TicksPerSecond / (float)ConveyorLogic.Length:0.##} tiles/s, up to 4 items per tile."));
                    return null;
                default:
                    if (b.Spec.Kinetic == KineticRole.Shaft) _machineBody.Add(Hint("Carries rotation along its length. Use a gearbox to turn corners."));
                    if (b.Spec.Kinetic == KineticRole.Gearbox) _machineBody.Add(Hint("Connects rotation on all four sides."));
                    return null;
            }
        }

        VisualElement Row()
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            _machineBody.Add(row);
            return row;
        }

        static Label SectionLabel(string text)
        {
            var l = new Label(text);
            l.AddToClassList("section-label");
            return l;
        }

        static Label Hint(string text)
        {
            var l = new Label(text);
            l.AddToClassList("hint-line");
            return l;
        }

        static SlotGrid LabeledSlot(VisualElement parent, string label)
        {
            var box = new VisualElement();
            box.AddToClassList("labeled-slot");
            parent.Add(box);
            var grid = new SlotGrid(box, 1, big: true);
            box.Add(new Label(label));
            return grid;
        }

        VisualElement Bar(string label, string fillClass)
        {
            var row = Row();
            var l = new Label(label);
            l.AddToClassList("bar-label");
            row.Add(l);
            var track = new VisualElement();
            track.AddToClassList("bar");
            var fill = new VisualElement();
            fill.AddToClassList("bar-fill");
            if (fillClass != "bar-fill") fill.AddToClassList(fillClass);
            track.Add(fill);
            row.Add(track);
            return fill;
        }

        static void SetBar(VisualElement fill, float fraction) =>
            fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);

        // ------------------------------------------------------------------ research

        sealed class TechRow
        {
            readonly Tech _tech;
            readonly VisualElement _row;
            readonly Label _state;
            readonly VisualElement _fill;

            public TechRow(PanelsController owner, Tech tech)
            {
                _tech = tech;
                _row = new VisualElement();
                _row.AddToClassList("tech-row");
                var name = new Label(tech.DisplayName);
                name.AddToClassList("tech-name");
                _row.Add(name);
                var desc = new Label(tech.Description + "  Unlocks: " + string.Join(", ", Array.ConvertAll(tech.Unlocks, r => r.DisplayName)));
                desc.AddToClassList("tech-desc");
                _row.Add(desc);
                _state = new Label();
                _state.AddToClassList("tech-state");
                _row.Add(_state);
                var track = new VisualElement();
                track.AddToClassList("bar");
                _fill = new VisualElement();
                _fill.AddToClassList("bar-fill");
                _fill.AddToClassList("bar-fill--research");
                track.Add(_fill);
                _row.Add(track);
                _row.RegisterCallback<ClickEvent>(_ =>
                {
                    var research = owner.bootstrap.Sim?.World.Research;
                    if (research != null && research.IsAvailable(tech) && research.Current != tech)
                        owner.Submit(new SetResearchCommand(tech.Id));
                });
                owner._techList.Add(_row);
            }

            public void Refresh(ResearchState research)
            {
                bool done = research.IsDone(_tech);
                bool current = research.Current == _tech;
                bool available = research.IsAvailable(_tech);
                _row.EnableInClassList("tech-row--done", done);
                _row.EnableInClassList("tech-row--current", current);
                _row.EnableInClassList("tech-row--available", available && !current);
                _row.EnableInClassList("tech-row--locked", !done && !available);

                int have = 0, need = 0;
                var parts = new List<string>();
                for (int i = 0; i < _tech.Cost.Length; i++)
                {
                    int p = research.ProgressOf(_tech, i);
                    have += p;
                    need += _tech.Cost[i].Count;
                    parts.Add($"{p}/{_tech.Cost[i].Count} {ItemCatalog.NameOf(_tech.Cost[i].Item)}");
                }
                string cost = string.Join(",  ", parts);
                if (done) _state.text = "✓ Researched";
                else if (current) _state.text = "Researching  ·  " + cost;
                else if (available) _state.text = "Click to research  ·  " + cost;
                else _state.text = "Requires " + string.Join(", ", Array.ConvertAll(_tech.Prerequisites, t => t.DisplayName));
                SetBar(_fill, need > 0 ? have / (float)need : 0f);
            }
        }
    }

    /// <summary>A fixed set of item slots (icon + count) created once and updated in place.</summary>
    public sealed class SlotGrid
    {
        readonly VisualElement[] _slots;
        readonly VisualElement[] _icons;
        readonly Label[] _counts;
        readonly ushort[] _items;
        readonly int[] _amounts;

        public event Action<int, EventBase> Clicked;

        public SlotGrid(VisualElement container, int count, bool big = false)
        {
            _slots = new VisualElement[count];
            _icons = new VisualElement[count];
            _counts = new Label[count];
            _items = new ushort[count];
            _amounts = new int[count];
            for (int i = 0; i < count; i++)
            {
                var slot = MakeSlot(out var icon, out var label);
                if (big) slot.AddToClassList("item-slot--big");
                int index = i;
                slot.RegisterCallback<ClickEvent>(e => Clicked?.Invoke(index, e));
                container.Add(slot);
                _slots[i] = slot;
                _icons[i] = icon;
                _counts[i] = label;
            }
        }

        public static VisualElement MakeSlot(out VisualElement icon, out Label count)
        {
            var slot = new VisualElement();
            slot.AddToClassList("item-slot");
            icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("item-icon");
            slot.Add(icon);
            count = new Label { pickingMode = PickingMode.Ignore };
            count.AddToClassList("item-count");
            slot.Add(count);
            return slot;
        }

        public ushort ItemAt(int i) => _items[i];
        public int CountAt(int i) => _amounts[i];

        public void Set(int i, ushort item, int count)
        {
            if (count <= 0) item = ItemCatalog.None;
            if (_items[i] != item)
            {
                _items[i] = item;
                _icons[i].style.backgroundImage = item == ItemCatalog.None ? new StyleBackground() : new StyleBackground(GameArt.ItemSprite(item));
                _slots[i].tooltip = item == ItemCatalog.None ? "" : ItemCatalog.NameOf(item);
            }
            if (_amounts[i] != count)
            {
                _amounts[i] = count;
                _counts[i].text = count > 1 ? count.ToString() : "";
            }
        }

        public void Show(Inventory inv)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i < inv.SlotCount) Set(i, inv[i].Item, inv[i].Count);
                else Set(i, ItemCatalog.None, 0);
            }
        }
    }
}
