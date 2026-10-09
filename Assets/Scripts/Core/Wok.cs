using System;
using UnityEngine;

namespace CatchGoose
{
    /// <summary>
    /// 锅的几何形状。建模、布点、物理三处共用这一份，改参数就三边同时对齐。
    /// 内壁是一个旋转抛物面：锅心最低，往锅边一路升高。
    /// </summary>
    [Serializable]
    public struct WokShape
    {
        [Tooltip("锅口半径")]
        public float rimRadius;
        [Tooltip("锅深：锅口到锅底（锅心）的落差")]
        public float depth;
        [Tooltip("锅口所在的世界 Y")]
        public float rimY;
        [Tooltip("壁形指数。2=旋转抛物面（均匀弧），3=锅底更平、锅沿更陡，更接近真锅")]
        public float curvePower;

        /// <summary>给定水平半径，返回锅内壁的高度（世界 Y）。</summary>
        public float SurfaceY(float radius)
        {
            float k = Mathf.Clamp01(radius / Mathf.Max(rimRadius, 0.001f));
            return rimY - depth * (1f - Mathf.Pow(k, Mathf.Max(curvePower, 1f)));
        }

        public static WokShape Default => new WokShape
        {
            rimRadius = 4.1f,
            depth = 1.8f,
            rimY = 0f,
            curvePower = 2f
        };
    }

    /// <summary>锅。挂着形状参数，Board 和出题器都从这里取，保证「看到的锅」就是「物理的锅」。</summary>
    public class Wok : MonoBehaviour
    {
        public WokShape shape = WokShape.Default;

        public float RimRadius => shape.rimRadius;
        public float Depth => shape.depth;

        public float SurfaceYAt(float radius) => shape.SurfaceY(radius);

        /// <summary>物件在锅里静止时，中心落在的高度。</summary>
        public float RestCenterY(float radius, float itemSize)
            => SurfaceYAt(radius) + itemSize * 0.5f;
    }
}
