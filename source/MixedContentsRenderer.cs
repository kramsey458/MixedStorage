using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Timberborn.Goods;
using Timberborn.StockpileVisualization;
using UnityEngine;
using UnityEngine.Rendering;

namespace MixedStorage
{
    // Sample the native visualizers without creating components or consuming simulation RNG.
    // Clip their geometry into stable, allocation-sized strips in finished-model space.
    internal sealed class MixedContentsRenderer
    {
        private static readonly ConditionalWeakTable<StockpileVisualizers, MixedContentsRenderer> Instances = new ConditionalWeakTable<StockpileVisualizers, MixedContentsRenderer>();
        private static readonly FieldInfo Current = AccessTools.Field(typeof(StockpileVisualizers), "_currentVisualizer");
        private static readonly FieldInfo Goods = AccessTools.Field(typeof(StockpileVisualizers), "_goodService");
        private static readonly FieldInfo CurrentId = AccessTools.Field(typeof(StockpileVisualizers), "_currentGoodId");
        private static readonly MethodInfo Select = AccessTools.Method(typeof(StockpileVisualizers), "GetVisualizer");
        private readonly List<GameObject> _objects = new List<GameObject>();
        private MeshRenderer _nativeRenderer;
        private string _signature;
        private bool _busy;
        private bool _failed;
        private MixedContentsRefreshQueue _queue;

        public static void Refresh(StockpileVisualizers owner)
        {
            var state = StorageState.Get(owner);
            if (state?.Active != true) return;
            var instance = Instances.GetValue(owner, _ => new MixedContentsRenderer());
            try { instance.Schedule(owner, state); }
            catch (Exception ex) { instance.Fail(ex); }
        }

        private void Schedule(StockpileVisualizers owner, StorageState state)
        {
            if (_busy || _failed) return;
            var original = Current.GetValue(owner);
            if (original == null) return;
            if (_queue == null)
            {
                var native = (GameObject)Field(Field(original, "_goodVisualization"), "_visualization");
                _queue = native.AddComponent<MixedContentsRefreshQueue>();
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
                instance._signature = null;
                if (instance._queue != null) instance._queue.Dirty = false;
            }
        }

        private void Release()
        {
            foreach (var obj in _objects)
            {
                if (obj == null) continue;
                obj.SetActive(false);
                UnityEngine.Object.Destroy(obj);
            }
            _objects.Clear();
            if (_nativeRenderer != null) _nativeRenderer.enabled = true;
        }

        private static object Field(object value, string name) => AccessTools.Field(value.GetType(), name).GetValue(value);
        private static void Call(object value, string name, params object[] args) => AccessTools.Method(value.GetType(), name).Invoke(value, args);

        private void Fail(Exception ex)
        {
            _failed = true;
            Release();
            Debug.LogWarning("[MixedStorage] Mixed visuals unavailable for this building; using native visuals. " + ex.GetBaseException().Message);
        }

        private void Render(StockpileVisualizers owner, StorageState state)
        {
            if (_busy || _failed) return;
            var shares = state.Shares.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
            // Single-good storage continues to use the original renderer.
            if (shares.Length < 2) { Release(); _signature = null; return; }
            var signature = state.Inventory.Capacity + ":" + string.Join(";", shares.Select(x => x.Key + "=" + x.Value + "/" + state.Inventory.AmountInStock(x.Key)));
            if (_signature == signature) { if (_nativeRenderer != null) _nativeRenderer.enabled = false; return; }
            var original = Current.GetValue(owner);
            if (original == null) return;
            var service = (IGoodService)Goods.GetValue(owner);
            var originalId = (string)CurrentId.GetValue(owner);
            _busy = true;
            try
            {
                Release();
                var visualization = Field(original, "_goodVisualization");
                var nativeObject = (GameObject)Field(visualization, "_visualization");
                _nativeRenderer = nativeObject.GetComponent<MeshRenderer>();
                var parent = nativeObject.transform.parent;
                var entityMaterials = Field(visualization, "_entityMaterials");
                var samples = new List<Sample>();
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                try
                {
                    foreach (var share in shares)
                    {
                        var spec = service.GetGood(share.Key);
                        var visualizer = Select.Invoke(owner, new object[] { spec });
                        if (visualizer == null) throw new InvalidOperationException("No native visualizer for " + share.Key);
                        Call(visualizer, "Initialize", spec, state.Inventory.Capacity);
                        // Full geometry establishes a footprint independent of inventory fill.
                        Call(visualizer, "UpdateAmount", state.Inventory.Capacity);
                        var filter = nativeObject.GetComponent<MeshFilter>();
                        var matrix = parent.worldToLocalMatrix * nativeObject.transform.localToWorldMatrix;
                        var bounds = filter.sharedMesh.bounds;
                        for (int corner = 0; corner < 8; corner++)
                        {
                            var point = matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents,
                                new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                            min = Mathf.Min(min, point.x); max = Mathf.Max(max, point.x);
                        }
                        int limit = state.Limit(share.Key), stock = state.Inventory.AmountInStock(share.Key);
                        if (stock == 0 || limit == 0) { samples.Add(null); continue; }
                        // Render the section's fill fraction, not the whole building's total stock.
                        int amount = (int)Math.Min(state.Inventory.Capacity, ((long)stock * state.Inventory.Capacity + limit - 1) / limit);
                        Call(visualizer, "UpdateAmount", amount);
                        samples.Add(new Sample
                        {
                            Mesh = filter.sharedMesh,
                            Matrix = parent.worldToLocalMatrix * nativeObject.transform.localToWorldMatrix,
                            Material = new Material(_nativeRenderer.sharedMaterial),
                            Id = share.Key
                        });
                    }
                    float cursor = min;
                    for (int index = 0; index < shares.Length; index++)
                    {
                        float end = index == shares.Length - 1 ? max : cursor + (max - min) * shares[index].Value / AllocationPlan.Total;
                        var sample = samples[index];
                        if (sample != null)
                        {
                            var mesh = StorageMeshClipper.Clip(sample.Mesh, sample.Matrix, cursor, end);
                            var obj = new GameObject("MixedStorageContents_" + sample.Id);
                            obj.layer = nativeObject.layer;
                            obj.transform.SetParent(parent, false);
                            _objects.Add(obj);
                            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                            var renderer = obj.AddComponent<MeshRenderer>();
                            renderer.sharedMaterial = sample.Material;
                            renderer.shadowCastingMode = _nativeRenderer.shadowCastingMode;
                            renderer.receiveShadows = _nativeRenderer.receiveShadows;
                            var resources = obj.AddComponent<MixedContentsResources>();
                            resources.OwnedMesh = mesh;
                            resources.OwnedMaterial = sample.Material;
                            resources.EntityMaterials = entityMaterials;
                            sample.Material = null;
                            Call(entityMaterials, "AddMaterial", obj.transform, resources.OwnedMaterial);
                        }
                        cursor = end;
                    }
                }
                finally
                {
                    foreach (var sample in samples) if (sample?.Material != null) UnityEngine.Object.Destroy(sample.Material);
                    // Restore internal native state for later updates, saves and the fallback path.
                    Call(original, "Initialize", service.GetGood(originalId), state.Inventory.Capacity);
                    Call(original, "UpdateAmount", state.Inventory.TotalAmountInStock);
                }
                _nativeRenderer.enabled = false;
                _signature = signature;
                Call(Field(owner, "_highlightableObject"), "UpdateColorAndHighlight");
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
            finally { _busy = false; }
        }

        private sealed class Sample
        {
            public Mesh Mesh;
            public Matrix4x4 Matrix;
            public Material Material;
            public string Id;
        }
    }

    // Coalesce delivery and allocation notifications, at most five rebuilds per second.
    public sealed class MixedContentsRefreshQueue : MonoBehaviour
    {
        internal Action RefreshAction;
        internal bool Dirty;
        private float _next;
        private void LateUpdate()
        {
            if (!Dirty || Time.unscaledTime < _next) return;
            Dirty = false;
            _next = Time.unscaledTime + .2f;
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
                try { AccessTools.Method(EntityMaterials.GetType(), "DestroyMaterial").Invoke(EntityMaterials, new object[] { OwnedMaterial }); }
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
