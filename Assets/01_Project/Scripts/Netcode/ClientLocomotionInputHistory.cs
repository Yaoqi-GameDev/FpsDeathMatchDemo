using FpsDemo.Fps;
using UnityEngine;

namespace FpsDemo.Netcode
{
    /// <summary>
    /// 纯客户端 Owner：按 <see cref="PlayerLocomotionInput.ClientTick"/> 保存最近若干帧输入与当时 <c>Time.deltaTime</c>，供预测和解时从权威 tick 起重放。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClientLocomotionInputHistory : MonoBehaviour
    {
        [Tooltip("环形槽位数（会向上取为 2 的幂，至少 64）。")]
        [SerializeField] private int _capacityPow2 = 512;

        private struct HistorySlot
        {
            public uint Tick;
            public PlayerLocomotionInput Input;
            public float DeltaTime;
        }

        private HistorySlot[] _slots;
        private int _mask;

        private void Awake()
        {
            int c = Mathf.NextPowerOfTwo(Mathf.Clamp(_capacityPow2, 64, 2048));
            _slots = new HistorySlot[c];
            _mask = c - 1;
        }

        /// <summary>写入本帧将要发往服务器并与本地模拟一致的输入快照。</summary>
        public void Store(uint tick, in PlayerLocomotionInput frame, float deltaTime)
        {
            if (_slots == null)
                Awake();

            int i = (int)(tick & (uint)_mask);
            var copy = frame;
            copy.ClientTick = tick;
            _slots[i] = new HistorySlot
            {
                Tick = tick,
                Input = copy,
                DeltaTime = deltaTime
            };
        }

        public bool TryGet(uint tick, out PlayerLocomotionInput input, out float deltaTime)
        {
            input = default;
            deltaTime = 0f;
            if (_slots == null)
                return false;

            int i = (int)(tick & (uint)_mask);
            HistorySlot s = _slots[i];
            if (s.Tick != tick)
                return false;

            input = s.Input;
            deltaTime = s.DeltaTime;
            return true;
        }

        /// <summary>区间内每一 tick 是否都在缓冲里且未因环形覆盖而失效。</summary>
        public bool CanReplayRange(uint firstTickInclusive, uint lastTickInclusive)
        {
            if (firstTickInclusive > lastTickInclusive)
                return true;
            for (uint t = firstTickInclusive; t <= lastTickInclusive; t++)
            {
                if (!TryGet(t, out _, out _))
                    return false;
            }

            return true;
        }
    }
}
