using UnityEngine;

namespace CatchGoose
{
    /// <summary>场上的一个物件。色块阶段：颜色区分种类，形状辅助辨识。</summary>
    public class ItemEntity : MonoBehaviour
    {
        static readonly PrimitiveType[] Shapes =
        {
            PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Capsule, PrimitiveType.Cylinder
        };

        public int TypeId { get; private set; }
        public int LayerIndex { get; private set; }
        public bool IsBlocked { get; private set; }
        /// <summary>场上的原始位置，撤回和洗牌都要用它复位。</summary>
        public Vector3 Origin { get; set; }
        /// <summary>静止锚点，弹簧把物件往这儿拉；会跟着物件慢慢挪，所以颠过的布局不会自己弹回去。</summary>
        public Vector3 Anchor { get; set; }
        /// <summary>翻锅模拟用的速度。</summary>
        public Vector3 Velocity { get; set; }
        /// <summary>这一层的地面高度，抛起来后落回这里。</summary>
        public float FloorY { get; set; }

        Renderer _renderer;
        Collider _collider;
        Color _baseColor;

        public void Init(int typeId, int layerIndex, float size)
        {
            TypeId = typeId;
            LayerIndex = layerIndex;
            Origin = transform.position;
            Anchor = transform.position;
            Velocity = Vector3.zero;
            _renderer = GetComponent<Renderer>();
            _collider = GetComponent<Collider>();
            _baseColor = TypeColor(typeId);
            _renderer.material.color = _baseColor;
            transform.localScale = Vector3.one * size;
            SetBlocked(false);
        }

        /// <summary>进了托盘就别再被射线点到。</summary>
        public void SetPickable(bool value)
        {
            if (_collider != null) _collider.enabled = value;
        }

        /// <summary>黄金角分散色相，相邻种类颜色差异最大化。</summary>
        public static Color TypeColor(int typeId)
            => Color.HSVToRGB((typeId * 0.618034f) % 1f, 0.62f, 0.95f);

        public static PrimitiveType ShapeFor(int typeId) => Shapes[typeId % Shapes.Length];

        public void SetBlocked(bool blocked)
        {
            IsBlocked = blocked;
            if (_renderer == null) return;
            _renderer.material.color = blocked ? _baseColor * 0.42f : _baseColor;
        }

        public void Nudge(float amount)
        {
            if (_renderer == null) return;
            _renderer.material.color = _baseColor * (1f + amount);
        }
    }
}
