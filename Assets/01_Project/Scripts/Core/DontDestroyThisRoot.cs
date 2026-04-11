using UnityEngine;

namespace FpsDemo.Core
{
    /// <summary>
    /// 挂在**场景根**空物体上（如 <c>--DDOL--</c>），仅对本物体调用 <see cref="DontDestroyOnLoad"/>。
    /// 子物体上的脚本不可对子节点单独 DDOL；整棵子树随根一起保留。
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class DontDestroyThisRoot : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
