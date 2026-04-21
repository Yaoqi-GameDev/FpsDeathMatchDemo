using System;
using System.Collections.Generic;
using FpsDemo.Data;
using FpsDemo.Match;
using UnityEngine;

namespace FpsDemo.Combat
{
    /// <summary>
    /// 服务器：将<strong>目标参战者</strong>根物体临时移到 <see cref="MatchLagCompensationService"/> 插值出的历史位姿，
    /// 再调用 <see cref="HitscanShotResolver.Resolve"/>；结束后还原。开枪者自身不移动。
    /// </summary>
    public static class HitscanLagCompensationResolver
    {
        private struct SavedRoot
        {
            public Transform Root;
            public Vector3 WorldPosition;
            public Quaternion WorldRotation;
            public CharacterController Controller;
            public bool ControllerWasEnabled;
        }

        public static bool TryResolve(
            Ray ray,
            float maxRange,
            LayerMask hitLayers,
            Transform instigatorWeaponTransform,
            float damagePerShot,
            BodyDamageMultiplierConfig bodyDamageConfig,
            bool applyDamage,
            float rewindSeconds,
            out bool hitDamageable,
            out ShotHitInfo shotInfo)
        {
            hitDamageable = false;
            shotInfo = default;

            MatchLagCompensationService svc = MatchLagCompensationService.Instance;
            if (svc == null)
                return false;

            Health sourceHealth = instigatorWeaponTransform.GetComponent<Health>()
                ?? instigatorWeaponTransform.GetComponentInParent<Health>(true);

            double targetTime = Time.timeAsDouble - Math.Max(0.0, rewindSeconds);
            var saved = new List<SavedRoot>(8);

            try
            {
                foreach (MatchParticipant mp in MatchParticipant.ActiveParticipants)
                {
                    if (mp == null || mp.Root == null)
                        continue;

                    Health targetHealth = mp.Root.GetComponentInChildren<Health>(true);
                    if (targetHealth == null)
                        continue;

                    if (IsSameShooterAndTarget(targetHealth, sourceHealth))
                        continue;

                    Transform root = mp.Root.transform;
                    CharacterController cc = root.GetComponent<CharacterController>();

                    saved.Add(new SavedRoot
                    {
                        Root = root,
                        WorldPosition = root.position,
                        WorldRotation = root.rotation,
                        Controller = cc,
                        ControllerWasEnabled = cc != null && cc.enabled
                    });

                    if (cc != null)
                        cc.enabled = false;

                    if (svc.TryGetInterpolatedPose(mp.ParticipantId, targetTime, out Vector3 p, out Quaternion q))
                        root.SetPositionAndRotation(p, q);
                }

                Physics.SyncTransforms();

                HitscanShotResolver.Resolve(
                    ray,
                    maxRange,
                    hitLayers,
                    instigatorWeaponTransform,
                    damagePerShot,
                    bodyDamageConfig,
                    applyDamage,
                    out hitDamageable,
                    out shotInfo);

                return true;
            }
            finally
            {
                for (int i = saved.Count - 1; i >= 0; i--)
                {
                    SavedRoot s = saved[i];
                    if (s.Root != null)
                        s.Root.SetPositionAndRotation(s.WorldPosition, s.WorldRotation);
                    if (s.Controller != null)
                        s.Controller.enabled = s.ControllerWasEnabled;
                }

                Physics.SyncTransforms();
            }
        }

        private static bool IsSameShooterAndTarget(Health target, Health shooter)
        {
            if (shooter == null)
                return false;
            return target == shooter;
        }
    }
}
