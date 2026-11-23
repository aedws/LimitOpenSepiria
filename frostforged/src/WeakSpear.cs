using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 약화된 볼루스파: 절반 피해로, 충전 시간과 관계없이 조준 방향에 발동합니다(크기는 그대로).
    /// 게임의 볼루스파 발동 경로를 그대로 쓰므로 얼음 무구 추가 발동 효과가 적용됩니다.
    /// 피해 배율은 추가 발동에도 그대로 전달됩니다.
    /// </summary>
    internal static class WeakSpear
    {
        /// <summary>약화 발동의 피해 배율.</summary>
        public const float DamageMultiplier = 0.5f;

        private static bool logged, loggedFailure;

        public static void Apply(Harmony harmony)
        {
            Type spear = Reflect.GameType("Charm_IceSpear");
            if (spear == null)
            {
                Log.Warn("볼루스파를 찾지 못해 블루비틀의 볼루스파 발동이 동작하지 않습니다.");
                return;
            }

            // 무녀의 예언과 같은 자리: 볼루스파가 주인의 적중을 받는 곳. 블루비틀을 들고 있으면 적중마다 약화 발동합니다.
            MethodInfo onAttack = AccessTools.GetDeclaredMethods(spear).FirstOrDefault(m => m.Name == "OnAttackUnit" && m.GetParameters().Length == 2);
            noCount = Reflect.GameType("ChargingCharm") == null ? null
                : Reflect.GameType("ChargingCharm").GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "ActivateChargingCharmNoCount" && m.GetParameters().Length == 3);
            if (onAttack == null || noCount == null)
                Log.Warn("볼루스파의 적중 지점을 찾지 못해 블루비틀의 볼루스파 발동이 동작하지 않습니다.");
            else
                harmony.Patch(onAttack, postfix: new HarmonyMethod(typeof(WeakSpear), nameof(AttackPostfix)));
        }

        private static MethodInfo noCount;
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Stamp> LastFired
            = new System.Runtime.CompilerServices.ConditionalWeakTable<object, Stamp>();
        private sealed class Stamp { public long Ticks; }
        private const long MinIntervalTicks = 1000000; // 0.1초: 한 번 찌를 때 여러 적이 맞아도 한 번만

        // Charm_IceSpear.OnAttackUnit(맞은 대상, 피해). 서버에서 불립니다.
        private static void AttackPostfix(object __instance, object instance)
        {
            try
            {
                if (instance == null || Convert.ToString(Reflect.Get(instance, "fromType")) != "DirectAttack")
                    return;
                if (!LanceAddon.IsBlue(Reflect.Get(__instance, "NetworkAvatar")))
                    return;
                Stamp last = LastFired.GetOrCreateValue(__instance);
                long now = DateTime.UtcNow.Ticks;
                if (now - last.Ticks < MinIntervalTicks)
                    return;
                last.Ticks = now;
                object charging = Reflect.Get(__instance, "chargingCharm");
                if (Reflect.IsNull(charging))
                    return;
                noCount.Invoke(charging, new object[] { DamageMultiplier, true, "WEAPON" });
                if (!logged)
                {
                    logged = true;
                    Log.Info("약화된 볼루스파 발동 확인 (피해 50%)");
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private static void Fail(Exception e)
        {
            if (loggedFailure)
                return;
            loggedFailure = true;
            Log.Error("약화된 볼루스파 처리 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
        }
    }
}
