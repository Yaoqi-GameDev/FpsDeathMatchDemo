using UnityEngine;

namespace FpsDemo.Fps
{
    /// <summary>
    /// 与 <see cref="Input.GetMouseButton"/> 的索引一致，Inspector 以下拉显示，避免写 0/1/2。
    /// </summary>
    public enum FpsMouseButton
    {
        Left = 0,
        Right = 1,
        Middle = 2
    }

    /// <summary>
    /// 只负责从 Legacy Input Manager 读取输入，并暴露给其它脚本。
    /// 含移动、视角、战斗（开火/换弹）等；以后要换 Input System 时，主要改这个类即可。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class FpsInput : MonoBehaviour
    {
        [Header("鼠标（使用 Project Settings → Input Manager 里的 Mouse X / Mouse Y）")]
        [SerializeField] private float _mouseSensitivity = 2f;
        [SerializeField] private bool _invertMouseY;

        [Header("按键（名称需与 Input Manager 一致；默认 WASD / Space / Shift / Ctrl）")]
        [SerializeField] private string _horizontalAxis = "Horizontal";
        [SerializeField] private string _verticalAxis = "Vertical";
        [SerializeField] private string _mouseXAxis = "Mouse X";
        [SerializeField] private string _mouseYAxis = "Mouse Y";
        [SerializeField] private KeyCode _jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode _sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode _crouchKey = KeyCode.LeftControl;
        [SerializeField] private KeyCode _interactKey = KeyCode.F;

        [Header("战斗（Hitscan / 换弹等由其它脚本读取）")]
        [SerializeField] private FpsMouseButton _fireMouseButton = FpsMouseButton.Left;
        [SerializeField] private KeyCode _reloadKey = KeyCode.R;
        [Tooltip("切到武器槽 0（多武器时由 FpsHitscanWeapon 读取）。")]
        [SerializeField] private KeyCode _weaponSlot1Key = KeyCode.Alpha1;
        [Tooltip("切到武器槽 1。")]
        [SerializeField] private KeyCode _weaponSlot2Key = KeyCode.Alpha2;

        /// <summary>平面移动意图：x 右为正，z 前为正（对应 CharacterController 常用约定）。</summary>
        public Vector2 MoveAxes { get; private set; }

        /// <summary>鼠标本帧位移（已乘灵敏度）；用于视角旋转。</summary>
        public Vector2 LookDelta { get; private set; }

        public bool JumpPressedThisFrame { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool CrouchHeld { get; private set; }
        /// <summary>本帧刚按下蹲键（用于滑铲等边沿检测）。</summary>
        public bool CrouchPressedThisFrame { get; private set; }
        /// <summary>交互键（上梯等）本帧按下。</summary>
        public bool InteractPressedThisFrame { get; private set; }

        /// <summary>开火键按住（连发用）。</summary>
        public bool FireHeld { get; private set; }

        /// <summary>开火键本帧刚按下。</summary>
        public bool FirePressedThisFrame { get; private set; }

        /// <summary>换弹本帧刚按下。</summary>
        public bool ReloadPressedThisFrame { get; private set; }

        /// <summary>武器槽 0（如主武器）本帧刚按下。</summary>
        public bool WeaponSlot1PressedThisFrame { get; private set; }

        /// <summary>武器槽 1（如副武器）本帧刚按下。</summary>
        public bool WeaponSlot2PressedThisFrame { get; private set; }

        /// <summary>
        /// 为 <c>false</c> 时屏蔽移动、跳跃、蹲、交互、开火、换弹、切枪；<b>仍更新</b> <see cref="LookDelta"/>（便于死亡后只转视角）。
        /// </summary>
        public bool GameplayInputEnabled { get; set; } = true;

        private void Update()
        {
            float mx = Input.GetAxis(_mouseXAxis) * _mouseSensitivity;
            float my = Input.GetAxis(_mouseYAxis) * _mouseSensitivity;
            if (_invertMouseY)
                my = -my;
            LookDelta = new Vector2(mx, my);

            if (!GameplayInputEnabled)
            {
                MoveAxes = Vector2.zero;
                JumpPressedThisFrame = false;
                SprintHeld = false;
                CrouchHeld = false;
                CrouchPressedThisFrame = false;
                InteractPressedThisFrame = false;
                FireHeld = false;
                FirePressedThisFrame = false;
                ReloadPressedThisFrame = false;
                WeaponSlot1PressedThisFrame = false;
                WeaponSlot2PressedThisFrame = false;
                return;
            }

            float h = Input.GetAxisRaw(_horizontalAxis);
            float v = Input.GetAxisRaw(_verticalAxis);
            MoveAxes = new Vector2(h, v);

            JumpPressedThisFrame = Input.GetKeyDown(_jumpKey);
            SprintHeld = Input.GetKey(_sprintKey);
            CrouchHeld = Input.GetKey(_crouchKey);
            CrouchPressedThisFrame = Input.GetKeyDown(_crouchKey);
            InteractPressedThisFrame = Input.GetKeyDown(_interactKey);

            int fireIndex = (int)_fireMouseButton;
            FireHeld = Input.GetMouseButton(fireIndex);
            FirePressedThisFrame = Input.GetMouseButtonDown(fireIndex);
            ReloadPressedThisFrame = Input.GetKeyDown(_reloadKey);

            WeaponSlot1PressedThisFrame = Input.GetKeyDown(_weaponSlot1Key);
            WeaponSlot2PressedThisFrame = Input.GetKeyDown(_weaponSlot2Key);
        }
    }
}

