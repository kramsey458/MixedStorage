using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Timberborn.Goods;
using Timberborn.StockpileVisualization;
using UnityEngine;

namespace MixedStorage
{
    // Sample the native visualizers without creating components or consuming simulation RNG.
    // Select complete native cells within allocation-sized strips in finished-model space.
    // A stock change only redraws the sections of the goods whose fill changed.
    internal sealed class MixedContentsRenderer
    {
        private static readonly ConditionalWeakTable<StockpileVisualizers, MixedContentsRenderer> Instances = new ConditionalWeakTable<StockpileVisualizers, MixedContentsRenderer>();
        private static readonly FieldInfo Current = AccessTools.Field(typeof(StockpileVisualizers), "_currentVisualizer");
        private static readonly FieldInfo Goods = AccessTools.Field(typeof(StockpileVisualizers), "_goodService");
        private static readonly FieldInfo CurrentId = AccessTools.Field(typeof(StockpileVisualizers), "_currentGoodId");
        private static readonly FieldInfo Highlightable = AccessTools.Field(typeof(StockpileVisualizers), "_highlightableObject");
        private static readonly MethodInfo Select = AccessTools.Method(typeof(StockpileVisualizers), "GetVisualizer");

        // Reflection lookups are resolved once per type; the native types involved are internal.
        private static readonly Dictionary<(Type, string), FieldInfo> Fields = new Dictionary<(Type, string), FieldInfo>();
        private static readonly Dictionary<(Type, string), MethodInfo> Methods = new Dictionary<(Type, string), MethodInfo>();
        private static readonly Dictionary<Type, PropertyInfo> Visualizations = new Dictionary<Type, PropertyInfo>();
        private static readonly Type[] InitializeParameters = { typeof(GoodSpec), typeof(int) };
        private static readonly Type[] AmountParameters = { typeof(int) };
        private static readonly Type[] AddMaterialParameters = { typeof(Transform), typeof(Material) };
        internal static readonly Type[] DestroyMaterialParameters = { typeof(Material) };
        private static readonly object[] SelectArguments = new object[1];
        private static readonly object[] InitializeArguments = new object[2];
        private static readonly object[] AmountArguments = new object[1];
        private static readonly object[] AddMaterialArguments = new object[2];

        private struct Footprint
        {
            public float Min, Max;
        }

        // The mesh, material and object drawn for one good; Object is null while it has nothing to show.
        private sealed class Section
        {
            public string Id;
            public int Amount;
            public GameObject Object;
            public MeshRenderer Renderer;
        }

        private readonly List<Section> _sections = new List<Section>();
        // Where a good's full-capacity model spans in finished-model space. It does not depend on stock.
        private readonly Dictionary<string, Footprint> _footprints = new Dictionary<string, Footprint>(StringComparer.Ordinal);
        private KeyValuePair<string, int>[] _shares;
        private int _sharesRevision = -1;
        private int[] _amounts = Array.Empty<int>();
        private int _footprintCapacity = -1;
        private int _builtRevision;
        private int _builtCapacity;
        private bool _built;
        private GameObject _nativeObject;
        private MeshRenderer _nativeRenderer;
        private MeshFilter _nativeFilter;
        private Transform _parent;
        private object _entityMaterials;
        private object _highlightable;
        private bool _busy;
        private bool _failed;
        private MixedContentsRefreshQueue _queue;

        public static void Refresh(StockpileVisualizers owner)
        {
            var state = StorageState.Get(owner);
            // A building that left mixed mode goes back to the native visuals.
            if (state?.Active != true) { Clear(owner); return; }
            var instance = Instances.GetValue(owner, _ => new MixedContentsRenderer());
            try { instance.Schedule(owner, state); }
            catch (Exception ex) { instance.Fail(ex); }
        }

        private void Schedule(StockpileVisualizers owner, StorageState state)
        {
            if (_busy || _failed) return;
            if (_queue == null)
            {
                var original = Current.GetValue(owner);
                if (original == null) return;
                // One shared GoodVisualization serves every native visualizer of the building.
                var visualization = Field(original, "_goodVisualization");
                _nativeObject = (GameObject)Field(visualization, "_visualization");
                _entityMaterials = Field(visualization, "_entityMaterials");
                _highlightable = Highlightable.GetValue(owner);
                _nativeRenderer = _nativeObject.GetComponent<MeshRenderer>();
                _nativeFilter = _nativeObject.GetComponent<MeshFilter>();
                _parent = _nativeObject.transform.parent;
                _queue = _nativeObject.AddComponent<MixedContentsRefreshQueue>();
                _queue.IsVisible = IsOnScreen;
                _queue.RefreshAction = () =>
                {
                    try { Render(owner, state); }
                    catch (Exception ex) { Fail(ex); }
                };
            }
            _queue.Dirty = true;
        }

        public static void Clear(StockpileVisualizers owner)
        {
            if (Instances.TryGetValue(owner, out var instance))
            {
                instance.Release();
                if (instance._queue != null) instance._queue.Dirty = false;
            }
        }

        private void Release()
        {
            foreach (var section in _sections) DestroySection(section);
            _sections.Clear();
            _built = false;
            if (_nativeRenderer != null) _nativeRenderer.enabled = true;
        }

        private static void DestroySection(Section section)
        {
            if (section.Object != null)
            {
                section.Object.SetActive(false);
                UnityEngine.Object.Destroy(section.Object);
            }
            section.Object = null;
            section.Renderer = null;
        }

        // A building the camera cannot see keeps its previous sections until it comes into view.
        // Storage with nothing drawn yet is always ready to build.
        private bool IsOnScreen()
        {
            bool drawn = false;
            foreach (var section in _sections)
            {
                if (section.Renderer == null) continue;
                drawn = true;
                if (section.Renderer.isVisible) return true;
            }
            return !drawn;
        }

        private static object Field(object value, string name)
        {
            var key = (value.GetType(), name);
            if (!Fields.TryGetValue(key, out var field)) Fields[key] = field = AccessTools.Field(key.Item1, name);
            return field.GetValue(value);
        }

        // BaseComponent also defines Initialize; name-only lookup is ambiguous.
        internal static MethodInfo Method(Type type, string name, Type[] parameters)
        {
            if (!Methods.TryGetValue((type, name), out var method)) Methods[(type, name)] = method = AccessTools.Method(type, name, parameters);
            return method;
        }

        private static void Initialize(object visualizer, GoodSpec spec, int capacity)
        {
            InitializeArguments[0] = spec;
            InitializeArguments[1] = capacity;
            Method(visualizer.GetType(), "Initialize", InitializeParameters).Invoke(visualizer, InitializeArguments);
        }

        private static void UpdateAmount(object visualizer, int amount)
        {
            AmountArguments[0] = amount;
            Method(visualizer.GetType(), "UpdateAmount", AmountParameters).Invoke(visualizer, AmountArguments);
        }

        private void Fail(Exception ex)
        {
            _failed = true;
            Release();
            Debug.LogWarning("[MixedStorage] Mixed visuals unavailable for this building; using native visuals. " + ex.GetBaseException());
        }

        // Render the section's fill fraction, not the whole building's total stock.
        private static int SectionAmount(int stock, int limit, int capacity) =>
            stock == 0 || limit == 0 ? 0 : (int)Math.Min(capacity, ((long)stock * capacity + limit - 1) / limit);

        private static KeyValuePair<string, int>[] SortedShares(StorageState state)
        {
            var shares = new KeyValuePair<string, int>[state.Shares.Count];
            int index = 0;
            foreach (var share in state.Shares) shares[index++] = share;
            Array.Sort(shares, (a, b) => string.CompareOrdinal(a.Key, b.Key));
            return shares;
        }

        private void Render(StockpileVisualizers owner, StorageState state)
        {
            if (_busy || _failed) return;
            if (_sharesRevision != state.Revision)
            {
                _shares = SortedShares(state);
                _sharesRevision = state.Revision;
            }
            // Single-good storage continues to use the original renderer.
            if (_shares.Length < 2) { Release(); return; }
            var inventory = state.Inventory;
            int capacity = inventory.Capacity;
            if (_amounts.Length != _shares.Length) _amounts = new int[_shares.Length];
            for (int i = 0; i < _shares.Length; i++)
            {
                string id = _shares[i].Key;
                _amounts[i] = SectionAmount(inventory.AmountInStock(id), state.Limit(id), capacity);
            }
            // New allocations or capacity redraw everything; stock alone redraws the sections that changed.
            bool all = !_built || _builtRevision != state.Revision || _builtCapacity != capacity;
            bool changed = all;
            for (int i = 0; !changed && i < _shares.Length; i++) changed = _sections[i].Amount != _amounts[i];
            if (!changed) { _nativeRenderer.enabled = false; return; }
            var original = Current.GetValue(owner);
            if (original == null) return;
            var service = (IGoodService)Goods.GetValue(owner);
            var originalId = (string)CurrentId.GetValue(owner);
            _busy = true;
            try
            {
                if (all)
                {
                    Release();
                    foreach (var share in _shares) _sections.Add(new Section { Id = share.Key });
                }
                if (_footprintCapacity != capacity)
                {
                    _footprints.Clear();
                    _footprintCapacity = capacity;
                }
                var samples = new Sample[_shares.Length];
                bool touched = false;
                try
                {
                    for (int i = 0; i < _shares.Length; i++)
                    {
                        string id = _shares[i].Key;
                        bool draw = _amounts[i] > 0 && (all || _sections[i].Amount != _amounts[i]);
                        bool measure = !_footprints.ContainsKey(id);
                        if (!draw && !measure) continue;
                        var spec = service.GetGood(id);
                        SelectArguments[0] = spec;
                        var visualizer = Select.Invoke(owner, SelectArguments);
                        if (visualizer == null) throw new InvalidOperationException("No native visualizer for " + id);
                        Initialize(visualizer, spec, capacity);
                        touched = true;
                        if (measure)
                        {
                            // Full geometry establishes a footprint independent of inventory fill.
                            UpdateAmount(visualizer, capacity);
                            _footprints[id] = Measure();
                        }
                        if (!draw) continue;
                        UpdateAmount(visualizer, _amounts[i]);
                        samples[i] = new Sample
                        {
                            Cells = NativeCells(visualizer),
                            Mesh = _nativeFilter.sharedMesh,
                            Matrix = NativeMatrix(),
                            Material = new Material(_nativeRenderer.sharedMaterial)
                        };
                    }
                    float min = float.PositiveInfinity, max = float.NegativeInfinity;
                    foreach (var share in _shares)
                    {
                        var footprint = _footprints[share.Key];
                        if (footprint.Min < min) min = footprint.Min;
                        if (footprint.Max > max) max = footprint.Max;
                    }
                    float cursor = min;
                    for (int index = 0; index < _shares.Length; index++)
                    {
                        bool last = index == _shares.Length - 1;
                        float end = last ? max : cursor + (max - min) * _shares[index].Value / AllocationPlan.Total;
                        var section = _sections[index];
                        if (all || section.Amount != _amounts[index])
                        {
                            DestroySection(section);
                            section.Amount = _amounts[index];
                            if (samples[index] != null) Draw(section, samples[index], cursor, end, last);
                        }
                        cursor = end;
                    }
                }
                finally
                {
                    foreach (var sample in samples) if (sample?.Material != null) UnityEngine.Object.Destroy(sample.Material);
                    // Restore internal native state for later updates, saves and the fallback path.
                    if (touched)
                    {
                        Initialize(original, service.GetGood(originalId), capacity);
                        UpdateAmount(original, inventory.TotalAmountInStock);
                    }
                }
                _nativeRenderer.enabled = false;
                _built = true;
                _builtRevision = state.Revision;
                _builtCapacity = capacity;
                Method(_highlightable.GetType(), "UpdateColorAndHighlight", Type.EmptyTypes).Invoke(_highlightable, null);
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
            finally { _busy = false; }
        }

        private Matrix4x4 NativeMatrix() => _parent.worldToLocalMatrix * _nativeObject.transform.localToWorldMatrix;

        private Footprint Measure()
        {
            var matrix = NativeMatrix();
            var bounds = _nativeFilter.sharedMesh.bounds;
            var footprint = new Footprint { Min = float.PositiveInfinity, Max = float.NegativeInfinity };
            for (int corner = 0; corner < 8; corner++)
            {
                var point = matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                footprint.Min = Mathf.Min(footprint.Min, point.x);
                footprint.Max = Mathf.Max(footprint.Max, point.x);
            }
            return footprint;
        }

        private void Draw(Section section, Sample sample, float from, float to, bool last)
        {
            var mesh = WholeCellGeometry.Build(sample.Mesh, sample.Matrix, sample.Cells, from, to, last);
            if (mesh == null) return;
            var obj = new GameObject("MixedStorageContents_" + section.Id);
            section.Object = obj;
            obj.layer = _nativeObject.layer;
            obj.transform.SetParent(_parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            section.Renderer = renderer;
            renderer.sharedMaterial = sample.Material;
            renderer.shadowCastingMode = _nativeRenderer.shadowCastingMode;
            renderer.receiveShadows = _nativeRenderer.receiveShadows;
            var resources = obj.AddComponent<MixedContentsResources>();
            resources.OwnedMesh = mesh;
            resources.OwnedMaterial = sample.Material;
            resources.EntityMaterials = _entityMaterials;
            sample.Material = null;
            AddMaterialArguments[0] = obj.transform;
            AddMaterialArguments[1] = resources.OwnedMaterial;
            Method(_entityMaterials.GetType(), "AddMaterial", AddMaterialParameters).Invoke(_entityMaterials, AddMaterialArguments);
        }

        private sealed class Sample
        {
            public Mesh[] Cells;
            public Mesh Mesh;
            public Matrix4x4 Matrix;
            public Material Material;
        }

        private static Mesh[] NativeCells(object visualizer)
        {
            var type = visualizer.GetType();
            if (!Visualizations.TryGetValue(type, out var property)) Visualizations[type] = property = AccessTools.Property(type, "CurrentVisualization");
            var spec = property?.GetValue(visualizer) as GoodVisualizationSpec;
            if (spec == null) return Array.Empty<Mesh>();
            return new[] { spec.PrimaryMesh?.Asset, spec.SecondaryMesh?.Asset };
        }
    }

    // Coalesce delivery and allocation notifications, at most five rebuilds per second while the building
    // is in view. Out of view it redraws at most once every two seconds, so a change is never held back long
    // even if the camera test were wrong.
    public sealed class MixedContentsRefreshQueue : MonoBehaviour
    {
        private const float VisibleInterval = .2f;
        private const float HiddenInterval = 2f;
        internal Action RefreshAction;
        internal Func<bool> IsVisible;
        internal bool Dirty;
        private float _next;
        private float _hiddenUntil;
        private void LateUpdate()
        {
            if (!Dirty) return;
            float now = Time.unscaledTime;
            if (now < _next) return;
            if (now < _hiddenUntil && IsVisible != null && !IsVisible()) return;
            Dirty = false;
            _next = now + VisibleInterval;
            _hiddenUntil = now + HiddenInterval;
            RefreshAction?.Invoke();
        }
    }

    public sealed class MixedContentsResources : MonoBehaviour
    {
        internal Mesh OwnedMesh;
        internal Material OwnedMaterial;
        internal object EntityMaterials;
        private void OnDestroy()
        {
            if (OwnedMesh != null) Destroy(OwnedMesh);
            if (OwnedMaterial != null)
            {
                try { MixedContentsRenderer.Method(EntityMaterials.GetType(), "DestroyMaterial", MixedContentsRenderer.DestroyMaterialParameters).Invoke(EntityMaterials, new object[] { OwnedMaterial }); }
                catch { Destroy(OwnedMaterial); }
            }
        }
    }

    [HarmonyPatch(typeof(StockpileVisualizers), "OnInventoryChanged")]
    internal static class MixedInventoryVisualPatch
    {
        static void Postfix(StockpileVisualizers __instance) => MixedContentsRenderer.Refresh(__instance);
    }
    [HarmonyPatch(typeof(StockpileVisualizers), "OnDisallowedGoodsChanged")]
    internal static class MixedAllocationVisualPatch
    {
        static void Postfix(StockpileVisualizers __instance) => MixedContentsRenderer.Refresh(__instance);
    }
    [HarmonyPatch(typeof(StockpileVisualizers), nameof(StockpileVisualizers.SetCurrentVisualizer))]
    internal static class MixedSelectedVisualPatch
    {
        static void Postfix(StockpileVisualizers __instance) => MixedContentsRenderer.Refresh(__instance);
    }
    [HarmonyPatch(typeof(StockpileVisualizers), nameof(StockpileVisualizers.OnExitFinishedState))]
    internal static class MixedExitVisualPatch
    {
        static void Postfix(StockpileVisualizers __instance) => MixedContentsRenderer.Clear(__instance);
    }
}
