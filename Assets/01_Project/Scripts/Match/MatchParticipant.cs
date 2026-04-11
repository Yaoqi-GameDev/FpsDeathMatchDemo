using System.Collections.Generic;
using UnityEngine;

namespace FpsDemo.Match
{
    /// <summary>
    /// 挂在<strong>参战单位根物体</strong>上（与 <c>Player</c>、人机根同级）；提供显示名与本地玩家标记，并在启用时进入全局列表供 <see cref="MatchManager"/> 登记比分。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchParticipant : MonoBehaviour
    {
        private static readonly List<MatchParticipant> Active = new List<MatchParticipant>(16);
        private static int _nextId = 1;

        [SerializeField] private string _displayName = "Player";
        [SerializeField] private bool _isLocalPlayer;

        /// <summary>当前场上已启用的参战者（启用顺序，仅供开局快照；勿在运行时频繁依赖顺序）。</summary>
        public static IReadOnlyList<MatchParticipant> ActiveParticipants => Active;

        public int ParticipantId { get; private set; }
        public string DisplayName => _displayName;
        public bool IsLocalPlayer => _isLocalPlayer;
        public GameObject Root => gameObject;

        private void OnEnable()
        {
            if (ParticipantId == 0)
                ParticipantId = _nextId++;
            if (!Active.Contains(this))
                Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_displayName))
                _displayName = gameObject.name;
        }
#endif
    }
}
