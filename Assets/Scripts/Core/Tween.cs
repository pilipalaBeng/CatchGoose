using System.Collections;
using UnityEngine;

namespace CatchGoose
{
    /// <summary>极简补间，免得为几段动画拖一个 DOTween 进来。</summary>
    public static class Tween
    {
        public static IEnumerator MoveTo(Transform t, Vector3 to, float duration)
        {
            if (t == null) yield break;
            Vector3 from = t.position;
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                if (t == null) yield break;
                t.position = Vector3.Lerp(from, to, EaseOut(Mathf.Clamp01(time / duration)));
                yield return null;
            }
            if (t != null) t.position = to;
        }

        /// <summary>抛物线飞入，物体进托盘时用，比直线有分量。</summary>
        public static IEnumerator MoveArc(Transform t, Vector3 to, float arcHeight, float duration)
        {
            if (t == null) yield break;
            Vector3 from = t.position;
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                if (t == null) yield break;
                float k = Mathf.Clamp01(time / duration);
                Vector3 flat = Vector3.Lerp(from, to, EaseOut(k));
                flat.y += Mathf.Sin(k * Mathf.PI) * arcHeight;
                t.position = flat;
                yield return null;
            }
            if (t != null) t.position = to;
        }

        public static IEnumerator Sink(Transform t, float distance, float duration)
        {
            Vector3 from = t.position;
            Vector3 to = from + Vector3.down * distance;
            yield return MoveTo(t, to, duration);
        }

        static float EaseOut(float k) => 1f - Mathf.Pow(1f - k, 3f);
    }
}
