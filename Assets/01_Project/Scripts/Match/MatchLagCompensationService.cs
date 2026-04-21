using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FpsDemo.Match
{
    /// <summary>
    /// 仅服务器：在 <see cref="LateUpdate"/> 采样 <see cref="MatchParticipant"/> 根 Transform（晚于默认 <see cref="FpsPlayerMotor"/> 的 <c>Update</c>），
    /// 供 Hitscan 按 <c>Time.timeAsDouble - rewind</c> 插值回溯（环形缓冲）。
    /// </summary>
    [DefaultExecutionOrder(80)]
    public sealed class MatchLagCompensationService : MonoBehaviour
    {
        public static MatchLagCompensationService Instance { get; private set; }

        [Tooltip("环形缓冲长度；约 bufferLength×帧间隔 应大于最大 rewind + 余量。")]
        [SerializeField] private int _bufferLength = 96;

        private struct ParticipantPose
        {
            public int ParticipantId;
            public Vector3 RootPosition;
            public Quaternion RootRotation;
            public bool Valid;
        }

        private struct TimeSample
        {
            public double Time;
            public ParticipantPose[] Poses;
            public int PoseCount;
        }

        private TimeSample[] _ring;
        private int _writeIndex;
        private int _filledCount;
        private ParticipantPose[] _scratch;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            int len = Mathf.Clamp(_bufferLength, 16, 256);
            _ring = new TimeSample[len];
            _scratch = new ParticipantPose[32];
            for (int i = 0; i < len; i++)
                _ring[i].Poses = new ParticipantPose[32];
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
                return;

            IReadOnlyList<MatchParticipant> list = MatchParticipant.ActiveParticipants;
            int count = 0;
            for (int i = 0; i < list.Count && count < _scratch.Length; i++)
            {
                MatchParticipant mp = list[i];
                if (mp == null || mp.Root == null)
                    continue;

                Transform root = mp.Root.transform;
                _scratch[count++] = new ParticipantPose
                {
                    ParticipantId = mp.ParticipantId,
                    RootPosition = root.position,
                    RootRotation = root.rotation,
                    Valid = true
                };
            }

            ref TimeSample slot = ref _ring[_writeIndex];
            slot.Time = Time.timeAsDouble;
            slot.PoseCount = count;
            for (int i = 0; i < count; i++)
                slot.Poses[i] = _scratch[i];

            _writeIndex = (_writeIndex + 1) % _ring.Length;
            _filledCount = Mathf.Min(_filledCount + 1, _ring.Length);
        }

        /// <summary>在 <paramref name="targetTime"/> 附近插值出该参战者根姿态；无足够历史则返回 false。</summary>
        public bool TryGetInterpolatedPose(
            int participantId,
            double targetTime,
            out Vector3 rootPos,
            out Quaternion rootRot)
        {
            rootPos = default;
            rootRot = Quaternion.identity;

            if (_filledCount < 1)
                return false;

            int len = _ring.Length;
            int newest = (_writeIndex - 1 + len) % len;
            int oldest = (_writeIndex - _filledCount + len) % len;

            if (targetTime >= _ring[newest].Time)
                return TryCopyPoseAtFrame(participantId, newest, out rootPos, out rootRot);

            if (targetTime <= _ring[oldest].Time)
                return TryCopyPoseAtFrame(participantId, oldest, out rootPos, out rootRot);

            int newer = newest;
            for (int i = 0; i < _filledCount - 1; i++)
            {
                int older = (newer - 1 + len) % len;
                double tNew = _ring[newer].Time;
                double tOld = _ring[older].Time;
                if (tOld <= targetTime && targetTime <= tNew)
                {
                    float u = tNew > tOld ? (float)((targetTime - tOld) / (tNew - tOld)) : 0f;
                    u = Mathf.Clamp01(u);
                    if (!FindParticipantInFrame(participantId, older, out ParticipantPose p0))
                        return false;
                    if (!FindParticipantInFrame(participantId, newer, out ParticipantPose p1))
                        return false;

                    rootPos = Vector3.Lerp(p0.RootPosition, p1.RootPosition, u);
                    rootRot = Quaternion.Slerp(p0.RootRotation, p1.RootRotation, u);
                    return true;
                }

                newer = older;
            }

            return false;
        }

        private bool TryCopyPoseAtFrame(int participantId, int frameIndex, out Vector3 rootPos, out Quaternion rootRot)
        {
            rootPos = default;
            rootRot = default;
            if (!FindParticipantInFrame(participantId, frameIndex, out ParticipantPose p))
                return false;
            rootPos = p.RootPosition;
            rootRot = p.RootRotation;
            return true;
        }

        private bool FindParticipantInFrame(int participantId, int frameIndex, out ParticipantPose pose)
        {
            pose = default;
            ref TimeSample s = ref _ring[frameIndex];
            for (int i = 0; i < s.PoseCount; i++)
            {
                if (s.Poses[i].ParticipantId == participantId && s.Poses[i].Valid)
                {
                    pose = s.Poses[i];
                    return true;
                }
            }

            return false;
        }
    }
}
