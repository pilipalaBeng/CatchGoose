using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatchGoose
{
    /// <summary>一个坑位：摆放位置 + 归属种类。</summary>
    public struct SlotData
    {
        public int typeId;
        public int layerIndex;
        public Vector3 position;
    }

    /// <summary>
    /// 逆向生成关卡：先按「从上到下」的解锁顺序排好坑位，
    /// 再每 3 个坑位打包成同种类的一组 —— 这样顺着拿必然能通关。
    /// </summary>
    public static class LevelGenerator
    {
        public static List<SlotData> Generate(LevelConfig cfg, int seed, Wok wok)
        {
            var rng = new System.Random(seed);
            var slots = BuildSlotsInUnlockOrder(cfg, rng, wok);
            AssignTypesInTriples(slots, cfg);
            ScatterWithinWindow(slots, cfg, rng);
            return slots;
        }

        static List<SlotData> BuildSlotsInUnlockOrder(LevelConfig cfg, System.Random rng, Wok wok)
        {
            var slots = new List<SlotData>(cfg.totalItems);
            var perLayer = SplitPerLayer(cfg.totalItems, cfg.layers);
            for (int layer = cfg.layers - 1; layer >= 0; layer--)
                AddLayer(slots, cfg, rng, wok, layer, perLayer[layer]);
            return slots;
        }

        static int[] SplitPerLayer(int total, int layers)
        {
            var counts = new int[layers];
            for (int i = 0; i < total; i++) counts[i % layers]++;
            return counts;
        }

        static void AddLayer(List<SlotData> slots, LevelConfig cfg, System.Random rng, Wok wok, int layer, int count)
        {
            float t = cfg.layers > 1 ? layer / (float)(cfg.layers - 1) : 0f;
            float radius = Mathf.Lerp(cfg.spreadRadiusBottom, cfg.spreadRadiusTop, t);
            var placed = new List<Vector2>(count);

            for (int i = 0; i < count; i++)
            {
                Vector2 p = SamplePoint(rng, radius, placed, cfg.itemSize);
                placed.Add(p);

                // 落点贴着锅壁：锅心深、锅边浅，所以同一层里外圈的位置天然比锅心高。
                // 层数只负责「往上再叠多少」，这样堆出来是一个贴着锅的丘，不是一根柱子。
                float surface = wok != null ? wok.SurfaceYAt(p.magnitude) : cfg.itemSize * 0.5f;
                float y = surface + cfg.itemSize * 0.5f + layer * cfg.layerHeight;

                slots.Add(new SlotData
                {
                    layerIndex = layer,
                    position = new Vector3(p.x, y, p.y)
                });
            }
        }

        /// <summary>
        /// 拒绝采样找一个「离已放置的都不太近」的点。
        /// 尝试次数不能太小：试几次就放弃的话，物件会全堆到先放的那一片，锅心挤、锅边空。
        /// </summary>
        static Vector2 SamplePoint(System.Random rng, float radius, List<Vector2> placed, float minGap)
        {
            Vector2 best = Vector2.zero;
            float bestDist = -1f;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vector2 p = RandomInCircle(rng, radius);
                float nearest = NearestDistance(p, placed);
                if (nearest > minGap) return p;
                if (nearest > bestDist) { bestDist = nearest; best = p; }
            }
            return best;
        }

        static Vector2 RandomInCircle(System.Random rng, float radius)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt((float)rng.NextDouble()) * radius;
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        static float NearestDistance(Vector2 p, List<Vector2> placed)
        {
            float best = float.MaxValue;
            foreach (var q in placed)
                best = Mathf.Min(best, Vector2.Distance(p, q));
            return best;
        }

        static void AssignTypesInTriples(List<SlotData> slots, LevelConfig cfg)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                slot.typeId = (i / 3) % cfg.typeCount;
                slots[i] = slot;
            }
        }

        /// <summary>
        /// 局部窗口洗牌：把同组的三个在附近打散，制造「要找一找」的手感。
        /// 窗口越大越难，上限 7（此时托盘最多积压两三组，8 槽位仍安全）。
        /// </summary>
        static void ScatterWithinWindow(List<SlotData> slots, LevelConfig cfg, System.Random rng)
        {
            int window = 1 + Mathf.RoundToInt(cfg.scatter * 6f);
            if (window <= 1) return;
            int safeWindow = Mathf.Min(window, cfg.traySlots - 1);
            for (int start = 0; start < slots.Count; start += safeWindow)
                ShuffleRange(slots, start, Mathf.Min(start + safeWindow, slots.Count), rng);
        }

        static void ShuffleRange(List<SlotData> slots, int start, int end, System.Random rng)
        {
            for (int i = end - 1; i > start; i--)
            {
                int j = start + rng.Next(i - start + 1);
                (slots[i], slots[j]) = (slots[j], slots[i]);
            }
        }
    }
}
