using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.CoreUI;
using Timberborn.Goods;
using UnityEngine;
using UnityEngine.UIElements;

namespace MixedStorage
{
    internal sealed class StorageView
    {
        private static WeakReference<StorageView> _activeView;
        private static Dictionary<string, int> _copiedAllocation;
        internal static bool IsEditingText
        {
            get
            {
                if (_activeView == null || !_activeView.TryGetTarget(out var view) || view._state == null || view._panel.panel == null) return false;
                var focused = view._panel.focusController?.focusedElement as VisualElement;
                bool textField = false;
                for (var element = focused; element != null; element = element.parent)
                {
                    if (element is TextField) textField = true;
                    if (element == view._panel) return textField;
                }
                return false;
            }
        }
        private sealed class Row
        {
            public string Id;
            public string Name;
            public VisualElement Root;
            public TextField Percent;
            public Label Limit;
            public Label Stock;
            public VisualElement SummaryCard;
            public Label SummaryCount;
            public Label SummaryShare;
            public Label SummaryNote;
            public VisualElement SummaryFill;
            public bool Valid;
        }

        private static readonly Color NativeText = new Color(.8f, .8f, .8f);
        private static readonly Color Muted = new Color(.7f, .74f, .7f);
        private static readonly Color Divider = new Color(.8f, .75f, .55f, .3f);
        private static readonly Color Error = new Color(1f, .52f, .43f);
        private static readonly Color Green = new Color(.57f, .89f, .66f);
        private readonly IGoodService _goods;
        private readonly VisualElementLoader _visualElementLoader;
        private readonly VisualTreeAsset _inputTemplate;
        private readonly VisualElement _vanilla;
        private readonly VisualElement _panel;
        private readonly ScrollView _body;
        private readonly Label _summary;
        private readonly ScrollView _contentsSummary;
        private readonly Label _contentsEmpty;
        private readonly Label _total;
        private readonly Label _message;
        private readonly Label _count;
        private readonly Label _rounding;
        private readonly TextField _search;
        private readonly Toggle _allocatedOnly;
        private readonly ScrollView _scroll;
        private readonly Button _apply;
        private readonly Button _copy;
        private readonly Button _paste;
        private readonly List<Row> _rows = new List<Row>();
        private StorageState _state;
        private Dictionary<string, int> _draft;
        private Dictionary<string, int> _preview;
        private int _revision;
        private int _messageRevision;
        private float _nextRefresh;
        private VisualElement _entityPanel;
        private StyleLength _originalPanelWidth;
        public VisualElement Root { get; }

        public StorageView(IGoodService goods, VisualElementLoader visualElementLoader, VisualElement vanilla)
        {
            _goods = goods;
            _visualElementLoader = visualElementLoader;
            _inputTemplate = visualElementLoader.LoadVisualTreeAsset("Core/InputBox");
            _vanilla = vanilla;
            Root = new VisualElement { name = "MixedStorageRoot" };
            // These are the game's own style sheets and nine-slice backgrounds.
            // The vanilla fragment is a sibling, so expose its styles to our controls too.
            for (int i = 0; i < vanilla.styleSheets.count; i++)
                Root.styleSheets.Add(vanilla.styleSheets[i]);
            Root.Add(vanilla);
            _panel = new NineSliceVisualElement { name = "MixedStoragePanel" };
            _panel.AddToClassList("entity-sub-panel");
            _panel.AddToClassList("bg-sub-box--green");
            _panel.style.display = DisplayStyle.None;
            _panel.style.color = NativeText;
            Root.Add(_panel);
            var title = Text("STORAGE ALLOCATION", 16);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _panel.Add(title);
            _summary = Text("", 16);
            _summary.style.unityFontStyleAndWeight = FontStyle.Bold;
            _summary.style.whiteSpace = WhiteSpace.Normal;
            _panel.Add(_summary);
            _contentsSummary = new ScrollView(ScrollViewMode.Vertical);
            _contentsSummary.style.flexShrink = 0;
            _contentsSummary.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _contentsSummary.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            CompactScroll(_contentsSummary);
            _contentsSummary.style.marginTop = 4;
            _contentsSummary.style.marginBottom = 4;
            _contentsSummary.tooltip = "Applied allocation percentage and current stored quantity / item limit. Includes incoming and excess goods, regardless of search or filters.";
            _panel.Add(_contentsSummary);
            _contentsEmpty = Text("No goods allocated or stored.", 15);

            _search = CreateInput("MixedStorageSearch", "Search the goods allowed in this storage building.");
            _search.RegisterValueChangedCallback(_ => Filter());
            var searchRow = Horizontal();
            searchRow.style.marginTop = 6;
            var searchLabel = Text("Search", 13);
            searchLabel.style.flexGrow = 1;
            searchLabel.style.flexBasis = 0;
            searchLabel.style.marginRight = 8;
            searchRow.Add(searchLabel);
            _search.style.flexGrow = 1;
            _search.style.flexBasis = 0;
            _search.style.minWidth = 0;
            searchRow.Add(_search);
            var clearSearch = ActionButton("×", () => { _search.value = ""; _search.Focus(); });
            clearSearch.tooltip = "Clear search";
            clearSearch.style.width = 24;
            clearSearch.style.minHeight = 24;
            clearSearch.style.paddingLeft = clearSearch.style.paddingRight = 0;
            clearSearch.style.marginLeft = 4;
            clearSearch.style.marginRight = 0;
            searchRow.Add(clearSearch);
            _panel.Add(searchRow);
            _allocatedOnly = new Toggle { text = "Allocated goods only" };
            _allocatedOnly.AddToClassList("game-toggle");
            _allocatedOnly.AddToClassList("entity-panel__toggle");
            _allocatedOnly.style.fontSize = 13;
            _allocatedOnly.RegisterValueChangedCallback(_ => Filter());
            _panel.Add(_allocatedOnly);
            _count = Text("", 11);
            _count.style.color = Muted;
            _panel.Add(_count);
            var header = Horizontal();
            var goodsHeader = Text("GOOD / STOCK", 11);
            goodsHeader.style.flexGrow = 1;
            goodsHeader.style.flexBasis = 0;
            goodsHeader.style.minWidth = 0;
            header.Add(goodsHeader);
            var percentHeader = Text("% / RESET / MAX", 11); percentHeader.style.width = 122; percentHeader.style.flexShrink = 0; header.Add(percentHeader);
            var limitHeader = Text("LIMIT", 11); limitHeader.style.width = 40; limitHeader.style.marginLeft = 6; limitHeader.style.flexShrink = 0; limitHeader.style.unityTextAlign = TextAnchor.MiddleRight; header.Add(limitHeader);
            header.style.marginTop = 5;
            _panel.Add(header);
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            CompactScroll(_scroll);
            _scroll.style.marginTop = 3;
            _panel.Add(_scroll);

            _total = Text("", 15);
            _total.style.unityFontStyleAndWeight = FontStyle.Bold;
            _total.style.marginTop = 7;
            _panel.Add(_total);
            _rounding = Text("", 11);
            _rounding.style.whiteSpace = WhiteSpace.Normal;
            _rounding.style.color = Muted;
            _panel.Add(_rounding);
            _message = Text("", 12);
            _message.style.whiteSpace = WhiteSpace.Normal;
            _panel.Add(_message);
            var clipboardActions = Horizontal();
            clipboardActions.style.marginTop = 6;
            _copy = ActionButton("Copy allocations", CopyAllocation);
            _copy.tooltip = "Copy this valid 100% draft. Stock, hauling mode and hauler priority are not copied.";
            _paste = ActionButton("Paste allocations", PasteAllocation);
            _paste.tooltip = "Paste copied percentages into this draft, then Apply. All allocated goods must be accepted here.";
            clipboardActions.Add(_copy);
            clipboardActions.Add(_paste);
            _panel.Insert(_panel.IndexOf(_total), clipboardActions);
            var actions = Horizontal();
            actions.style.marginTop = 6;
            actions.Add(ActionButton("Clear all", ClearDraft));
            actions.Add(ActionButton("Revert", () => { LoadDraft(); _message.text = "Draft reverted."; _message.style.color = Muted; }));
            _apply = ActionButton("Apply 100%", Apply);
            _apply.style.flexGrow = 1;
            _apply.style.marginRight = 0;
            actions.Add(_apply);
            _panel.Add(actions);
            // Keep clipboard controls, the total and Apply outside the scrolling content. The game window
            // includes other fragments above us, so budget from this fragment's actual top.
            _body = new ScrollView(ScrollViewMode.Vertical);
            _body.AddToClassList("scroll--green-decorated");
            new ScrollBarInitializationService().InitializeVisualElement(_body);
            _body.style.minHeight = 0;
            _body.style.flexShrink = 1;
            _body.style.flexGrow = 1;
            _body.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _body.verticalScrollerVisibility = ScrollerVisibility.Auto;
            CompactScroll(_body);
            _body.verticalScroller.style.width = 20;
            _body.verticalScroller.style.minWidth = 20;
            _body.verticalScroller.style.marginLeft = 4;
            foreach (var child in _panel.Children().ToArray())
                if (child != title && child != clipboardActions && child != _total && child != actions) _body.Add(child);
            _panel.Insert(1, _body);
            title.style.flexShrink = clipboardActions.style.flexShrink = _total.style.flexShrink = actions.style.flexShrink = 0;
            _total.style.whiteSpace = WhiteSpace.Normal;
            _total.style.borderTopWidth = 1;
            _total.style.borderTopColor = Divider;
            _total.style.paddingTop = 5;
            _panel.style.minHeight = 0;
            Root.style.minWidth = 0;
            Root.style.alignSelf = Align.Stretch;
            _panel.RegisterCallback<GeometryChangedEvent>(_ => FitPanel());
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == Root) RestorePanelWidth(); });
            // Keep typing and scrolling within the editor instead of bubbling to shortcuts/panel scrolling.
            _panel.RegisterCallback<KeyDownEvent>(evt => { if (evt.target is TextElement || evt.target is TextField) evt.StopPropagation(); });
        }

        public void Show(StorageState state)
        {
            _state = state;
            _activeView = state == null ? null : new WeakReference<StorageView>(this);
            _panel.style.display = state == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (state == null) { RestorePanelWidth(); return; }
            _vanilla.style.display = DisplayStyle.None;
            _messageRevision = state.MessageRevision;
            _search.SetValueWithoutNotify("");
            _allocatedOnly.SetValueWithoutNotify(false);
            LoadDraft();
            _message.text = state.Active ? "Edit percentages, then Apply. 0% disables a good." : "Set percentages totaling 100% to activate mixed storage.";
            _message.style.color = Muted;
            RefreshStock();
        }

        public void Clear()
        {
            RestorePanelWidth();
            _state = null;
            _panel.style.display = DisplayStyle.None;
            _rows.Clear();
            _scroll.Clear();
            _contentsSummary.Clear();
        }

        private void LoadDraft()
        {
            _draft = _state.Draft();
            _revision = _state.Revision;
            _rows.Clear();
            _scroll.Clear();
            _contentsSummary.Clear();
            _contentsSummary.Add(_contentsEmpty);
            foreach (var id in _draft.Keys.OrderBy(DisplayName, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x, StringComparer.Ordinal))
            {
                var row = new Row { Id = id, Name = DisplayName(id), Root = Horizontal(), Valid = true };
                CreateSummaryCard(row);
                row.Root.style.paddingTop = row.Root.style.paddingBottom = 1;
                row.Root.style.borderBottomWidth = 1;
                row.Root.style.borderBottomColor = Divider;
                var icon = new Image();
                icon.style.width = icon.style.height = 20;
                icon.style.flexShrink = 0;
                icon.style.marginRight = 5;
                if (_goods.HasGood(id)) icon.sprite = _goods.GetGood(id).IconSmall.Value;
                row.Root.Add(icon);
                var details = new VisualElement();
                details.style.flexGrow = 1;
                details.style.flexBasis = 0;
                details.style.flexShrink = 1;
                details.style.minWidth = 0;
                var name = Text(row.Name, 13);
                name.style.whiteSpace = WhiteSpace.Normal;
                details.Add(name);
                row.Stock = Text("", 11);
                row.Stock.style.color = Muted;
                details.Add(row.Stock);
                row.Root.Add(details);
                row.Percent = CreateInput("Percent_" + id, "0–100%, up to two decimal places. Changes are drafts until Apply.");
                row.Percent.style.width = 58;
                row.Percent.style.minWidth = 58;
                row.Percent.style.marginLeft = 0;
                row.Percent.style.marginRight = 4;
                row.Percent.style.flexShrink = 0;
                row.Percent.style.fontSize = 13;
                row.Percent.style.marginTop = row.Percent.style.marginBottom = 0;
                row.Percent.style.height = row.Percent.style.minHeight = 22;
                row.Percent.SetValueWithoutNotify(AllocationPlan.Format(_draft[id]));
                row.Percent.RegisterValueChangedCallback(evt =>
                {
                    row.Valid = AllocationPlan.TryParsePercent(evt.newValue, out var units);
                    _draft[row.Id] = row.Valid ? units : 0;
                    SetInputValidity(row.Percent, row.Valid);
                    _message.text = "Unapplied changes";
                    _message.style.color = Muted;
                    Validate();
                });
                row.Root.Add(row.Percent);
                var reset = ActionButton("×", () =>
                {
                    row.Percent.value = "0";
                    row.Percent.Focus();
                    row.Percent.SelectAll();
                });
                reset.tooltip = "Reset " + row.Name + " to 0% (draft only)";
                reset.style.width = 22;
                reset.style.minWidth = 22;
                reset.style.minHeight = 22;
                reset.style.flexShrink = 0;
                reset.style.marginTop = reset.style.marginBottom = 0;
                reset.style.marginLeft = 0;
                reset.style.marginRight = 4;
                reset.style.paddingLeft = reset.style.paddingRight = 0;
                row.Root.Add(reset);
                var max = ActionButton("Max", () => SetDraft(
                    AllocationPlan.Max(_draft.Keys, row.Id), row.Name + " set to 100%. Press Apply."));
                max.tooltip = "Set " + row.Name + " to 100% and all other goods to 0% (draft only)";
                max.style.width = 34;
                max.style.minWidth = 34;
                max.style.minHeight = 22;
                max.style.fontSize = 11;
                max.style.flexShrink = 0;
                max.style.marginTop = max.style.marginBottom = 0;
                max.style.marginLeft = max.style.marginRight = 0;
                max.style.paddingLeft = max.style.paddingRight = 0;
                row.Root.Add(max);
                row.Limit = Text("", 13);
                row.Limit.style.width = 40;
                row.Limit.style.marginLeft = 6;
                row.Limit.style.flexShrink = 0;
                row.Limit.style.unityTextAlign = TextAnchor.MiddleRight;
                row.Root.Add(row.Limit);
                _rows.Add(row);
                _scroll.Add(row.Root);
            }
            Validate();
            Filter();
            RefreshStock();
        }

        private string DisplayName(string id) => _goods.HasGood(id) ? _goods.GetGood(id).PluralDisplayName.Value : id + " (unavailable)";

        private void Validate()
        {
            bool validFields = _rows.All(x => x.Valid);
            long total = _draft.Values.Sum(x => (long)x);
            bool valid = validFields && AllocationPlan.IsValid(_draft) && _draft.All(x => x.Value == 0 || _state.Inventory.Takes(x.Key));
            _apply.SetEnabled(valid && !_state.Pending);
            _copy.SetEnabled(valid);
            _paste.SetEnabled(_copiedAllocation != null && !_state.Pending);
            _total.text = !validFields ? "Enter valid percentages (0–100, 2 decimals)" :
                total == AllocationPlan.Total ? "100% / 100% allocated" :
                (total / 100m).ToString("0.##") + "% / 100% — " + (Math.Abs(total - AllocationPlan.Total) / 100m).ToString("0.##") + (total < AllocationPlan.Total ? "% remaining" : "% over");
            _total.style.color = valid ? Green : Error;
            _preview = valid ? AllocationPlan.Capacities(_draft, _state.Inventory.Capacity) : null;
            foreach (var row in _rows) row.Limit.text = _preview != null ? _preview[row.Id].ToString() : "—";
            int zeroSlots = _preview == null ? 0 : _draft.Count(x => x.Value > 0 && _preview[x.Key] == 0);
            _rounding.text = zeroSlots > 0 ? zeroSlots + " allocated good(s) round to 0 items. Increase their shares or use larger storage." :
                "Limits round to whole items; leftover slots go to the largest fractions. All slots are allocated.";
            _rounding.style.color = zeroSlots > 0 ? Error : Muted;
        }

        private void Filter()
        {
            if (_draft == null) return;
            var search = _search.value?.Trim() ?? "";
            int visible = 0;
            foreach (var row in _rows)
            {
                bool show = (!_allocatedOnly.value || _draft[row.Id] > 0) &&
                            (search.Length == 0 || row.Name.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0 || row.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
                row.Root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show) visible++;
            }
            _count.text = visible + " of " + _rows.Count + " goods shown · Total includes hidden rows";
        }

        private void ClearDraft()
        {
            foreach (var row in _rows)
            {
                _draft[row.Id] = 0;
                row.Valid = true;
                row.Percent.SetValueWithoutNotify("0");
                SetInputValidity(row.Percent, true);
            }
            _allocatedOnly.SetValueWithoutNotify(false);
            _message.text = "Draft cleared. Existing allocations remain active until Apply.";
            _message.style.color = Muted;
            Validate(); Filter();
        }

        private void SetDraft(Dictionary<string, int> draft, string message)
        {
            // Keep every row, including filtered-out goods, consistent with the new draft.
            foreach (var row in _rows)
            {
                _draft[row.Id] = draft.TryGetValue(row.Id, out var value) ? value : 0;
                row.Valid = true;
                row.Percent.SetValueWithoutNotify(AllocationPlan.Format(_draft[row.Id]));
                SetInputValidity(row.Percent, true);
            }
            _message.text = message;
            _message.style.color = Muted;
            Validate();
            Filter();
        }

        private void CopyAllocation()
        {
            if (_rows.Any(x => !x.Valid) || !AllocationPlan.IsValid(_draft)) return;
            _copiedAllocation = new Dictionary<string, int>(_draft, StringComparer.Ordinal);
            _message.text = "Allocations copied. Select another storage building and Paste.";
            _message.style.color = Green;
            Validate();
        }

        private void PasteAllocation()
        {
            if (_state.Pending) return;
            if (!AllocationPlan.TryPaste(_copiedAllocation, _draft.Keys.Where(x => _state.Inventory.Takes(x)), out var draft))
            {
                _message.text = "Cannot paste: this building does not accept all copied goods. Draft unchanged.";
                _message.style.color = Error;
                return;
            }
            SetDraft(draft, "Allocations pasted. Limits use this building's capacity. Press Apply.");
        }

        private void Apply()
        {
            if (_rows.Any(x => !x.Valid)) return;
            var result = AllocationCommands.Submit(_state, AllocationPlan.Serialize(_draft));
            if (result == SubmissionResult.Queued)
            {
                _state.Pending = true;
                _message.text = "Queued for multiplayer. Applies on the next simulation tick.";
                _message.style.color = Muted;
                _apply.SetEnabled(false);
            }
            else ShowResult();
            RefreshStock();
        }

        public void Refresh()
        {
            if (_state == null) return;
            FitPanel();
            _vanilla.style.display = DisplayStyle.None;
            if (_revision != _state.Revision) LoadDraft();
            if (_messageRevision != _state.MessageRevision) { ShowResult(); Validate(); }
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + .4f;
            RefreshStock();
        }

        private void FitPanel()
        {
            if (_state == null) return;
            var viewport = _panel.panel?.visualTree;
            if (viewport == null) return;
            if (_entityPanel == null)
            {
                // Widen the shared window, not just our fragment. Native headers,
                // descriptions and hauling controls stretch with it automatically.
                for (var parent = Root.parent; parent != null; parent = parent.parent)
                {
                    if (parent.name != "EntityPanel" || !parent.ClassListContains("entity-panel")) continue;
                    _entityPanel = parent;
                    _originalPanelWidth = parent.style.width;
                    break;
                }
            }
            float width = Mathf.Min(440, viewport.worldBound.width - 24);
            if (_entityPanel != null && width > 0 && Mathf.Abs(_entityPanel.resolvedStyle.width - width) > 1)
                _entityPanel.style.width = width;
            float bottom = viewport.worldBound.yMax;
            float top = _panel.worldBound.yMin;
            if (float.IsNaN(bottom) || float.IsNaN(top) || bottom <= 0) return;
            float height = Mathf.Clamp(bottom - Mathf.Max(0, top) - 16 - SpaceBelow(), 80, 520);
            if (Mathf.Abs(_panel.resolvedStyle.height - height) > 1)
                _panel.style.height = height;
        }

        // The game stacks its own fragments under this one in the same column, such as the construction
        // site's materials. Reserve their height so they stay on screen instead of being pushed past its bottom.
        private float SpaceBelow()
        {
            if (_entityPanel == null) return 0;
            float below = 0;
            for (var node = _panel; node.parent != null && node.parent != _entityPanel; node = node.parent)
            {
                var parent = node.parent;
                float edge = node.layout.yMax;
                if (float.IsNaN(edge) || parent.resolvedStyle.flexDirection != FlexDirection.Column) continue;
                float last = edge;
                for (int i = parent.IndexOf(node) + 1; i < parent.childCount; i++)
                {
                    var sibling = parent[i];
                    var style = sibling.resolvedStyle;
                    if (style.display == DisplayStyle.None || style.position == Position.Absolute || float.IsNaN(sibling.layout.yMax)) continue;
                    last = Mathf.Max(last, sibling.layout.yMax + style.marginBottom);
                }
                below += last - edge;
            }
            return below;
        }

        private void RestorePanelWidth()
        {
            if (_entityPanel == null) return;
            _entityPanel.style.width = _originalPanelWidth;
            _entityPanel = null;
        }

        private void ShowResult()
        {
            _messageRevision = _state.MessageRevision;
            _message.text = _state.LastMessage;
            _message.style.color = _state.LastSuccess ? Green : Error;
        }

        private void RefreshStock()
        {
            var inventory = _state.Inventory;
            _summary.text = inventory.TotalAmountInStock + " / " + inventory.Capacity + " items · " +
                (_state.Active ? _state.Shares.Count + (_state.Shares.Count == 1 ? " good allocated" : " goods allocated") : "Single-good settings active");
            int visibleContents = 0;
            foreach (var row in _rows)
            {
                int stock = inventory.AmountInStock(row.Id), incoming = inventory.ReservedCapacity(row.Id);
                int liveLimit = _state.Active ? _state.Limit(row.Id) : inventory.LimitedAmount(row.Id);
                int share = 0;
                if (_state.Active) _state.Shares.TryGetValue(row.Id, out share);
                else if (_state.Allower.HasAllowedGood && _state.Allower.AllowedGood == row.Id) share = AllocationPlan.Total;
                bool visible = share > 0 || stock > 0 || incoming > 0;
                row.SummaryCard.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (visible) visibleContents++;
                row.SummaryCount.text = stock + " / " + liveLimit;
                row.SummaryCount.style.color = stock > liveLimit ? Error : NativeText;
                row.SummaryShare.text = AllocationPlan.Format(share) + "% allocated";
                row.SummaryNote.text = (incoming > 0 ? "+" + incoming + " incoming" : "") +
                    (stock > liveLimit ? (incoming > 0 ? " · " : "") + (stock - liveLimit) + " excess" : "");
                row.SummaryNote.style.display = incoming > 0 || stock > liveLimit ? DisplayStyle.Flex : DisplayStyle.None;
                row.SummaryNote.style.color = stock > liveLimit ? Error : Muted;
                row.SummaryFill.style.width = Length.Percent(liveLimit > 0 ? Mathf.Clamp01((float)stock / liveLimit) * 100 : stock > 0 ? 100 : 0);
                row.SummaryFill.style.unityBackgroundImageTintColor = stock > liveLimit ? Error : Color.white;
                row.Stock.text = stock + " stored" + (incoming > 0 ? " + " + incoming + " incoming" : "") +
                    (stock > liveLimit ? " · excess" : "");
                row.Stock.style.color = stock > liveLimit ? Error : Muted;
            }
            _contentsEmpty.style.display = visibleContents == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void CreateSummaryCard(Row row)
        {
            var card = new NineSliceVisualElement();
            card.AddToClassList("bg-sub-box--blue");
            card.style.marginBottom = 4;
            card.style.paddingLeft = card.style.paddingRight = 7;
            card.style.paddingTop = card.style.paddingBottom = 5;
            card.style.flexShrink = 0;
            card.tooltip = row.Name + ": stored / limit. Bar shows stock as a fraction of the applied limit.";
            var line = Horizontal();
            var icon = new Image();
            icon.style.width = icon.style.height = 30;
            icon.style.flexShrink = 0;
            icon.style.marginRight = 8;
            if (_goods.HasGood(row.Id)) icon.sprite = _goods.GetGood(row.Id).IconSmall.Value;
            line.Add(icon);
            var details = new VisualElement();
            details.style.flexGrow = 1;
            details.style.flexShrink = 1;
            details.style.minWidth = 0;
            var name = Text(row.Name, 16);
            name.style.whiteSpace = WhiteSpace.Normal;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            details.Add(name);
            row.SummaryShare = Text("", 14);
            row.SummaryShare.style.color = Muted;
            details.Add(row.SummaryShare);
            line.Add(details);
            var counts = new VisualElement();
            counts.style.flexShrink = 0;
            counts.style.marginLeft = 8;
            row.SummaryCount = Text("", 19);
            row.SummaryCount.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.SummaryCount.style.unityTextAlign = TextAnchor.MiddleRight;
            counts.Add(row.SummaryCount);
            var legend = Text("stored / limit", 12);
            legend.style.color = Muted;
            legend.style.unityTextAlign = TextAnchor.MiddleRight;
            counts.Add(legend);
            line.Add(counts);
            card.Add(line);
            row.SummaryNote = Text("", 13);
            row.SummaryNote.style.whiteSpace = WhiteSpace.Normal;
            card.Add(row.SummaryNote);
            var track = new VisualElement();
            track.style.height = 4;
            track.style.marginTop = 4;
            track.style.backgroundImage = new StyleBackground(Resources.Load<Sprite>("UI/Images/Backgrounds/bg-pixel-1"));
            row.SummaryFill = new VisualElement();
            row.SummaryFill.style.height = 4;
            row.SummaryFill.style.backgroundImage = new StyleBackground(Resources.Load<Sprite>("UI/Images/Backgrounds/bg-pixel-4"));
            track.Add(row.SummaryFill);
            card.Add(track);
            row.SummaryCard = card;
            _contentsSummary.Add(card);
        }

        private static Label Text(string value, int size)
        {
            var label = new Label(value);
            label.AddToClassList("entity-panel__text");
            label.style.fontSize = size;
            label.style.marginLeft = label.style.marginRight = 0;
            label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingTop = label.style.paddingBottom = 0;
            return label;
        }
        private static void CompactScroll(ScrollView view)
        {
            view.style.minWidth = 0;
            view.contentViewport.style.minWidth = 0;
            view.contentViewport.style.marginLeft = view.contentViewport.style.marginRight = 0;
            view.contentContainer.style.minWidth = 0;
            view.contentContainer.style.marginLeft = view.contentContainer.style.marginRight = 0;
            view.contentContainer.style.paddingLeft = view.contentContainer.style.paddingRight = 0;
        }
        private static VisualElement Horizontal()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            return row;
        }
        private Button ActionButton(string text, Action action)
        {
            var button = (Button)_visualElementLoader.LoadVisualElement("Game/EntityPanel/DebugButton");
            button.name = "";
            button.RemoveFromClassList("debug-fragment__button");
            button.text = text;
            button.clicked += action;
            button.style.fontSize = 13;
            button.style.whiteSpace = WhiteSpace.NoWrap;
            button.style.minHeight = 28;
            button.style.marginRight = 4;
            button.style.paddingLeft = button.style.paddingRight = 6;
            return button;
        }

        private TextField CreateInput(string name, string tooltip)
        {
            // NineSliceTextField is internal; obtain the native control from its template.
            // Clone without initializing the unused dialog buttons and localization.
            var field = _inputTemplate.CloneTree().Q<TextField>("Input");
            field.RemoveFromHierarchy();
            field.RemoveFromClassList("box__input");
            field.name = name;
            field.tooltip = tooltip;
            field.pickingMode = PickingMode.Position;
            field.style.fontSize = 13;
            field.style.height = field.style.minHeight = 24;
            var input = field.Q<VisualElement>(className: "unity-text-field__input");
            if (input != null) input.style.unityTextAlign = TextAnchor.MiddleLeft;
            return field;
        }

        private static void SetInputValidity(TextField field, bool valid)
        {
            // Tint the input above the native frame, keeping invalid drafts visible.
            var input = field.Q<VisualElement>(className: "unity-text-field__input");
            if (input != null)
                input.style.backgroundColor = valid ? new StyleColor(StyleKeyword.Null) : new StyleColor(new Color(.5f, .14f, .1f));
        }
    }
}
