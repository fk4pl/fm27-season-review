using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonReview
{
    /// <summary>
    /// The season review inside the game: a UI Toolkit panel on our own GameObject, UIDocument
    /// and PanelSettings, drawn from a <see cref="PanelModel"/>. It only reads from the game
    /// (the list of UI documents and panel settings, and the font of one game label) and only
    /// creates and removes its own objects; no game element is changed and nothing is written
    /// to disk (every entry point runs with disk writes forbidden).
    ///
    /// The panel is an extra. Its first unexpected error disables it for the rest of the
    /// session, logs once and removes its own objects; the report writer is never involved.
    /// </summary>
    internal sealed class ReportPanel
    {
        private const int VkEscape = 0x1B;
        private const string OwnName = "SeasonReviewPanel";
        private const string ThemeSourceName = "HighlightingLayerSettings"; // the game's panel that uses Unity's default runtime theme
        private const int FallbackCharsPerLine = 120; // only for the positioned-text fallback

        private static readonly Color BoxBg = new Color(0.07f, 0.08f, 0.10f, 1f);
        private static readonly Color TitleBg = new Color(0.15f, 0.19f, 0.28f, 1f);
        private static readonly Color TileBg = new Color(0.12f, 0.14f, 0.19f, 1f);
        private static readonly Color TabBg = new Color(0.14f, 0.16f, 0.21f, 1f);
        private static readonly Color TabActiveBg = new Color(0.24f, 0.36f, 0.62f, 1f);
        private static readonly Color CloseBg = new Color(0.58f, 0.17f, 0.17f, 1f);
        private static readonly Color HeadRowBg = new Color(0.17f, 0.20f, 0.27f, 1f);
        private static readonly Color AltRowBg = new Color(0.10f, 0.115f, 0.145f, 1f);
        private static readonly Color HighlightBg = new Color(0.13f, 0.18f, 0.16f, 1f);
        private static readonly Color TextColor = new Color(0.93f, 0.93f, 0.93f, 1f);
        private static readonly Color DimColor = new Color(0.66f, 0.70f, 0.76f, 1f);
        private static readonly Color AccentColor = new Color(0.62f, 0.76f, 1f, 1f);
        private static readonly Color NoticeColor = new Color(1f, 0.80f, 0.45f, 1f);

        private readonly SeasonController _controller;
        private Hotkey _hotkey;
        private string _hotkeyParsedFrom;
        private bool _disabled, _open, _escWasDown;
        private int _generation;

        // Our own objects; all of them are removed on close.
        private GameObject _object;
        private PanelSettings _settings;
        private VisualElement _box, _body;
        private readonly List<Button> _tabButtons = new();
        private readonly List<Il2CppSystem.Action> _handlers = new(); // click handlers, kept alive while shown
        private PanelModel _model;
        private ScrollView[] _tabViews;

        // Found once per session.
        private bool _stylesProbed, _rows, _wrap, _right, _clip, _fontSearched, _haveFont, _loggedHost, _loggedLayout;
        private StyleEnum<Overflow> _hidden;
        private int _measureIn; // frames until the tab's layout is measured for the log (0 = not waiting)
        private ScrollView _measureView;
        private StyleEnum<FlexDirection> _rowDirection;
        private StyleEnum<WhiteSpace> _wrapNormal;
        private StyleEnum<TextAnchor> _alignRight;
        private FontDefinition _font;

        private ReportPanel(SeasonController controller) => _controller = controller;

        /// <summary>Null when the panel is switched off in the settings.</summary>
        public static ReportPanel Create(SeasonController controller)
        {
            try
            {
                if (!Plugin.PanelEnabled.Value) return null;
                return new ReportPanel(controller);
            }
            catch (Exception e)
            {
                Plugin.L.LogWarning("In-game panel unavailable: " + e.Message);
                return null;
            }
        }

        // ── Keys ───────────────────────────────────────────────────────────

        /// <summary>Call once per frame.</summary>
        public void Tick()
        {
            if (_disabled) return;
            try
            {
                ParseHotkey();
                if (_measureIn > 0 && --_measureIn == 0) LogLayout();
                if (_hotkey.PressedThisFrame())
                {
                    if (_open) Guarded("close", () => Close());
                    else Guarded("open", Open);
                    return;
                }
                if (!_open) return; // Esc is not read at all while the panel is closed

                var escDown = IsDown(VkEscape) && GameHasFocus();
                if (escDown && !_escWasDown) Guarded("close", () => Close());
                _escWasDown = escDown;
            }
            catch (Exception e) { Disable("keys", e); }
        }

        private void ParseHotkey()
        {
            var text = Plugin.PanelHotkeyText.Value;
            if (text == _hotkeyParsedFrom) return;
            _hotkeyParsedFrom = text;
            if (!Hotkey.TryParse(text, out _hotkey))
            {
                Plugin.L.LogWarning($"Invalid PanelHotkey '{text}', falling back to F10.");
                Hotkey.TryParse("F10", out _hotkey);
            }
            if (Hotkey.TryParse(Plugin.HotkeyText.Value, out var report) && report.ToString() == _hotkey.ToString())
                Plugin.L.LogWarning($"PanelHotkey and Hotkey are both {_hotkey}: the key will open the panel and write a report.");
            Plugin.L.LogInfo($"Panel hotkey: {_hotkey}");
        }

        // ── Open / close ───────────────────────────────────────────────────

        private void Open()
        {
            int generation = ++_generation;
            var strings = Plugin.LoadStrings();

            CreateHost();
            _open = true;
            _escWasDown = true; // an Esc held while opening must be released first
            Render(PanelModelBuilder.Message(strings, "panel.loading"));

            _controller.RequestPanelRead(model => Guarded("show", () =>
            {
                if (_open && generation == _generation) Render(model);
            }));
        }

        private void Close()
        {
            _open = false;
            _generation++;
            _controller.CancelPanelRead();
            Teardown();
        }

        /// <summary>Removes only what we created.</summary>
        private void Teardown()
        {
            try { if (_object != null) UnityEngine.Object.Destroy(_object); }
            catch (Exception e) { Plugin.L.LogWarning("Panel: could not remove its GameObject: " + e.Message); }
            try { if (_settings != null) UnityEngine.Object.Destroy(_settings); }
            catch (Exception e) { Plugin.L.LogWarning("Panel: could not remove its PanelSettings: " + e.Message); }
            _object = null;
            _settings = null;
            _box = null;
            _body = null;
            _model = null;
            _tabViews = null;
            _measureView = null;
            _measureIn = 0;
            _tabButtons.Clear();
            _handlers.Clear();
        }

        // ── Safety ─────────────────────────────────────────────────────────

        /// <summary>Every entry point of the panel: read-only on disk, and an error only ever disables the panel.</summary>
        private void Guarded(string what, Action action)
        {
            if (_disabled) return;
            try
            {
                using (DiskWrites.Forbid(PanelRead.Reason)) action();
            }
            catch (Exception e) { Disable(what, e); }
        }

        private void Disable(string what, Exception e)
        {
            if (_disabled) return;
            _disabled = true;
            _open = false;
            Plugin.L.LogWarning($"In-game panel disabled for this session after an error ({what}): {e.GetType().Name}: {e.Message}. Reports are not affected.");
            try { _controller.CancelPanelRead(); } catch (Exception) { }
            Teardown();
        }

        // ── Host: our own PanelSettings, GameObject and UIDocument ─────────

        private void CreateHost()
        {
            var gameSettings = Resources.FindObjectsOfTypeAll(Il2CppType.Of<PanelSettings>())
                .Select(o => o.TryCast<PanelSettings>())
                .Where(p => p != null && !p.name.StartsWith(OwnName, StringComparison.Ordinal)).ToList();
            var themeSource = gameSettings.FirstOrDefault(p => p.name == ThemeSourceName && p.themeStyleSheet != null)
                              ?? gameSettings.FirstOrDefault(p => p.themeStyleSheet != null);
            float highestOrder = gameSettings.Count > 0 ? gameSettings.Max(p => p.sortingOrder) : 0f;

            if (!_fontSearched) FindGameFont();
            if (!_stylesProbed) ProbeStyles();

            PanelSettings settings;
            float documentOrder = 0f;
            string how;
            try
            {
                var own = ScriptableObject.CreateInstance(Il2CppType.Of<PanelSettings>()).Cast<PanelSettings>();
                _settings = own;
                own.name = OwnName + "Settings";
                own.hideFlags = HideFlags.HideAndDontSave;
                if (themeSource != null)
                {
                    own.themeStyleSheet = themeSource.themeStyleSheet;
                    own.textSettings = themeSource.textSettings;
                }
                own.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                own.referenceResolution = new Vector2Int(1920, 1080);
                own.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                own.match = 0.5f;
                own.sortingOrder = highestOrder + 100f;
                settings = own;
                how = $"own panel settings above the game's (order {own.sortingOrder}), theme from '{(themeSource != null ? themeSource.name : "none")}'";
            }
            catch (Exception e)
            {
                // Known alternative, not an error: share the game's themed panel, drawn last.
                try { if (_settings != null) UnityEngine.Object.Destroy(_settings); } catch (Exception) { }
                _settings = null;
                settings = themeSource ?? throw new InvalidOperationException("no panel settings available: " + e.Message);
                documentOrder = 10000f;
                how = $"shared panel settings '{settings.name}' (own ones could not be created: {e.Message})";
            }

            _object = new GameObject(OwnName);
            _object.SetActive(false); // configure before UIDocument.OnEnable runs
            UnityEngine.Object.DontDestroyOnLoad(_object);
            var document = _object.AddComponent(Il2CppType.Of<UIDocument>()).Cast<UIDocument>();
            document.panelSettings = settings;
            document.sortingOrder = documentOrder;
            _object.SetActive(true);

            // Our document's full-screen root: lets clicks outside the box through to the game.
            var root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = new StyleFloat(1f);
            root.style.width = new StyleLength(Length.Percent(100f));
            root.style.height = new StyleLength(Length.Percent(100f));

            _box = new VisualElement { name = OwnName + "Box" };
            _box.pickingMode = PickingMode.Position; // the box itself takes the pointer: nothing reaches the game behind it
            var s = _box.style;
            s.width = new StyleLength(PanelLayout.Width);
            s.height = new StyleLength(PanelLayout.Height);
            s.maxWidth = new StyleLength(Length.Percent(PanelLayout.MaxWidthPercent));
            s.maxHeight = new StyleLength(Length.Percent(PanelLayout.MaxHeightPercent));
            s.flexShrink = new StyleFloat(0f);
            s.backgroundColor = new StyleColor(BoxBg);
            if (_clip) s.overflow = _hidden; // nothing is ever drawn outside the panel
            Pad(_box, PanelLayout.BoxPadding, 10f);

            if (!PanelLayout.TryParsePosition(Plugin.PanelPositionText.Value, out var position))
                Plugin.L.LogWarning($"Invalid PanelPosition '{Plugin.PanelPositionText.Value}', using Center.");
            var (left, top, right, bottom) = PanelLayout.Margins(position);
            s.marginLeft = Margin(left);
            s.marginTop = Margin(top);
            s.marginRight = Margin(right);
            s.marginBottom = Margin(bottom);
            root.Add(_box);

            if (!_loggedHost)
            {
                _loggedHost = true;
                Plugin.L.LogInfo($"Panel: {how}; position {position}; rows {(_rows ? "side by side" : "as positioned text (fallback)")}, " +
                                 $"wrapping {(_wrap ? "on" : "unavailable")}, clipping {(_clip ? "on" : "unavailable")}, right-aligned numbers {(_right ? "on" : "unavailable")}, " +
                                 $"game font {(_haveFont ? "found" : "not found (theme font)")}.");
            }
        }

        private static StyleLength Margin(float? gap) =>
            gap.HasValue ? new StyleLength(gap.Value) : new StyleLength(StyleKeyword.Auto);

        /// <summary>
        /// Style values of generic type exist per enum in the game build, which cannot be checked
        /// in advance. Each is tried once on an element that is never shown; without them the
        /// panel still works with a simpler layout.
        /// </summary>
        private void ProbeStyles()
        {
            _stylesProbed = true;
            var probe = new VisualElement();
            _rows = Try(() => { _rowDirection = new StyleEnum<FlexDirection>(FlexDirection.Row); probe.style.flexDirection = _rowDirection; });
            _wrap = Try(() => { _wrapNormal = new StyleEnum<WhiteSpace>(WhiteSpace.Normal); probe.style.whiteSpace = _wrapNormal; });
            _right = Try(() => { _alignRight = new StyleEnum<TextAnchor>(TextAnchor.UpperRight); probe.style.unityTextAlign = _alignRight; });
            _clip = Try(() => { _hidden = new StyleEnum<Overflow>(Overflow.Hidden); probe.style.overflow = _hidden; });
        }

        private static bool Try(Action action)
        {
            try { action(); return true; }
            catch (Exception) { return false; }
        }

        /// <summary>Remembers the font of one visible game label (read-only), so our text looks like the game's.</summary>
        private void FindGameFont()
        {
            _fontSearched = true;
            var queue = new Queue<VisualElement>();
            foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<UIDocument>()))
            {
                var document = o.TryCast<UIDocument>();
                if (document == null || !document.isActiveAndEnabled || document.gameObject.name.StartsWith(OwnName, StringComparison.Ordinal)) continue;
                var root = document.rootVisualElement;
                if (root != null) queue.Enqueue(root);
            }

            int visited = 0;
            while (queue.Count > 0 && visited < 5000)
            {
                var element = queue.Dequeue();
                visited++;
                var text = element.TryCast<TextElement>();
                if (text != null && !string.IsNullOrWhiteSpace(text.text))
                {
                    var definition = text.resolvedStyle.unityFontDefinition;
                    if (definition.fontAsset != null || definition.font != null)
                    {
                        _font = definition;
                        _haveFont = true;
                        return;
                    }
                }
                int count = element.childCount;
                for (int i = 0; i < count; i++) queue.Enqueue(element.ElementAt(i));
            }
            _fontSearched = false; // nothing visible yet (e.g. a loading screen): look again next time
        }

        // ── Drawing ────────────────────────────────────────────────────────

        private void Render(PanelModel model)
        {
            _model = model;
            _box.Clear();
            _tabButtons.Clear();
            _handlers.Clear();
            _tabViews = new ScrollView[model.Tabs.Count];

            // Title bar with the Close button.
            var bar = new VisualElement();
            bar.style.backgroundColor = new StyleColor(TitleBg);
            bar.style.flexShrink = new StyleFloat(0f);
            Pad(bar, 10f, 6f);
            if (_rows) bar.style.flexDirection = _rowDirection;
            var title = Text(PanelText.Bold(model.Title), 22f, TextColor, rich: true);
            title.style.flexGrow = new StyleFloat(1f);
            title.style.flexShrink = new StyleFloat(1f);
            bar.Add(title);
            var close = MakeButton(model.Close, CloseBg, () => Close());
            if (!_rows) close.style.width = new StyleLength(140f); // stacked under the title: do not stretch
            bar.Add(close);
            _box.Add(bar);

            if (!string.IsNullOrEmpty(model.Subtitle)) _box.Add(Spaced(Text(model.Subtitle, 15f, DimColor), 6f, 0f));
            if (!string.IsNullOrEmpty(model.Notice)) _box.Add(Spaced(Text(model.Notice, 15f, NoticeColor), 4f, 0f));

            if (model.Message != null)
            {
                _box.Add(Spaced(Text(model.Message, 18f, TextColor), 24f, 0f));
                return;
            }

            if (model.Tiles.Count > 0) _box.Add(Tiles(model));

            // Tab buttons.
            VisualElement tabBar;
            if (_rows)
            {
                tabBar = new VisualElement();
                tabBar.style.flexDirection = _rowDirection;
            }
            else tabBar = new ScrollView(ScrollViewMode.Horizontal); // lays its content out in a row without a generic style value
            tabBar.style.flexShrink = new StyleFloat(0f);
            tabBar.style.marginTop = new StyleLength(8f);
            tabBar.style.marginBottom = new StyleLength(6f);
            for (int i = 0; i < model.Tabs.Count; i++)
            {
                int index = i;
                var button = MakeButton(model.Tabs[i].Label, TabBg, () => SelectTab(index));
                button.style.marginRight = new StyleLength(4f);
                _tabButtons.Add(button);
                tabBar.Add(button);
            }
            _box.Add(tabBar);

            _body = new VisualElement();
            _body.style.flexGrow = new StyleFloat(1f);
            _body.style.flexShrink = new StyleFloat(1f);
            _body.style.flexBasis = new StyleLength(0f);
            _box.Add(_body);

            if (model.Tabs.Count > 0) SelectTab(0);
        }

        private void SelectTab(int index)
        {
            if (_model == null || _body == null || index < 0 || index >= _model.Tabs.Count) return;
            for (int i = 0; i < _tabButtons.Count; i++)
                _tabButtons[i].style.backgroundColor = new StyleColor(i == index ? TabActiveBg : TabBg);

            _tabViews[index] ??= TabView(_model.Tabs[index]); // built the first time the tab is opened
            _body.Clear();
            _body.Add(_tabViews[index]);
            if (!_loggedLayout)
            {
                _measureView = _tabViews[index];
                _measureIn = 5; // let the layout settle first
            }
        }

        /// <summary>
        /// Writes the measured widths of the open tab to the log, once per session: the proof
        /// that the content is as wide as the visible area and no wider. Only reads our own elements.
        /// </summary>
        private void LogLayout()
        {
            try
            {
                var view = _measureView;
                _measureView = null;
                if (view == null || _box == null) return;
                float viewport = view.contentViewport.layout.width, content = view.contentContainer.layout.width, widest = 0f;
                if (float.IsNaN(viewport) || viewport <= 0f) return; // not laid out yet: try again with the next tab
                var container = view.contentContainer;
                int count = container.childCount;
                for (int i = 0; i < count; i++) widest = Math.Max(widest, container.ElementAt(i).layout.xMax);
                _loggedLayout = true;
                Plugin.L.LogInfo($"Panel layout: panel {_box.layout.width:0.#} x {_box.layout.height:0.#}, tab area {viewport:0.#} wide, " +
                                 $"content {content:0.#}, widest row ends at {widest:0.#}" +
                                 (widest > viewport + 0.5f || content > viewport + 0.5f ? " (WIDER than the tab area)." : " (fits)."));
            }
            catch (Exception e)
            {
                _loggedLayout = true; // a measurement only: never a reason to disable the panel
                Plugin.L.LogInfo("Panel layout could not be measured: " + e.Message);
            }
        }

        private VisualElement Tiles(PanelModel model)
        {
            if (!_rows)
                return Spaced(Text(string.Join("   ·   ", model.Tiles.Select(t => t.Label + ": " + t.Value)), 16f, TextColor), 8f, 0f);

            var tiles = new VisualElement();
            tiles.style.flexDirection = _rowDirection;
            tiles.style.flexShrink = new StyleFloat(0f);
            tiles.style.marginTop = new StyleLength(8f);
            foreach (var (label, value) in model.Tiles)
            {
                var tile = new VisualElement();
                tile.style.backgroundColor = new StyleColor(TileBg);
                tile.style.marginRight = new StyleLength(8f);
                tile.style.minWidth = new StyleLength(120f);
                Pad(tile, 12f, 6f);
                tile.Add(Text(label, 13f, DimColor));
                tile.Add(Text(PanelText.Bold(value), 22f, TextColor, rich: true));
                tiles.Add(tile);
            }
            return tiles;
        }

        private ScrollView TabView(PanelTab tab)
        {
            // Up and down only. The content is exactly as wide as the visible area, so rows
            // (whose cells are shares of the row) and wrapped text can never be wider than the panel.
            var view = new ScrollView(ScrollViewMode.Vertical);
            view.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            view.style.flexGrow = new StyleFloat(1f);
            view.style.minWidth = new StyleLength(0f);
            var content = view.contentContainer.style;
            content.width = new StyleLength(Length.Percent(100f));
            content.maxWidth = new StyleLength(Length.Percent(100f));
            content.minWidth = new StyleLength(0f);
            view.Add(Spaced(Text(PanelText.Bold(tab.Title), 20f, AccentColor, rich: true), 4f, 6f));
            foreach (var row in tab.Rows) view.Add(Row(row));
            return view;
        }

        private VisualElement Row(PanelRow row)
        {
            switch (row.Kind)
            {
                case PanelRowKind.Heading:
                    return Spaced(Text(PanelText.Bold(row.Text), 17f, AccentColor, rich: true), 14f, 4f);
                case PanelRowKind.Text:
                    return Spaced(Text(row.Text, 15f, TextColor), 2f, 4f);
                case PanelRowKind.Note:
                    return Spaced(Text("<i>" + PanelText.Escape(row.Text) + "</i>", 15f, DimColor, rich: true), 2f, 4f);
                case PanelRowKind.Bullet:
                    return Spaced(Text("•  " + row.Text, 15f, TextColor), 1f, 1f);
                case PanelRowKind.Highlight:
                    var box = new VisualElement();
                    box.style.backgroundColor = new StyleColor(HighlightBg);
                    box.style.marginTop = new StyleLength(6f);
                    box.style.marginBottom = new StyleLength(6f);
                    box.style.flexShrink = new StyleFloat(0f);
                    Pad(box, 12f, 8f);
                    box.Add(Text(row.Cells[0], 13f, DimColor));
                    box.Add(Text(PanelText.Bold(row.Cells[1]), 20f, TextColor, rich: true));
                    if (row.Cells[2].Length > 0) box.Add(Text(row.Cells[2], 15f, TextColor));
                    return box;
                default:
                    return Cells(row);
            }
        }

        /// <summary>A key/value pair or a table line: cells side by side, or one positioned line as the fallback.</summary>
        private VisualElement Cells(PanelRow row)
        {
            bool header = row.Kind == PanelRowKind.TableHeader;
            var background = header ? HeadRowBg : row.Alternate ? AltRowBg : (Color?)null;
            float size = PanelLayout.TextSize;

            if (!_rows)
            {
                var line = Text(PanelText.PositionedRow(row.Cells, row.Widths, FallbackCharsPerLine, header), size, TextColor, rich: true, wrap: false);
                if (background.HasValue) line.style.backgroundColor = new StyleColor(background.Value);
                if (_clip) line.style.overflow = _hidden;
                Pad(line, PanelLayout.CellPadding, 3f);
                return line;
            }

            var element = new VisualElement();
            element.style.flexDirection = _rowDirection;
            element.style.flexShrink = new StyleFloat(0f); // its height; the width comes from the tab
            element.style.minWidth = new StyleLength(0f);
            if (_clip) element.style.overflow = _hidden;
            if (background.HasValue) element.style.backgroundColor = new StyleColor(background.Value);
            if (row.Kind == PanelRowKind.KeyValue) element.style.maxWidth = new StyleLength(Length.Percent(70f));

            for (int i = 0; i < row.Cells.Length; i++)
            {
                var cell = header
                    ? Text(PanelText.Bold(row.Cells[i]), size, TextColor, rich: true)
                    : Text(row.Cells[i], size, row.Kind == PanelRowKind.KeyValue && i == 0 ? DimColor : TextColor);
                // A share of the row (the shares never add up to more than 100, see PanelModelBuilder),
                // padding inside it. The cell never grows with its text: long text wraps or is cut,
                // and if the cells were ever too wide together they shrink instead of widening the row.
                cell.style.width = new StyleLength(Length.Percent(row.Widths[i]));
                cell.style.minWidth = new StyleLength(0f);
                cell.style.flexGrow = new StyleFloat(0f);
                cell.style.flexShrink = new StyleFloat(1f);
                if (_clip) cell.style.overflow = _hidden;
                Pad(cell, PanelLayout.CellPadding, 3f);
                if (_right && row.Numeric != null && row.Numeric[i]) cell.style.unityTextAlign = _alignRight;
                element.Add(cell);
            }
            return element;
        }

        // ── Small builders ─────────────────────────────────────────────────

        /// <summary>A label with inline styles only. Plain text is shown exactly as given; rich text must be escaped by the caller.</summary>
        private Label Text(string text, float size, Color color, bool rich = false, bool wrap = true)
        {
            var label = new Label(text ?? "");
            label.enableRichText = rich;
            label.style.color = new StyleColor(color);
            label.style.fontSize = new StyleLength(size);
            if (_haveFont) label.style.unityFontDefinition = new StyleFontDefinition(_font);
            if (wrap && _wrap) label.style.whiteSpace = _wrapNormal;
            return label;
        }

        private Button MakeButton(string text, Color background, Action onClick)
        {
            var handler = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(() => Guarded("click", onClick)));
            _handlers.Add(handler);

            var button = new Button();
            button.text = text ?? "";
            button.style.backgroundColor = new StyleColor(background);
            button.style.color = new StyleColor(TextColor);
            button.style.fontSize = new StyleLength(15f);
            button.style.flexShrink = new StyleFloat(0f);
            if (_haveFont) button.style.unityFontDefinition = new StyleFontDefinition(_font);
            Pad(button, 14f, 5f);
            button.add_clicked(handler);
            return button;
        }

        private static T Spaced<T>(T element, float top, float bottom) where T : VisualElement
        {
            element.style.marginTop = new StyleLength(top);
            element.style.marginBottom = new StyleLength(bottom);
            element.style.flexShrink = new StyleFloat(0f);
            return element;
        }

        private static void Pad(VisualElement element, float horizontal, float vertical)
        {
            element.style.paddingLeft = new StyleLength(horizontal);
            element.style.paddingRight = new StyleLength(horizontal);
            element.style.paddingTop = new StyleLength(vertical);
            element.style.paddingBottom = new StyleLength(vertical);
        }

        // ── Win32 keys (same approach as Hotkey) ───────────────────────────

        private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        private static bool GameHasFocus()
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero) return false;
            GetWindowThreadProcessId(window, out var pid);
            return pid == (uint)Environment.ProcessId;
        }

        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}
