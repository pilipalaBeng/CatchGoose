using System.Collections.Generic;
using UnityEngine;

namespace CatchGoose
{
    /// <summary>锅里那一堆。负责生成、遮挡判定、取走与放回。</summary>
    public class Board : MonoBehaviour
    {
        const float BlockOverlap = 0.26f;
        const float SettleDistance = 0.07f;

        readonly List<ItemEntity> _items = new List<ItemEntity>();
        LevelConfig _cfg;
        float _probeHalf;

        public int Remaining => _items.Count;

        public void Build(LevelConfig cfg, int seed)
        {
            Clear();
            _cfg = cfg;
            _probeHalf = cfg.itemSize * 0.42f;
            foreach (var slot in LevelGenerator.Generate(cfg, seed))
                Spawn(slot, cfg);
            RefreshBlocking();
        }

        void Spawn(SlotData slot, LevelConfig cfg)
        {
            var go = GameObject.CreatePrimitive(ItemEntity.ShapeFor(slot.typeId));
            go.name = "Item_" + slot.typeId;
            go.transform.SetParent(transform, false);
            go.transform.position = slot.position;
            var item = go.AddComponent<ItemEntity>();
            item.Init(slot.typeId, slot.layerIndex, cfg.itemSize);
            _items.Add(item);
        }

        public void Clear()
        {
            foreach (var item in _items)
                if (item != null) Destroy(item.gameObject);
            _items.Clear();
        }

        public ItemEntity Raycast(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit)) return null;
            return hit.collider.GetComponent<ItemEntity>();
        }

        /// <summary>上方压着东西且投影重叠够多 = 取不出来。</summary>
        public bool IsBlocked(ItemEntity target)
        {
            Rect self = XZRect(target);
            foreach (var other in _items)
            {
                if (other == target) continue;
                if (other.transform.position.y <= target.transform.position.y + 0.05f) continue;
                if (OverlapRatio(self, XZRect(other)) > BlockOverlap) return true;
            }
            return false;
        }

        public void Take(ItemEntity item)
        {
            if (!_items.Remove(item)) return;
            RefreshBlocking();
            SettleAbove(item);
        }

        public void PutBack(ItemEntity item)
        {
            item.transform.position = item.Origin;
            item.gameObject.SetActive(true);
            if (!_items.Contains(item)) _items.Add(item);
            RefreshBlocking();
        }

        void RefreshBlocking()
        {
            foreach (var item in _items) item.SetBlocked(IsBlocked(item));
        }

        void SettleAbove(ItemEntity removed)
        {
            foreach (var item in _items)
            {
                if (item.transform.position.y <= removed.transform.position.y) continue;
                if (FlatDistance(item, removed) > _cfg.itemSize * 1.6f) continue;
                StartCoroutine(Tween.Sink(item.transform, SettleDistance, 0.18f));
            }
        }

        /// <summary>洗牌只在同层内互换位置：解锁顺序不变，关卡依然有解。</summary>
        public void Shuffle()
        {
            var groups = new Dictionary<int, List<ItemEntity>>();
            foreach (var item in _items)
            {
                if (!groups.TryGetValue(item.LayerIndex, out var list))
                {
                    list = new List<ItemEntity>();
                    groups[item.LayerIndex] = list;
                }
                list.Add(item);
            }
            foreach (var pair in groups) ShuffleLayer(pair.Value);
            RefreshBlocking();
        }

        static void ShuffleLayer(List<ItemEntity> layer)
        {
            var spots = new List<Vector3>(layer.Count);
            foreach (var item in layer) spots.Add(item.transform.position);
            for (int i = spots.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (spots[i], spots[j]) = (spots[j], spots[i]);
            }
            for (int i = 0; i < layer.Count; i++)
            {
                layer[i].Origin = spots[i];
                layer[i].transform.position = spots[i];
            }
        }

        static float FlatDistance(ItemEntity a, ItemEntity b)
        {
            Vector3 pa = a.transform.position;
            Vector3 pb = b.transform.position;
            return Mathf.Sqrt((pa.x - pb.x) * (pa.x - pb.x) + (pa.z - pb.z) * (pa.z - pb.z));
        }

        Rect XZRect(ItemEntity item)
        {
            Vector3 p = item.transform.position;
            float size = _probeHalf * 2f;
            return new Rect(p.x - _probeHalf, p.z - _probeHalf, size, size);
        }

        static float OverlapRatio(Rect a, Rect b)
        {
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            if (w <= 0f || h <= 0f) return 0f;
            return (w * h) / (a.width * a.height);
        }
    }
}
