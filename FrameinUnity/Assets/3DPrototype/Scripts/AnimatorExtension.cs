using UnityEngine;
using System;

// アニメータのステートを列挙
public enum PlayerAnimState
{
    Idle,
    Locomotion,
    Jump,
    Squat
};

public static class AnimatorExtension
{
    public static void SetBool(this Animator anim, PlayerAnimState state, bool b)
    {
        string name = state.ToString();
        anim.SetBool(name, b);
    }

    public static int EnumToHash(this Animator anim, PlayerAnimState state)
    {
        int hash = Animator.StringToHash("Base Layer." + state.ToString());
        return hash;
    }
}