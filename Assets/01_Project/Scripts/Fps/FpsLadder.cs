using UnityEngine;

namespace FpsDemo.Fps
{
    /// <summary>
    /// 挂在梯子物体上：同一物体上需有 <see cref="Collider"/>，并勾选 <b>Is Trigger</b>。
    /// 物体的 <b>Transform.up</b> 为沿梯「向上」攀爬方向；请在场景里旋转使 up 指向梯顶。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class FpsLadder : MonoBehaviour
    {
        [SerializeField] private float _climbSpeed = 3.2f;

        /// <summary>沿梯上下移动速度（米/秒）。</summary>
        public float ClimbSpeed => _climbSpeed;

        /// <summary>世界空间：沿梯向上的单位向量。</summary>
        public Vector3 WorldUp => transform.up.normalized;

        /// <summary>世界空间：梯面侧向（左右平移），用于 A/D。</summary>
        public Vector3 WorldRight => transform.right.normalized;

        private void OnTriggerEnter(Collider other)
        {
            var motor = other.GetComponent<FpsPlayerMotor>();
            if (motor != null)
                motor.NotifyLadderEnter(this);
        }

        private void OnTriggerExit(Collider other)
        {
            var motor = other.GetComponent<FpsPlayerMotor>();
            if (motor != null)
                motor.NotifyLadderExit(this);
        }
    }
}
