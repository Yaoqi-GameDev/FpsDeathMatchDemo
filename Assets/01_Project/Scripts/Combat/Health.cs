using System;
using UnityEngine;

namespace FpsDemo.Combat
{
    /// <summary>
    /// 生命值：挂在带 <see cref="Collider"/> 的物体上；射线命中后由 <see cref="FpsHitscanWeapon"/> 调用
    /// <see cref="IDamageable"/>。每次扣血触发 <see cref="Damaged"/>；致死时再发布 <see cref="Died"/> 与 <see cref="CombatKillBus"/>。
    /// </summary>
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private bool _destroyGameObjectWhenDead = true;

        /// <summary>本物体死亡时触发（先于 <see cref="Destroy"/>）；订阅者勿阻塞主线程。</summary>
        public event Action<KillReport> Died;

        /// <summary>
        /// 每次成功扣血后触发（含致死一击）；参数为本次伤害量、来源（可为 <c>null</c>）。
        /// 在 <see cref="Died"/> 之前触发；仅本地表现（受击 UI/音效等）可订阅，勿阻塞主线程。
        /// </summary>
        public event Action<float, GameObject> Damaged;

        public float Current { get; private set; }
        public float Max => _maxHealth;
        public bool IsDead { get; private set; }

        /// <summary>
        /// 联机客户端：由 <see cref="FpsDemo.Netcode.NetworkHealthBridge"/> 根据服务器 <c>NetworkVariable</c> 写入；
        /// 会触发 <see cref="Damaged"/>（受击表现）与必要时 <see cref="Died"/>（本地死亡流程），<strong>不</strong>调用 <see cref="CombatKillBus"/>（击杀仅服务器权威发布）。
        /// </summary>
        public void ApplyMirrorFromNetwork(float newCurrent)
        {
            float clamped = Mathf.Clamp(newCurrent, 0f, _maxHealth);
            if (Mathf.Approximately(clamped, Current) && (clamped > 0f || IsDead))
                return;

            float loss = Current - clamped;
            if (loss > 0f)
                Damaged?.Invoke(loss, null);

            Current = clamped;

            if (Current > 0f)
            {
                IsDead = false;
                return;
            }

            Current = 0f;
            if (IsDead)
                return;

            IsDead = true;
            var report = new KillReport(gameObject, null);
            Died?.Invoke(report);
        }

        private void Awake()
        {
            Current = _maxHealth;
        }

        public void ApplyDamage(float amount)
        {
            ApplyDamage(amount, null);
        }

        public void ApplyDamage(float amount, GameObject instigator)
        {
            if (IsDead || amount <= 0f)
                return;

            Current -= amount;
            Damaged?.Invoke(amount, instigator);

            if (Current > 0f)
                return;

            Current = 0f;
            IsDead = true;

            var report = new KillReport(gameObject, instigator);
            CombatKillBus.Publish(report);
            Died?.Invoke(report);

            if (_destroyGameObjectWhenDead)
                Destroy(gameObject);
        }

        /// <summary>复活为满血（用于测试假人重生等，不销毁玩家的流程）。</summary>
        public void ReviveFull()
        {
            Current = _maxHealth;
            IsDead = false;
        }

        /// <summary>玩家/假人复活前调用：避免死亡时销毁物体。</summary>
        public void SetDestroyOnDeath(bool destroy)
        {
            _destroyGameObjectWhenDead = destroy;
        }

        /// <summary>编辑器或调试：重置为满血。</summary>
        [ContextMenu("Combat/Reset Health")]
        private void ContextResetHealth()
        {
            Current = _maxHealth;
            IsDead = false;
        }
    }
}
