using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 축사의 검집 발동 규칙.
    ///
    /// 게임의 검집(Charm_AirSlash)은 기본 공격을 휘두를 때 '충전이 끝나 있으면' 참격을 날립니다.
    /// 초승달 식을 들고 있으면 충전 여부와 관계없이 매번 날리게 합니다.
    ///  - 충전이 끝나 있으면: 게임 원래 경로 그대로(충전 소모).
    ///  - 충전 중이면: 게임에 이미 있는 '충전을 보지 않는 발동'(ActivateChargingCharmNoCount)을 부릅니다.
    /// 두 경로 모두 게임의 발동 함수이므로 증폭·재발동·얼음 무구 세트 효과가 그대로 따라옵니다.
    /// </summary>
    internal static class ScabbardPatch
    {
        private static FieldInfo chargingCharmField;
        private static FieldInfo isAvailableField;
        private static MethodInfo activateNoCount;
        private static bool loggedFailure;
        private static bool loggedFirst;

        public static void Apply(Harmony harmony)
        {
            Type charm = Reflect.GameType("Charm_AirSlash");
            Type charging = Reflect.GameType("ChargingCharm");
            MethodInfo handleSwing = charm != null ? AccessTools.GetDeclaredMethods(charm).FirstOrDefault(m => m.Name == "HandleSwing") : null;
            if (charm != null)
                chargingCharmField = charm.GetField("chargingCharm", Reflect.Any);
            if (charging != null)
            {
                isAvailableField = charging.GetField("isAvailable", Reflect.Any);
                activateNoCount = charging.GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "ActivateChargingCharmNoCount" && m.GetParameters().Length == 3);
            }

            if (handleSwing == null || chargingCharmField == null || isAvailableField == null || activateNoCount == null)
            {
                Log.Warn("검집 발동 지점을 찾지 못했습니다. 무기는 등록되지만 '매 공격 발동'은 동작하지 않습니다.");
                return;
            }

            harmony.Patch(handleSwing, prefix: new HarmonyMethod(typeof(ScabbardPatch), nameof(HandleSwingPrefix)));
            Log.Info("검집 발동 패치 적용: Charm_AirSlash.HandleSwing");
        }

        private static bool HandleSwingPrefix(object __instance)
        {
            try
            {
                if (!HoldsCrescent(__instance))
                    return true;

                object charging = chargingCharmField.GetValue(__instance);
                if (Reflect.IsNull(charging))
                    return true;

                if (!loggedFirst)
                {
                    loggedFirst = true;
                    Log.Info("초승달 식으로 검집 발동 확인 (" + __instance.GetType().Name + " '" + ((UnityEngine.Object)__instance).name + "')");
                }

                // 충전이 끝나 있으면 게임 원래 경로가 처리합니다.
                if ((bool)isAvailableField.GetValue(charging))
                    return true;

                activateNoCount.Invoke(charging, new object[] { 1f, true, "" });
                return false;
            }
            catch (Exception e)
            {
                if (!loggedFailure)
                {
                    loggedFailure = true;
                    Log.Error("검집 발동 처리 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
                }
                return true;
            }
        }

        private static bool HoldsCrescent(object charm)
        {
            object controller = Reflect.Get(charm, "WeaponController");
            if (Reflect.IsNull(controller))
                return false;
            var weapon = Reflect.Get(controller, "currentWeapon") as Component;
            return !Reflect.IsNull(weapon) && !Reflect.IsNull(weapon.GetComponent(typeof(CrescentAddon)));
        }
    }
}
