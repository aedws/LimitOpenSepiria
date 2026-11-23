using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 기수 무기(만변창 : 4기사의 3단계)의 고유 효과. 모두 서버(호스트)에서 처리합니다.
    ///
    /// 화염기수
    ///  - 태양검을 얻을 때마다(상한에 걸려 실제로 늘지 않아도) '화염 날개 깃'을 1개 얻습니다. 깃에는 상한이 없습니다.
    ///  - 태양검이 모자랄 때 깃이 태양검 대신 쓰입니다(태양검으로 판정). 깃으로 나간 태양검은 화염 속성 피해의 160% 입니다.
    ///  - 깃이 20개 이상이면 공격 속도 +100%, 화상 최대 중첩 +3.
    /// 뇌격기수
    ///  - 무기 공격이 적중한 적마다 먹구름 번개를 떨어뜨립니다(먹구름 1 소모, 없으면 50% 피해 — DarkCloudBoost).
    ///  - 먹구름 강화 확률(구름씨 화살촉)이 100% 를 넘으면, 넘은 만큼이 피해 배율로 이어 붙습니다.
    /// 종말기수의 치명타 확률·피해 증가는 LanceAddon 에서 능력치로 줍니다.
    /// </summary>
    internal static class Riders
    {
        public const int FeatherBuffCount = 20;
        public const int FeatherAttackSpeed = 100;
        public const int FeatherBurnStack = 3;
        public const float FeatherDamagePercent = 160f;
        private const string FeatherHud = "FFC_Rider_Feather";

        private sealed class State { public int Feathers; public bool Buff; public bool Hud; }

        private static readonly ConditionalWeakTable<object, State> States = new ConditionalWeakTable<object, State>();
        private static bool pendingFeather, featherShot;
        private static readonly bool[] Logged = new bool[6];
        private static bool loggedFailure;

        public static void Apply(Harmony harmony)
        {
            Type self = typeof(Riders);
            Type sword = Reflect.GameType("ComboEffect_FlameSword");
            if (sword != null)
            {
                List<MethodInfo> methods = AccessTools.GetDeclaredMethods(sword);
                MethodInfo add = methods.FirstOrDefault(m => m.Name == "AddSwordServer");
                MethodInfo use = methods.FirstOrDefault(m => m.Name == "UseSword" && m.GetParameters().Length == 1);
                MethodInfo create = methods.FirstOrDefault(m => m.Name == "CreateFlameSword");
                MethodInfo damage = methods.FirstOrDefault(m => m.Name == "GetDamage" && m.GetParameters().Length == 1);
                if (add != null && use != null && create != null && damage != null)
                {
                    harmony.Patch(add, postfix: new HarmonyMethod(self, nameof(AddSwordPostfix)));
                    harmony.Patch(use, prefix: new HarmonyMethod(self, nameof(UseSwordPrefix)));
                    harmony.Patch(create, postfix: new HarmonyMethod(self, nameof(CreateSwordPostfix)));
                    harmony.Patch(damage, postfix: new HarmonyMethod(self, nameof(SwordDamagePostfix)));
                }
                else
                {
                    Log.Warn("태양검 지점을 찾지 못해 화염기수의 화염 날개 깃은 동작하지 않습니다.");
                }
            }
            Type cloud = Reflect.GameType("ComboEffect_DarkCloud");
            MethodInfo fire = cloud == null ? null : AccessTools.GetDeclaredMethods(cloud).FirstOrDefault(m => m.Name == "FireLightning" && m.GetParameters().Length == 2);
            if (fire != null)
                harmony.Patch(fire, prefix: new HarmonyMethod(self, nameof(LightningPrefix)));
            else
                Log.Warn("먹구름 지점을 찾지 못해 뇌격기수의 피해 증가는 동작하지 않습니다.");
        }

        private static void Once(int index, string message)
        {
            if (Logged[index])
                return;
            Logged[index] = true;
            Log.Info(message);
        }

        private static void Fail(string where, Exception e)
        {
            if (loggedFailure)
                return;
            loggedFailure = true;
            Log.Error("기수 무기 효과 처리 중 오류(" + where + ", 이후 같은 오류는 기록하지 않습니다): " + e);
        }

        private static object Combo(object avatar, string name)
        {
            object inventory = Reflect.Get(avatar, "Inventory");
            if (Reflect.IsNull(inventory))
                return null;
            object combo = Reflect.Call(inventory, null, "FindComboEffect", name);
            return Reflect.IsNull(combo) ? null : combo;
        }

        // ── 장착·해제 ─────────────────────────────────────────────────────────
        public static void OnEquip(object avatar, int kind)
        {
            try
            {
                if (kind == LanceAddon.Flame)
                    RefreshFeathers(avatar, States.GetOrCreateValue(avatar));
            }
            catch (Exception e)
            {
                Fail("장착", e);
            }
        }

        public static void OnUnequip(object avatar)
        {
            try
            {
                State state;
                if (Reflect.IsNull(avatar) || !States.TryGetValue(avatar, out state))
                    return;
                SetBuff(avatar, state, false);
                if (state.Hud)
                {
                    Reflect.Call(avatar, null, "DestroyEffectHUD", FeatherHud);
                    state.Hud = false;
                }
            }
            catch (Exception e)
            {
                Fail("해제", e);
            }
        }

        // ── 화염기수 ─────────────────────────────────────────────────────────
        private static void RefreshFeathers(object avatar, State state)
        {
            if (state.Feathers > 0)
            {
                if (!state.Hud)
                {
                    Reflect.Call(avatar, null, "CreateEffectHUD",
                        StatusHud.Ensure("FFC_FEATHER", "EffectHUD_FFC_Feather_Name", "EffectHUD_FFC_Feather_FlavorText", "feather.png"), FeatherHud);
                    state.Hud = true;
                }
                Reflect.Call(avatar, null, "SetEffectHUDValue", FeatherHud, state.Feathers.ToString());
            }
            else if (state.Hud)
            {
                Reflect.Call(avatar, null, "DestroyEffectHUD", FeatherHud);
                state.Hud = false;
            }
            SetBuff(avatar, state, state.Feathers >= FeatherBuffCount);
        }

        private static void SetBuff(object avatar, State state, bool on)
        {
            if (state.Buff == on || Reflect.IsNull(avatar))
                return;
            int sign = on ? 1 : -1;
            Type stat = Reflect.GameType("ECustomStat");
            MethodInfo add = avatar.GetType().GetMethod("AddCustomStat", new[] { stat, typeof(int) });
            MethodInfo addUnsafe = avatar.GetType().GetMethod("AddCustomStatUnsafe", new[] { typeof(string), typeof(int) });
            if (add == null || addUnsafe == null)
                return;
            add.Invoke(avatar, new[] { Enum.Parse(stat, "AttackSpeed"), sign * FeatherAttackSpeed });
            addUnsafe.Invoke(avatar, new object[] { "BURNSTACK", sign * FeatherBurnStack });
            state.Buff = on;
            if (on)
                Once(1, "화염기수 확인: 화염 날개 깃 " + FeatherBuffCount + "개 — 공격 속도 +" + FeatherAttackSpeed + "%, 화상 중첩 +" + FeatherBurnStack);
        }

        // 태양검을 얻음(서버). 상한 때문에 실제로 늘지 않았어도 깃은 1개 얻습니다.
        private static void AddSwordPostfix(object __instance, int amount)
        {
            try
            {
                if (amount <= 0)
                    return;
                object avatar = Reflect.Get(__instance, "Networkavatar");
                if (LanceAddon.KindOf(avatar) != LanceAddon.Flame)
                    return;
                State state = States.GetOrCreateValue(avatar);
                state.Feathers++;
                RefreshFeathers(avatar, state);
                Once(0, "화염기수 확인: 태양검 획득으로 화염 날개 깃 획득");
            }
            catch (Exception e)
            {
                Fail("깃 획득", e);
            }
        }

        // 태양검을 씀(서버). 태양검이 모자라면 깃이 대신 쓰입니다.
        private static bool UseSwordPrefix(object __instance, int count, ref bool __result)
        {
            pendingFeather = false;
            try
            {
                object avatar = Reflect.Get(__instance, "Networkavatar");
                if (count <= 0 || LanceAddon.KindOf(avatar) != LanceAddon.Flame)
                    return true;
                State state;
                if (!States.TryGetValue(avatar, out state) || state.Feathers <= 0)
                    return true;
                int swords = Convert.ToInt32(Reflect.Get(__instance, "currentSword"));
                if (swords >= count || swords + state.Feathers < count)
                    return true;
                Reflect.Set(__instance, "NetworkcurrentSword", 0);
                state.Feathers -= count - swords;
                RefreshFeathers(avatar, state);
                pendingFeather = true;
                __result = true;
                return false;
            }
            catch (Exception e)
            {
                Fail("깃 사용", e);
                return true;
            }
        }

        // 태양검 한 자루가 날아가는 과정. 깃으로 나간 것이면 그 과정 동안 피해 계산에 표시를 해 둡니다.
        private static void CreateSwordPostfix(ref IEnumerator __result)
        {
            if (!pendingFeather || __result == null)
                return;
            pendingFeather = false;
            __result = FeatherFlight(__result);
        }

        private static IEnumerator FeatherFlight(IEnumerator inner)
        {
            while (true)
            {
                bool more;
                featherShot = true;
                try
                {
                    more = inner.MoveNext();
                }
                finally
                {
                    featherShot = false;
                }
                if (!more)
                    yield break;
                yield return inner.Current;
            }
        }

        private static void SwordDamagePostfix(object __instance, ref float __result)
        {
            if (!featherShot)
                return;
            try
            {
                float normal = Convert.ToSingle(Reflect.Get(__instance, "damagePercent"));
                if (normal > 0f)
                    __result *= FeatherDamagePercent / normal;
                Once(2, "화염기수 확인: 화염 날개 깃 발사 (화염 속성 피해 " + FeatherDamagePercent + "%)");
            }
            catch (Exception e)
            {
                Fail("깃 피해", e);
            }
        }

        // ── 뇌격기수 ─────────────────────────────────────────────────────────
        /// <summary>뇌격기수의 무기 공격이 victim 에게 적중함(서버).</summary>
        public static void ThunderHit(object attacker, object victim)
        {
            try
            {
                object cloud = Combo(attacker, "DARKCLOUD");
                if (cloud == null || !Equals(Reflect.Get(cloud, "isDarkCloudActive"), true))
                    return;
                // 먹구름이 있으면 1 소모해 100%, 없으면 DarkCloudBoost 가 50% 로 떨어뜨립니다.
                Reflect.Call(cloud, null, "UseCloudToTarget", victim, 100);
                Once(3, "뇌격기수 확인: 적중한 적에게 먹구름 번개");
            }
            catch (Exception e)
            {
                Fail("뇌격 적중", e);
            }
        }

        private static readonly System.Random Dice = new System.Random();

        // 먹구름 번개의 피해(서버). 게임은 '먹구름 강화 확률'(구름씨 화살촉)이 100% 를 넘어도 2배 한 번만 적용합니다.
        // 뇌격기수는 넘은 만큼을 배율로 이어 붙입니다: 100% 마다 +1배, 나머지는 그 확률로 +1배.
        // (예: 250% → 3배 확정, 50% 확률로 4배)
        private static void LightningPrefix(object __instance, ref int baseDamagePercent)
        {
            try
            {
                object avatar = Reflect.Get(__instance, "Networkavatar");
                if (LanceAddon.KindOf(avatar) != LanceAddon.Thunder)
                    return;
                int luck = ItemEffects.Stat(avatar, "DARKCLOUDLUCK");
                if (luck <= 100)
                    return;
                int over = luck - 100;
                int multiplier = 2 + over / 100 + (Dice.Next(100) < over % 100 ? 1 : 0); // 게임이 이미 2배는 적용합니다
                if (multiplier <= 2)
                    return;
                baseDamagePercent = baseDamagePercent * multiplier / 2;
                Once(4, "뇌격기수 확인: 먹구름 강화 확률 " + luck + "% → " + multiplier + "배");
            }
            catch (Exception e)
            {
                Fail("뇌격 피해", e);
            }
        }
    }
}
