using System;
using UIFramework;

namespace FpsDemo.UI
{
    /// <summary>结算窗打开时传入的文案与窗口行为。</summary>
    [Serializable]
    public class EndGameWindowProperties : WindowProperties
    {
        public string SummaryText;

        public EndGameWindowProperties()
        {
        }

        public EndGameWindowProperties(string summaryText)
            : base(WindowPriority.ForceForeground, hideOnForegroundLost: false, suppressPrefabProperties: true)
        {
            SummaryText = summaryText;
            IsPopup = true;
        }
    }
}
