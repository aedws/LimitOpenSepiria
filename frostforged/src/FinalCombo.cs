using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 초승달 식의 공격을 모두 '마지막 공격'으로 판정합니다.
    ///  - 기본 공격: 무기의 마지막 공격 번호를 0(고정된 첫 번째 베기)으로 둡니다(CrescentAddon). 게임 원래 판정을 그대로 탑니다.
    ///  - 납도 공격: 게임에서는 특수 공격이라 마지막 공격 판정이 없습니다. 판정이 만들어질 때 기본 공격의 마지막 공격과
    ///    같은 처리(마지막 공격 피해 증가, 마지막 공격 알림)를 해 줍니다.
    /// </summary>
    internal static class FinalCombo
    {
        private static bool loggedFirst;
        private static bool loggedFailure;

        public static void Apply(Harmony harmony)
        {
            Type katana = Reflect.GameType("WeaponSimple_Katana");
            MethodInfo created = katana != null ? AccessTools.GetDeclaredMethods(katana).FirstOrDefault(m => m.Name == "OnSpecialAttackCreated") : null;
            if (created == null)
            {
                Log.Warn("납도 공격 판정 지점을 찾지 못해 납도 공격의 '마지막 공격' 판정은 동작하지 않습니다.");
                return;
            }
            harmony.Patch(created, postfix: new HarmonyMethod(typeof(FinalCombo), nameof(SpecialCreatedPostfix)));
            Log.Info("마지막 공격 판정 패치 적용: WeaponSimple_Katana.OnSpecialAttackCreated");
        }

        private static void SpecialCreatedPostfix(object __instance, object projectile)
        {
            try
            {
                var weapon = __instance as Component;
                if (Reflect.IsNull(weapon) || Reflect.IsNull(projectile) || Reflect.IsNull(weapon.GetComponent(typeof(CrescentAddon))))
                    return;
                if (!Equals(Reflect.Get(weapon, "sheathStateEnabled"), true))
                    return; // 납도 상태의 공격만
                object controller = Reflect.Get(weapon, "Networkowner");
                object avatar = Reflect.Get(controller, "unitAvatar");
                if (Reflect.IsNull(controller) || Reflect.IsNull(avatar))
                    return;

                // WeaponSimple.BasicAttackCreated 의 마지막 공격 처리와 같습니다.
                Reflect.Set(projectile, "forceFinalCombo", true);
                float bonus = Convert.ToSingle(Reflect.Call(avatar, null, "GetCustomStatUnsafe", "LASTBASICATTACKDAMAGE"));
                float ratio = Convert.ToSingle(Reflect.Get(projectile, "defaultDamageRatio"));
                Reflect.Set(projectile, "defaultDamageRatio", ratio * (1f + bonus / 100f));
                var notify = Reflect.Get(controller, "OnSwingCreated") as Delegate;
                if (notify != null)
                    notify.DynamicInvoke(true, projectile);

                if (!loggedFirst)
                {
                    loggedFirst = true;
                    Log.Info("납도 공격 마지막 공격 판정 확인 (마지막 공격 피해 +" + bonus + "%)");
                }
            }
            catch (Exception e)
            {
                if (!loggedFailure)
                {
                    loggedFailure = true;
                    Log.Error("마지막 공격 판정 처리 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
                }
            }
        }
    }
}
