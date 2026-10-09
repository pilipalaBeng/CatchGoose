using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CatchGoose
{
    /// <summary>流程总控：开局、拾取、三消结算、道具、胜负。</summary>
    public class GameManager : MonoBehaviour
    {
        const float FlyDuration = 0.34f;
        const float FlyArc = 1.3f;

        [SerializeField] Board board;
        [SerializeField] Tray tray;
        [SerializeField] GameUI ui;
        [SerializeField] LevelConfig[] levels;

        readonly List<ItemEntity> _undoStack = new List<ItemEntity>();
        LevelConfig _cfg;
        int _levelIndex;
        int _undoLeft;
        int _shuffleLeft;
        int _extractLeft;
        bool _finished;

        void Start()
        {
            if (ui != null) ui.Bind(this);
            StartLevel(0);
        }

        void Update()
        {
            if (_finished || _cfg == null) return;
            if (!Input.GetMouseButtonDown(0)) return;
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            var item = board.Raycast(ray);
            if (item != null) OnPick(item);
        }

        public void StartLevel(int index)
        {
            _levelIndex = Mathf.Clamp(index, 0, levels.Length - 1);
            _cfg = levels[_levelIndex];
            _finished = false;
            _undoStack.Clear();

            tray.Setup(_cfg.traySlots);
            tray.Clear();
            board.Build(_cfg, SeedFor(_cfg));

            _undoLeft = _cfg.undoCount;
            _shuffleLeft = _cfg.shuffleCount;
            _extractLeft = _cfg.extractCount;
            RefreshUI();
            if (ui != null) ui.HideResult();
        }

        public void NextLevel() => StartLevel(_levelIndex + 1 >= levels.Length ? 0 : _levelIndex + 1);

        public void RetryLevel() => StartLevel(_levelIndex);

        static int SeedFor(LevelConfig cfg)
            => cfg.useRandomSeed ? Random.Range(1, 999999) : cfg.seed;

        void OnPick(ItemEntity item)
        {
            if (item.IsBlocked)
            {
                StartCoroutine(Nudge(item));
                return;
            }
            board.Take(item);
            item.SetPickable(false);

            if (!tray.TryPlace(item, out var cleared))
            {
                board.PutBack(item);
                item.SetPickable(true);
                Lose();
                return;
            }

            StartCoroutine(tray.FlyInRoutine(item, FlyArc, FlyDuration));
            if (cleared != null)
            {
                StartCoroutine(ClearAfterFly(cleared));
            }
            else
            {
                _undoStack.Add(item);
                if (tray.IsFull)
                {
                    Lose();
                    return;
                }
            }

            RefreshUI();
            CheckWin();
        }

        /// <summary>等飞进托盘再消，否则消除动画会跟飞入补间抢同一个 transform。</summary>
        IEnumerator ClearAfterFly(List<ItemEntity> cleared)
        {
            yield return new WaitForSeconds(FlyDuration);
            HandleCleared(cleared);
        }

        void HandleCleared(List<ItemEntity> cleared)
        {
            foreach (var one in cleared)
            {
                if (one == null) continue;
                _undoStack.Remove(one);
                StartCoroutine(PopOut(one));
            }
        }

        void CheckWin()
        {
            if (board.Remaining > 0) return;
            if (tray.Count > 0)
            {
                Lose();
                return;
            }
            _finished = true;
            if (ui != null) ui.ShowResult(true, _levelIndex + 1);
        }

        void Lose()
        {
            _finished = true;
            if (ui != null) ui.ShowResult(false, _levelIndex + 1);
        }

        public void UseUndo()
        {
            if (_finished || _undoLeft <= 0 || _undoStack.Count == 0) return;
            var item = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            tray.Remove(item);
            RestoreToBoard(item);
            _undoLeft--;
            RefreshUI();
        }

        public void UseShuffle()
        {
            if (_finished || _shuffleLeft <= 0) return;
            board.Shuffle();
            _shuffleLeft--;
            RefreshUI();
        }

        /// <summary>清盘：托盘里的全退回锅里，救命用，每关次数有限。</summary>
        public void UseExtract()
        {
            if (_finished || _extractLeft <= 0 || tray.Count == 0) return;
            foreach (var item in tray.TakeAll()) RestoreToBoard(item);
            _undoStack.Clear();
            _extractLeft--;
            RefreshUI();
        }

        void RestoreToBoard(ItemEntity item)
        {
            board.PutBack(item);
            item.SetPickable(true);
            item.transform.localScale = Vector3.one * _cfg.itemSize;
        }

        void RefreshUI()
        {
            if (ui == null) return;
            ui.Refresh(_levelIndex + 1, board.Remaining, tray.Count, _cfg.traySlots,
                _undoLeft, _shuffleLeft, _extractLeft);
        }

        IEnumerator PopOut(ItemEntity item)
        {
            float time = 0f;
            Vector3 start = item.transform.localScale;
            while (time < 0.18f)
            {
                time += Time.deltaTime;
                if (item == null) yield break;
                item.transform.localScale = start * (1f - Mathf.Clamp01(time / 0.18f));
                yield return null;
            }
            if (item != null) Destroy(item.gameObject);
        }

        IEnumerator Nudge(ItemEntity item)
        {
            item.Nudge(0.35f);
            yield return new WaitForSeconds(0.12f);
            if (item != null) item.SetBlocked(item.IsBlocked);
        }
    }
}
