using UnityEngine;

namespace CatchGoose
{
    /// <summary>
    /// 倾斜输入。真机走陀螺仪重力向量；PC 上鼠标模拟默认关闭——颠锅只由「颠锅按钮」触发。
    /// 对外只给两样东西：<see cref="Tilt"/> 当前倾斜、<see cref="ShakeEnergy"/> 抖动能量。
    /// </summary>
    public class TiltInput : MonoBehaviour
    {
        [Header("灵敏度")]
        [Range(0f, 2f), Tooltip("鼠标划动转成倾斜的强度（仅在 mouseTilt 打开时生效）")]
        public float mouseSensitivity = 1.1f;
        [Range(0f, 2f), Tooltip("陀螺仪转成倾斜的强度")]
        public float gyroSensitivity = 1.4f;
        [Tooltip("划动后回正的速度，越大越快稳住")]
        public float recenter = 4.5f;
        [Tooltip("倾斜平滑，越大跟得越紧")]
        public float smooth = 10f;
        [Tooltip("PC 上是否用鼠标划动来倾斜。默认关：颠锅只认按钮。")]
        public bool mouseTilt = false;
        [Tooltip("强制用鼠标，调试时勾上")]
        public bool forceMouse = false;

        [Header("视觉")]
        [Tooltip("跟着一起倾斜的物体（通常是相机），不填就不做视觉倾斜")]
        public Transform tiltVisual;
        [Tooltip("视觉最大倾角（度）")]
        public float maxVisualAngle = 7f;

        Vector2 _target;
        Vector2 _smooth;
        Vector2 _prevSmooth;
        Vector2 _lastMouse;
        float _energy;
        Quaternion _baseRotation = Quaternion.identity;

        /// <summary>已平滑的倾斜，x/z 分量范围约 [-1,1]。</summary>
        public Vector2 Tilt => _smooth;
        /// <summary>抖动能量：划得越快越高，对应原版「颠勺靠频率」。</summary>
        public float ShakeEnergy => _energy;
        public bool UsingGyro { get; private set; }

        void Start()
        {
            UsingGyro = !forceMouse && SystemInfo.supportsGyroscope;
            if (UsingGyro) Input.gyro.enabled = true;
            _lastMouse = Input.mousePosition;

            // 记住初始姿态。视觉倾斜必须是「在初始姿态上叠加」，
            // 否则倾斜归零时会把相机本身的俯角一起擦掉。
            if (tiltVisual != null) _baseRotation = tiltVisual.localRotation;
        }

        void Update()
        {
            Vector2 raw;
            if (UsingGyro) raw = ReadGyro();
            else if (mouseTilt) raw = ReadMouse();
            else raw = Vector2.zero;

            float k = 1f - Mathf.Exp(-smooth * Time.deltaTime);
            _smooth = Vector2.Lerp(_smooth, raw, k);

            float delta = (_smooth - _prevSmooth).magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
            _prevSmooth = _smooth;
            _energy = Mathf.Lerp(_energy, Mathf.Clamp01(delta * 0.2f), 1f - Mathf.Exp(-6f * Time.deltaTime));

            ApplyVisual();
        }

        Vector2 ReadGyro()
        {
            Vector3 g = Input.gyro.gravity;
            return Vector2.ClampMagnitude(new Vector2(g.x, g.y) * gyroSensitivity, 1f);
        }

        /// <summary>
        /// 鼠标：用「划动增量」而不是绝对位置。
        /// 这样点物件时鼠标不动、锅是稳的；快速划动才颠得起来，正好对应原版的频率手感。
        /// </summary>
        Vector2 ReadMouse()
        {
            Vector2 p = Input.mousePosition;
            Vector2 d = new Vector2((p.x - _lastMouse.x) / Mathf.Max(Screen.width, 1),
                                    (p.y - _lastMouse.y) / Mathf.Max(Screen.height, 1));
            _lastMouse = p;

            _target += d * mouseSensitivity * 14f;
            _target *= Mathf.Exp(-recenter * Time.deltaTime);
            return Vector2.ClampMagnitude(_target, 1f);
        }

        void ApplyVisual()
        {
            if (tiltVisual == null) return;
            float angleX = -_smooth.y * maxVisualAngle;
            float angleZ = _smooth.x * maxVisualAngle;
            tiltVisual.localRotation = _baseRotation * Quaternion.Euler(angleX, 0f, angleZ);
        }
    }
}
