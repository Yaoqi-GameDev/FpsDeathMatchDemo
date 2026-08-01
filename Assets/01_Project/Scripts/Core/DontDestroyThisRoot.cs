using UnityEngine;

namespace FpsDemo.Core
{
    /// <summary>
    /// 挂在场景根（如 <c>--DDOL--</c>）上，对本物体调用 <see cref="DontDestroyOnLoad"/>。
    /// 名为 <c>--DDOL--</c> 的根全局只保留一个：后进场景的重复根会把子物体合并进来并自毁，避免回大厅堆多个 DDOL。
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class DontDestroyThisRoot : MonoBehaviour
    {
        public const string AppDdolRootName = "--DDOL--";

        /// <summary>全局应用 DDOL 根（名称为 <see cref="AppDdolRootName"/>）。</summary>
        public static DontDestroyThisRoot AppRoot { get; private set; }

        private void Awake()
        {
            if (gameObject.name == AppDdolRootName)
            {
                if (AppRoot != null && AppRoot != this)
                {
                    MergeChildrenInto(AppRoot.transform);
                    Destroy(gameObject);
                    return;
                }

                AppRoot = this;
            }

            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (AppRoot == this)
                AppRoot = null;
        }

        private void MergeChildrenInto(Transform primary)
        {
            if (primary == null)
                return;

            while (transform.childCount > 0)
            {
                var child = transform.GetChild(0);
                child.SetParent(primary, true);
            }
        }
    }
}
