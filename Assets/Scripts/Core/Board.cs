using System.Collections.Generic;
using UnityEngine;

namespace CatchGoose
{
    /// <summary>
    /// 锅里那一堆。负责生成、翻锅物理、遮挡判定、取走与放回。
    /// 伪物理：倾斜重力 + 弹簧回锚 + 阻尼 + 同层推挤与黏连，不引刚体。
    /// </summary>
    public class Board : MonoBehaviour
    {
        const float BlockOverlap = 0.26f;
        const float SettleDistance = 0.07f;

        [Header("倾斜")]
        [Tooltip("垂直重力，颠锅抛起来的东西靠它落回锅里")]
        public float gravity = 16f;
        [Tooltip("倾斜产生的水平加速度，物件往低处滑")]
        public float tiltGravity = 7.5f;
        [Tooltip("每多一层，被晃动的幅度加成")]
        public float layerMobility = 0.2f;
        [Tooltip("弹簧把物件拉回锚点的强度")]
        public float spring = 4f;

        [Header("黏滞感")]
        [Tooltip("速度衰减率，越小越黏、越拖沓")]
        public float damping = 7f;
        [Tooltip("锚点跟随速度：越大越「塑」，颠完的布局不会自己弹回去")]
        public float plasticity = 1.4f;
        [Tooltip("同层互相推开的距离（物体尺寸的倍数）")]
        public float separationScale = 1.0f;
        [Tooltip("同层互相黏连的距离（物体尺寸的倍数）")]
        public float cohesionScale = 1.8f;
        public float separationForce = 13f;
        [Tooltip("黏连力，「扒拉一坨」的手感就靠它")]
        public float cohesionForce = 3.2f;

        [Header("颠锅")]
        public float tossLift = 3.6f;
        public float tossScatter = 1.1f;
        [Tooltip("向锅心聚拢的强度：原版的代价。每次颠挪一点点，狂颠才会堆成一坨")]
        public float tossPull = 0.2f;
        public float tossCooldown = 0.35f;
        [Tooltip("落地反弹")]
        public float bounce = 0.22f;

        [Header("刷新")]
        [Tooltip("遮挡关系重算间隔，物件一直在动，没必要每帧算")]
        public float blockRefreshInterval = 0.1f;

        [SerializeField] TiltInput tiltInput;
        [SerializeField] Wok wok;

        readonly List<ItemEntity> _items = new List<ItemEntity>();
        LevelConfig _cfg;
        float _probeHalf;
        float _blockTimer;
        float _tossCd;

        public int Remaining => _items.Count;
        public bool CanToss => _tossCd <= 0f && _items.Count > 0;

        void Update()
        {
            if (_items.Count == 0) return;
            if (_tossCd > 0f) _tossCd -= Time.deltaTime;

            Simulate(Time.deltaTime);

            _blockTimer += Time.deltaTime;
            if (_blockTimer >= blockRefreshInterval)
            {
                _blockTimer = 0f;
                RefreshBlocking();
            }
        }

        public void Build(LevelConfig cfg, int seed)
        {
            Clear();
            _cfg = cfg;
            _probeHalf = cfg.itemSize * 0.42f;
            foreach (var slot in LevelGenerator.Generate(cfg, seed, wok))
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
            item.FloorY = slot.position.y;
            _items.Add(item);
        }

        public void Clear()
        {
            foreach (var item in _items)
                if (item != null) Destroy(item.gameObject);
            _items.Clear();
        }

        /// <summary>颠一下锅：整体上抛 + 随机散开 + 轻微向锅心聚。</summary>
        public bool Toss()
        {
            if (!CanToss) return false;
            _tossCd = tossCooldown;
            float energy = tiltInput != null ? tiltInput.ShakeEnergy : 0f;

            foreach (var item in _items)
            {
                Vector3 p = item.transform.position;
                float flat = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                Vector3 toCenter = flat > 1e-4f ? new Vector3(-p.x / flat, 0f, -p.z / flat) : Vector3.zero;
                float lift = tossLift * (0.75f + Random.value * 0.5f) * (1f + item.LayerIndex * 0.08f);

                // 锚点跟着一起挪，否则弹簧会把物件原样拉回去，颠了等于白颠
                Vector3 drift = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * tossScatter
                    + toCenter * tossPull;
                item.Anchor += drift;

                item.Velocity += Vector3.up * lift * (1f + energy * 0.35f) + drift * 0.6f;
            }
            return true;
        }

        void Simulate(float dt)
        {
            float step = Mathf.Min(dt, 1f / 30f);
            Vector2 tilt = tiltInput != null ? tiltInput.Tilt : Vector2.zero;
            UpdateSupports();

            foreach (var item in _items)
            {
                Transform t = item.transform;
                Vector3 pos = t.position;
                Vector3 vel = item.Velocity;

                float mobility = 1f + item.LayerIndex * layerMobility;
                vel.x += tilt.x * tiltGravity * mobility * step;
                vel.z += tilt.y * tiltGravity * mobility * step;
                vel.y -= gravity * step;
                vel += (item.Anchor - pos) * spring * step;
                vel *= Mathf.Exp(-damping * step);

                pos += vel * step;
                if (pos.y < item.FloorY)
                {
                    pos.y = item.FloorY;
                    if (vel.y < 0f) vel.y = -vel.y * bounce;
                }

                item.Velocity = vel;
                // 锚点慢慢跟上，所以颠出来的新布局不会自己弹回原样
                item.Anchor = Vector3.Lerp(item.Anchor, pos, plasticity * step);
                t.position = pos;
            }

            Separate(step);
            ClampToPot();
        }

        /// <summary>
        /// 谁踩在谁身上：下方物件撑住上方的。
        /// 没有这一步，物件只停在自己那一层的地板上，颠一下就被推成一饼，堆叠感全没了。
        /// </summary>
        void UpdateSupports()
        {
            float half = _cfg.itemSize * 0.5f;
            float reach = _cfg.itemSize * 0.72f;
            float rise = _cfg.itemSize * 0.78f;
            float reach2 = reach * reach;

            foreach (var item in _items)
            {
                Vector3 p = item.transform.position;
                float floor = WokFloorY(p, half);
                foreach (var other in _items)
                {
                    if (other == item) continue;
                    Vector3 o = other.transform.position;
                    if (o.y >= p.y - 0.02f) continue;
                    float dx = o.x - p.x;
                    float dz = o.z - p.z;
                    if (dx * dx + dz * dz > reach2) continue;
                    float top = o.y + rise;
                    if (top > floor) floor = top;
                }
                item.FloorY = floor;
            }
        }

        /// <summary>锅壁本身的地板：越靠锅边越高。物件被晃到外圈会顺着锅壁爬上去，再滑回来。</summary>
        float WokFloorY(Vector3 position, float half)
        {
            if (wok == null) return half;
            float radius = Mathf.Sqrt(position.x * position.x + position.z * position.z);
            return wok.SurfaceYAt(radius) + half;
        }

        float WokClampRadius
            => wok != null ? wok.RimRadius - _cfg.itemSize * 0.45f : _cfg.spreadRadiusBottom;

        /// <summary>
        /// 同层之间：太挤就推开，稍远就黏一把。
        /// 用层号分组而不是高度差——锅会随半径升高，同层物件的高度本来就参差，按高度分会串层。
        /// </summary>
        void Separate(float step)
        {
            float sep = _cfg.itemSize * separationScale;
            float coh = _cfg.itemSize * cohesionScale;

            for (int i = 0; i < _items.Count; i++)
            {
                ItemEntity a = _items[i];
                Vector3 pa = a.transform.position;
                for (int j = i + 1; j < _items.Count; j++)
                {
                    ItemEntity b = _items[j];
                    Vector3 pb = b.transform.position;
                    if (a.LayerIndex != b.LayerIndex) continue;

                    float dx = pb.x - pa.x;
                    float dz = pb.z - pa.z;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dist > coh || dist < 1e-4f) continue;

                    float nx = dx / dist;
                    float nz = dz / dist;
                    if (dist < sep)
                    {
                        float push = (sep - dist) / sep * separationForce * step;
                        a.Velocity -= new Vector3(nx * push, 0f, nz * push);
                        b.Velocity += new Vector3(nx * push, 0f, nz * push);
                    }
                    else
                    {
                        float t = 1f - (dist - sep) / (coh - sep);
                        float pull = cohesionForce * t * step;
                        a.Velocity += new Vector3(nx * pull, 0f, nz * pull);
                        b.Velocity -= new Vector3(nx * pull, 0f, nz * pull);
                    }
                }
            }
        }

        void ClampToPot()
        {
            float r = WokClampRadius;
            foreach (var item in _items)
            {
                // 锚点也要关在锅里，不然弹簧会一直把物件往锅外拽，边上就会一直抖
                Vector3 a = item.Anchor;
                float da = Mathf.Sqrt(a.x * a.x + a.z * a.z);
                if (da > r && da > 1e-4f)
                {
                    float k = r / da;
                    a.x *= k;
                    a.z *= k;
                    item.Anchor = a;
                }

                Vector3 p = item.transform.position;
                float d = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                if (d <= r || d < 1e-4f) continue;
                float kp = r / d;
                p.x *= kp;
                p.z *= kp;
                item.transform.position = p;
                item.Velocity = new Vector3(item.Velocity.x * -0.25f, item.Velocity.y, item.Velocity.z * -0.25f);
            }
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
            item.Velocity = Vector3.zero;
            item.Anchor = item.Origin;
            item.gameObject.SetActive(true);
            if (!_items.Contains(item)) _items.Add(item);
            RefreshBlocking();
        }

        void RefreshBlocking()
        {
            foreach (var item in _items) item.SetBlocked(IsBlocked(item));
        }

        /// <summary>拿走一个后，压在上方的往下沉一点。改锚点而不是动画，免得和物理打架。</summary>
        void SettleAbove(ItemEntity removed)
        {
            foreach (var item in _items)
            {
                if (item.transform.position.y <= removed.transform.position.y) continue;
                if (FlatDistance(item, removed) > _cfg.itemSize * 1.6f) continue;
                item.Anchor += Vector3.down * SettleDistance;
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
                layer[i].Anchor = spots[i];
                layer[i].transform.position = spots[i];
                layer[i].Velocity = Vector3.zero;
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
