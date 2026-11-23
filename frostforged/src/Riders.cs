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
    ///  - 구름씨 화살촉의 확률(%)만큼 먹구름 번개의 피해도 늘어납니다(여러 개면 합산).
    /// 종말기수
    ///  - 태양검·얼음 무구·먹구름의 피해가 혼돈 피해로 바뀌고, 가장 높은 속성 피해를 기준으로 다시 계산됩니다.
    ///    (치명타 확률·피해 증가는 LanceAddon 에서 능력치로 줍니다.)
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
        private static HashSet<string> armamentIds;
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

        private sealed class ArrowCache { public long Ticks; public int Percent; }
        private static readonly ConditionalWeakTable<object, ArrowCache> Arrows = new ConditionalWeakTable<object, ArrowCache>();

        // 보유한 구름씨 화살촉의 확률 합(%). 찾는 비용이 있어 1초 동안은 같은 값을 씁니다.
        private static int ArrowheadPercent(object avatar)
        {
            ArrowCache cache = Arrows.GetOrCreateValue(avatar);
            long now = DateTime.UtcNow.Ticks;
            if (cache.Ticks != 0 && now - cache.Ticks < TimeSpan.TicksPerSecond)
                return cache.Percent;
            cache.Ticks = now;
            cache.Percent = 0;
            Type charm = Reflect.GameType("Charm_Lightning_BasicAttack");
            MethodInfo find = typeof(UnityEngine.Object).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "FindObjectsOfType" && !m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Type));
            if (charm == null || find == null)
                return 0;
            foreach (object owned in (IEnumerable)find.Invoke(null, new object[] { charm }))
            {
                if (Reflect.IsNull(owned) || !ReferenceEquals(Reflect.Get(owned, "NetworkAvatar"), avatar) || !Equals(Reflect.Get(owned, "IsEffectEnabled"), true))
                    continue;
                var byLevel = Reflect.Get(owned, "lightningPercentByLevel") as int[];
                if (byLevel == null || byLevel.Length == 0)
                    continue;
                int index = Convert.ToInt32(Reflect.Call(owned, null, "CurrentLevelToIdx"));
                cache.Percent += byLevel[Math.Max(0, Math.Min(byLevel.Length - 1, index))];
            }
            return cache.Percent;
        }

        // 먹구름 번개의 피해(서버). 뇌격기수면 구름씨 화살촉의 확률만큼 피해가 늘어납니다.
        private static void LightningPrefix(object __instance, ref int baseDamagePercent)
        {
            try
            {
                object avatar = Reflect.Get(__instance, "Networkavatar");
                if (LanceAddon.KindOf(avatar) != LanceAddon.Thunder)
                    return;
                int bonus = ArrowheadPercent(avatar);
                if (bonus <= 0)
                    return;
                baseDamagePercent = baseDamagePercent * (100 + bonus) / 100;
                Once(4, "뇌격기수 확인: 구름씨 화살촉으로 먹구름 피해 +" + bonus + "%");
            }
            catch (Exception e)
            {
                Fail("뇌격 피해", e);
            }
        }

        // ── 종말기수 ─────────────────────────────────────────────────────────
        // 얼음 무구(충전식 아티팩트)들의 피해 이름. 아이템 목록에서 한 번 모읍니다.
        private static HashSet<string> ArmamentIds()
        {
            if (armamentIds != null)
                return armamentIds;
            var ids = new HashSet<string>();
            try
            {
                Type database = Reflect.GameType("ItemDatabase");
                var all = Reflect.Call(null, database, "GetAllItemID") as IEnumerable;
                if (all != null)
                    foreach (object id in all)
                    {
                        object entity = Reflect.Call(null, database, "FindItemById", id);
                        var prefab = Reflect.IsNull(entity) ? null : Reflect.Get(entity, "resourcePrefab") as GameObject;
                        Component charm = Reflect.IsNull(prefab) ? null : prefab.GetComponent(Reflect.GameType("Charm_Basic"));
                        if (Reflect.IsNull(charm) || Reflect.IsNull(Reflect.Get(charm, "chargingCharm")))
                            continue;
                        string damageId = Reflect.Get(charm, "damageId") as string;
                        if (!string.IsNullOrEmpty(damageId))
                            ids.Add(damageId);
                    }
            }
            catch (Exception e)
            {
                Fail("얼음 무구 목록", e);
            }
            Log.Info("종말기수: 얼음 무구 피해 " + ids.Count + "종 [" + string.Join(", ", ids.ToArray()) + "]");
            armamentIds = ids;
            return ids;
        }

        /// <summary>종말기수가 주는 이 피해가 태양검·얼음 무구·먹구름의 것인가</summary>
        public static bool IsDoomSource(string damageId)
        {
            if (string.IsNullOrEmpty(damageId))
                return false;
            return damageId == "Ability_FlameSword" || damageId == "Ability_DarkCloud" || ArmamentIds().Contains(damageId);
        }

        public static void LogDoom()
        {
            Once(5, "종말기수 확인: 태양검·얼음 무구·먹구름 피해를 혼돈 피해로 전환");
        }
    }
}
