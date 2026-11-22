using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 납도 상태의 공격과 얼음 무구의 연동.
    ///
    /// 게임의 납도 공격은 '특수 공격'이라 원래는 축사의 검집을 발동시키지 않습니다.
    /// 초승달 식은 납도 공격에서도 검집을 발동시키고, 그때 보유한 볼루스파(얼음창)를 조준 방향으로 함께 발동시킵니다.
    /// 볼루스파의 조준 발사는 게임에 이미 있는 경로("WEAPON" 발동, 무녀의 예언이 쓰는 것)를 그대로 부릅니다.
    /// </summary>
    internal static class CharmLink
    {
        private static readonly List<object> Scabbards = new List<object>();
        private static readonly List<object> Spears = new List<object>();
        private static MethodInfo activate;
        private static MethodInfo activateNoCount;
        private static FieldInfo isAvailable;
        private static bool loggedFirst;
        private static bool loggedFailure;

        public static void Apply(Harmony harmony)
        {
            Type charging = Reflect.GameType("ChargingCharm");
            if (charging != null)
            {
                isAvailable = charging.GetField("isAvailable", Reflect.Any);
                activate = charging.GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "ActivateChargingCharm" && m.GetParameters().Length == 3);
                activateNoCount = charging.GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "ActivateChargingCharmNoCount" && m.GetParameters().Length == 3);
            }
            bool tracked = Track(harmony, "Charm_AirSlash", nameof(ScabbardOn), nameof(ScabbardOff))
                           & Track(harmony, "Charm_IceSpear", nameof(SpearOn), nameof(SpearOff));
            if (!tracked || isAvailable == null || activate == null || activateNoCount == null)
                Log.Warn("납도 공격 연동에 필요한 지점을 일부 찾지 못했습니다. 납도 공격의 검집·볼루스파 발동이 동작하지 않을 수 있습니다.");
            else
                Log.Info("납도 공격 연동 준비됨 (축사의 검집, 볼루스파)");
        }

        private static bool Track(Harmony harmony, string typeName, string on, string off)
        {
            Type type = Reflect.GameType(typeName);
            if (type == null)
                return false;
            MethodInfo enabled = AccessTools.GetDeclaredMethods(type).FirstOrDefault(m => m.Name == "OnEnabledEffect");
            MethodInfo disabled = AccessTools.GetDeclaredMethods(type).FirstOrDefault(m => m.Name == "OnDisabledEffect");
            if (enabled == null || disabled == null)
                return false;
            harmony.Patch(enabled, postfix: new HarmonyMethod(typeof(CharmLink), on));
            harmony.Patch(disabled, postfix: new HarmonyMethod(typeof(CharmLink), off));
            return true;
        }

        private static void ScabbardOn(object __instance) { if (!Scabbards.Contains(__instance)) Scabbards.Add(__instance); }
        private static void ScabbardOff(object __instance) { Scabbards.Remove(__instance); }
        private static void SpearOn(object __instance) { if (!Spears.Contains(__instance)) Spears.Add(__instance); }
        private static void SpearOff(object __instance) { Spears.Remove(__instance); }

        /// <summary>서버에서, 납도 공격이 나갈 때 불립니다. avatar 는 공격한 캐릭터입니다.</summary>
        public static void OnSheathAttack(object avatar)
        {
            try
            {
                if (activate == null || activateNoCount == null || isAvailable == null || Reflect.IsNull(avatar))
                    return;

                int fired = 0;
                foreach (object scabbard in Owned(Scabbards, avatar))
                {
                    object charging = Reflect.Get(scabbard, "chargingCharm");
                    if (Reflect.IsNull(charging))
                        continue;
                    // 충전이 끝나 있으면 게임 원래 발동(충전 소모), 아니면 충전을 보지 않는 발동.
                    if ((bool)isAvailable.GetValue(charging))
                        activate.Invoke(charging, new object[] { 1f, true, "" });
                    else
                        activateNoCount.Invoke(charging, new object[] { 1f, true, "" });
                    fired++;
                }
                if (fired == 0)
                    return; // 검집이 발동하지 않으면 볼루스파도 발동하지 않습니다.

                int spears = 0;
                foreach (object spear in Owned(Spears, avatar))
                {
                    object charging = Reflect.Get(spear, "chargingCharm");
                    if (Reflect.IsNull(charging))
                        continue;
                    activateNoCount.Invoke(charging, new object[] { 1f, true, "WEAPON" }); // 조준 방향으로 발사
                    spears++;
                }

                if (!loggedFirst)
                {
                    loggedFirst = true;
                    Log.Info("납도 공격 연동 확인: 검집 " + fired + "개, 볼루스파 " + spears + "개 발동");
                }
            }
            catch (Exception e)
            {
                if (!loggedFailure)
                {
                    loggedFailure = true;
                    Log.Error("납도 공격 연동 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
                }
            }
        }

        /// <summary>서버에서, 보유한 볼루스파를 충전과 관계없이 조준 방향으로 발동합니다. 발동한 개수를 돌려줍니다.</summary>
        public static int FireSpears(object avatar, float damageMultiplier)
        {
            if (activateNoCount == null || Reflect.IsNull(avatar))
                return 0;
            int spears = 0;
            foreach (object spear in Owned(Spears, avatar))
            {
                object charging = Reflect.Get(spear, "chargingCharm");
                if (Reflect.IsNull(charging))
                    continue;
                activateNoCount.Invoke(charging, new object[] { damageMultiplier, true, "WEAPON" });
                spears++;
            }
            return spears;
        }

        private static List<object> Owned(List<object> charms, object avatar)
        {
            charms.RemoveAll(Reflect.IsNull);
            return charms.Where(c => ReferenceEquals(Reflect.Get(c, "NetworkAvatar"), avatar)).ToList();
        }
    }
}
