using UIFramework;
using UnityEngine;
using UnityEngine.UI;

namespace FpsDemo.UI
{
    /// <summary>
    /// Full-screen hurt flash as a Prioritary panel (above main HUD panel layer).
    /// Prefab / ScreenId: <c>HurtOverlayPanelController</c>. Hosts <see cref="FpsPlayerHurtOverlayFeedback"/>.
    /// </summary>
    public sealed class HurtOverlayPanelController : PanelController
    {
        public const string ScreenId = "HurtOverlayPanelController";

        [SerializeField] private Image _overlayImage;
        [SerializeField] private FpsPlayerHurtOverlayFeedback _feedback;

        protected override void OnPropertiesSet()
        {
            if (_overlayImage != null)
                _overlayImage.raycastTarget = false;
            if (_feedback == null)
                _feedback = GetComponent<FpsPlayerHurtOverlayFeedback>();
        }
    }
}
