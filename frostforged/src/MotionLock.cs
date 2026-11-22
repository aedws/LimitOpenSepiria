using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 기본 공격 모션을 첫 번째 베기로 고정합니다.
    ///
    /// 카타나의 연속 베기 순서는 애니메이터의 상태 전환으로 정해지고, 각 베기가 시작될 때 애니메이션이
    /// WeaponControllerSimple.BeginAttackAnimation(공격 번호)를 부릅니다.
    ///  - 0번(첫 베기)이 시작되면 그 애니메이터 상태를 기억합니다.
    ///  - 초승달 식을 든 채 1번 이후의 베기가 시작되려 하면, 기억해 둔 첫 베기 상태를 처음부터 다시 재생합니다.
    /// 다시 재생된 첫 베기가 0번으로 공격을 시작하므로 피해·검집 발동은 게임 원래 경로를 그대로 탑니다.
    /// </summary>
    internal static class MotionLock
    {
        private sealed class State { public int FirstHash; public bool Known; }

        private static readonly ConditionalWeakTable<object, State> States = new ConditionalWeakTable<object, State>();
        private static FieldInfo layerField;
        private static MethodInfo play;
        private static bool loggedLock;
        private static bool loggedFailure;

        public static void Apply(Harmony harmony)
        {
            Type controller = Reflect.GameType("WeaponControllerSimple");
            MethodInfo begin = controller != null
                ? AccessTools.GetDeclaredMethods(controller).FirstOrDefault(m => m.Name == "BeginAttackAnimation" && m.GetParameters().Length == 1)
                : null;
            layerField = controller != null ? controller.GetField("currentWeaponAnimationLayer", Reflect.Any) : null;
            if (begin == null || layerField == null)
            {
                Log.Warn("공격 시작 지점을 찾지 못해 '첫 번째 베기 고정'은 동작하지 않습니다.");
                return;
            }
            harmony.Patch(begin, prefix: new HarmonyMethod(typeof(MotionLock), nameof(BeginPrefix)));
            Log.Info("모션 고정 패치 적용: WeaponControllerSimple.BeginAttackAnimation");
        }

        private static bool BeginPrefix(object __instance, int fireId)
        {
            try
            {
                var weapon = Reflect.Get(__instance, "currentWeapon") as Component;
                if (Reflect.IsNull(weapon) || Reflect.IsNull(weapon.GetComponent(typeof(CrescentAddon))))
                    return true;

                object animator = Reflect.Get(__instance, "animator");
                if (Reflect.IsNull(animator))
                    return true;
                int layer = (int)layerField.GetValue(__instance);

                SoundProbe.MarkAttack(fireId);
                State state = States.GetOrCreateValue(__instance);
                if (fireId == 0)
                {
                    // 전환 중이면 들어가고 있는 상태가 첫 베기입니다.
                    bool transition = (bool)Reflect.Call(animator, null, "IsInTransition", layer);
                    object info = Reflect.Call(animator, null, transition ? "GetNextAnimatorStateInfo" : "GetCurrentAnimatorStateInfo", layer);
                    state.FirstHash = (int)Reflect.Get(info, "fullPathHash");
                    state.Known = state.FirstHash != 0;
                    return true;
                }

                if (!state.Known)
                    return true;

                if (play == null)
                    play = animator.GetType().GetMethod("Play", new[] { typeof(int), typeof(int), typeof(float) });
                if (play == null)
                    return true;

                play.Invoke(animator, new object[] { state.FirstHash, layer, 0f });
                if (!loggedLock)
                {
                    loggedLock = true;
                    Log.Info("모션 고정 동작 확인: " + fireId + "번 베기 → 첫 번째 베기 (레이어 " + layer + ")");
                }
                return false;
            }
            catch (Exception e)
            {
                if (!loggedFailure)
                {
                    loggedFailure = true;
                    Log.Error("모션 고정 처리 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
                }
                return true;
            }
        }
    }
}
