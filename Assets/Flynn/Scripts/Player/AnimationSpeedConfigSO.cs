using UnityEngine;

namespace Flynn.Player
{
    /// <summary>
    /// Per-animation playback speed multipliers. Slotted into PlayerController2D
    /// so designers can normalise clip durations without re-importing sprites.
    /// </summary>
    [CreateAssetMenu(menuName = "Flynn/Player/Animation Speed Config", fileName = "AnimationSpeedConfig")]
    public class AnimationSpeedConfigSO : ScriptableObject
    {
        [Header("Locomotion")]
        [Min(0.1f)] public float idleSpeed = 1f;
        [Min(0.1f)] public float runSpeed = 1f;
        [Min(0.1f)] public float swimSpeed = 1f;
        [Min(0.1f)] public float grappleSpeed = 1f;
        [Min(0.1f)] public float carrySpeed = 1f;

        [Header("Triggers")]
        [Min(0.1f)] public float throwSpeed = 1f;
        [Min(0.1f)] public float jumpSpeed = 1f;

        public float GetSpeed(PlayerAnimationState state)
        {
            switch (state)
            {
                case PlayerAnimationState.Idle:    return idleSpeed;
                case PlayerAnimationState.Run:     return runSpeed;
                case PlayerAnimationState.Swim:    return swimSpeed;
                case PlayerAnimationState.Grapple: return grappleSpeed;
                case PlayerAnimationState.Carry:   return carrySpeed;
                case PlayerAnimationState.Throw:   return throwSpeed;
                case PlayerAnimationState.Jump:    return jumpSpeed;
                default:                           return 1f;
            }
        }
    }
}
