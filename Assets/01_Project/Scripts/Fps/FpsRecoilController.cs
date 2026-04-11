using FpsDemo.Combat;
using FpsDemo.Data;
using UnityEngine;

namespace FpsDemo.Fps
{
    /// <summary>
    /// 后座：订阅 <see cref="FpsHitscanWeapon.ShotFired"/>，按当前武器的 <see cref="HitscanWeaponConfig"/> 累加俯仰/偏航，并在 <see cref="LateUpdate"/> 中向 0 恢复。
    /// <b>_recoilPivot</b> 通常为 <b>Main Camera</b> 的 <see cref="Transform"/>（与 <see cref="FpsPlayerLook"/> 在 <b>CameraPivot</b> 上的俯仰叠加）；勿改场景武器挂点层级。
    /// </summary>
    [DefaultExecutionOrder(-45)]
    public sealed class FpsRecoilController : MonoBehaviour
    {
        [SerializeField] private FpsHitscanWeapon _weapon;
        [Tooltip("通常为本物体 Transform；与相机、武器挂点同父便于同抬枪。")]
        [SerializeField] private Transform _recoilPivot;

        private float _pitchAccum;
        private float _yawAccum;

        private void Awake()
        {
            if (_recoilPivot == null)
                _recoilPivot = transform;
        }

        private void OnEnable()
        {
            if (_weapon != null)
                _weapon.ShotFired += OnShotFired;
        }

        private void OnDisable()
        {
            if (_weapon != null)
                _weapon.ShotFired -= OnShotFired;
        }

        private void OnShotFired()
        {
            if (_weapon == null)
                return;

            HitscanWeaponConfig cfg = _weapon.ActiveConfig;
            if (cfg == null)
                return;

            _pitchAccum += cfg.RecoilVerticalPerShot;
            float half = cfg.RecoilYawRandomHalf;
            _yawAccum += Random.Range(-half, half);
        }

        private void LateUpdate()
        {
            if (_weapon == null || _recoilPivot == null)
                return;

            HitscanWeaponConfig cfg = _weapon.ActiveConfig;
            if (cfg == null)
                return;

            float dt = Time.deltaTime;
            float rec = Mathf.Max(0.01f, cfg.RecoilRecoveryDegreesPerSecond);
            _pitchAccum = Mathf.MoveTowards(_pitchAccum, 0f, rec * dt);
            _yawAccum = Mathf.MoveTowards(_yawAccum, 0f, rec * dt);

            // 正 pitch 累加 → 枪往上跳（与 FpsPlayerLook 中「抬头」的俯仰约定一致：负欧拉 X 常对应抬头）
            _recoilPivot.localRotation = Quaternion.Euler(-_pitchAccum, _yawAccum, 0f);
        }
    }
}
