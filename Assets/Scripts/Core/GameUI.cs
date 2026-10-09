using UnityEngine;
using UnityEngine.UI;

namespace CatchGoose
{
    /// <summary>纯信息展示 + 按钮转发，逻辑一律交给 GameManager。</summary>
    public class GameUI : MonoBehaviour
    {
        [SerializeField] Text levelText;
        [SerializeField] Text remainText;
        [SerializeField] Text trayText;
        [SerializeField] Button undoButton;
        [SerializeField] Button shuffleButton;
        [SerializeField] Button extractButton;
        [SerializeField] GameObject resultPanel;
        [SerializeField] Text resultText;
        [SerializeField] Button nextButton;
        [SerializeField] Button retryButton;

        Text _undoLabel;
        Text _shuffleLabel;
        Text _extractLabel;

        public void Bind(GameManager game)
        {
            _undoLabel = LabelOf(undoButton);
            _shuffleLabel = LabelOf(shuffleButton);
            _extractLabel = LabelOf(extractButton);
            if (undoButton != null) undoButton.onClick.AddListener(game.UseUndo);
            if (shuffleButton != null) shuffleButton.onClick.AddListener(game.UseShuffle);
            if (extractButton != null) extractButton.onClick.AddListener(game.UseExtract);
            if (nextButton != null) nextButton.onClick.AddListener(game.NextLevel);
            if (retryButton != null) retryButton.onClick.AddListener(game.RetryLevel);
            HideResult();
        }

        public void Refresh(int level, int remain, int trayCount, int trayCap, int undo, int shuffle, int extract)
        {
            Set(levelText, "第 " + level + " 关");
            Set(remainText, "锅里剩 " + remain);
            Set(trayText, "托盘 " + trayCount + " / " + trayCap);
            Set(_undoLabel, "撤回 " + undo);
            Set(_shuffleLabel, "洗牌 " + shuffle);
            Set(_extractLabel, "清盘 " + extract);
        }

        public void ShowResult(bool win, int level)
        {
            if (resultPanel == null) return;
            resultPanel.SetActive(true);
            Set(resultText, win ? "第 " + level + " 关，过了" : "托盘塞满了，再来一把");
            if (nextButton != null) nextButton.gameObject.SetActive(win);
        }

        public void HideResult()
        {
            if (resultPanel != null) resultPanel.SetActive(false);
        }

        static void Set(Text label, string value)
        {
            if (label != null) label.text = value;
        }

        static Text LabelOf(Button button)
            => button != null ? button.GetComponentInChildren<Text>() : null;
    }
}
