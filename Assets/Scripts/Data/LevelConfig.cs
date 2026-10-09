using UnityEngine;

namespace CatchGoose
{
    /// <summary>
    /// 单关数值配置。难度 = 种类数 / 槽位数 × 堆叠层数 × 打散度。
    /// </summary>
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "CatchGoose/LevelConfig")]
    public class LevelConfig : ScriptableObject
    {
        [Header("规模")]
        [Tooltip("物体总数，必须是 3 的倍数")]
        public int totalItems = 36;
        [Tooltip("物体种类数，越大越难")]
        public int typeCount = 6;
        [Tooltip("堆叠层数，越深被压住的越多")]
        public int layers = 2;

        [Header("难度旋钮")]
        [Range(0f, 1f), Tooltip("同组三个物体在解锁序列上的打散程度，0 最友好")]
        public float scatter = 0f;
        [Tooltip("托盘槽位数，越小越难")]
        public int traySlots = 8;

        [Header("布局")]
        [Tooltip("底层半径，要能放下 totalItems/layers 个物体，否则会挤成一团")]
        public float potRadiusBottom = 3.0f;
        public float potRadiusTop = 2.2f;
        public float layerHeight = 0.5f;
        public float itemSize = 0.62f;

        [Header("道具（每关重置，不看广告）")]
        public int undoCount = 3;
        public int shuffleCount = 2;
        public int extractCount = 1;

        [Header("随机")]
        public bool useRandomSeed = true;
        public int seed = 1;

        public int GroupCount => totalItems / 3;

        /// <summary>难度比值，供调参时对齐手感。0.75 轻松，1.7 吃力。</summary>
        public float DifficultyRatio => typeCount / (float)traySlots;
    }
}
