using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CatchGoose
{
    /// <summary>下方托盘：同类凑满三个就消掉，塞满还没消就算输。</summary>
    public class Tray : MonoBehaviour
    {
        [SerializeField] Transform[] anchors;
        readonly List<ItemEntity> _held = new List<ItemEntity>();
        int _capacity;

        public int Count => _held.Count;
        public bool IsFull => _held.Count >= _capacity;

        public void Setup(int capacity)
        {
            _capacity = capacity;
        }

        public void Clear()
        {
            foreach (var item in _held)
                if (item != null) Destroy(item.gameObject);
            _held.Clear();
        }

        /// <summary>放入一个物体。返回本次被消掉的三个（没消掉则为 null），塞不下返回 false。</summary>
        public bool TryPlace(ItemEntity item, out List<ItemEntity> cleared)
        {
            cleared = null;
            if (_held.Count >= _capacity) return false;

            _held.Insert(InsertIndex(item.TypeId), item);
            var same = CollectSameType(item.TypeId);
            if (same.Count >= 3)
            {
                foreach (var one in same) _held.Remove(one);
                cleared = same;
            }
            RelayoutExcept(item);
            return true;
        }

        public void Remove(ItemEntity item)
        {
            if (_held.Remove(item)) Relayout();
        }

        public IEnumerator FlyInRoutine(ItemEntity item, float arcHeight, float duration)
        {
            yield return Tween.MoveArc(item.transform, AnchorFor(_held.IndexOf(item)), arcHeight, duration);
        }

        public List<ItemEntity> TakeAll()
        {
            var all = new List<ItemEntity>(_held);
            _held.Clear();
            return all;
        }

        int InsertIndex(int typeId)
        {
            for (int i = _held.Count - 1; i >= 0; i--)
                if (_held[i].TypeId == typeId) return i + 1;
            return _held.Count;
        }

        List<ItemEntity> CollectSameType(int typeId)
        {
            var result = new List<ItemEntity>();
            foreach (var item in _held)
                if (item.TypeId == typeId) result.Add(item);
            return result;
        }

        Vector3 AnchorFor(int index)
        {
            if (anchors == null || anchors.Length == 0) return transform.position;
            return anchors[Mathf.Clamp(index, 0, anchors.Length - 1)].position;
        }

        void Relayout()
        {
            for (int i = 0; i < _held.Count; i++)
                StartCoroutine(Tween.MoveTo(_held[i].transform, AnchorFor(i), 0.2f));
        }

        /// <summary>刚放进来那个由飞入动画负责，别让两个协程抢同一个 transform。</summary>
        void RelayoutExcept(ItemEntity skip)
        {
            for (int i = 0; i < _held.Count; i++)
                if (_held[i] != skip) StartCoroutine(Tween.MoveTo(_held[i].transform, AnchorFor(i), 0.2f));
        }
    }
}
