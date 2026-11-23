using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 이 모드의 능력치(FFC...)가 실제로 하는 일. 모두 서버(호스트)에서만 동작합니다.
    ///  - FFCICECRIT      : 본인의 얼음 속성 공격에 치명타 확률 추가(동료의 공격은 공격자가 동료라 해당되지 않습니다)
    ///  - FFCWEAPONTOICE  : 무기 공격이 얼음속성 피해 기반이 되고, 그 값만큼(%) 피해 감소
    ///  - FFCHEALONHIT    : 피해를 줄 때마다 최대 HP 의 (값/100)% 회복
    ///  - FFCMAXHPDAMAGE  : 무기 공격 적중 시 최대 HP 의 값% 만큼 추가 피해(가장 높은 속성)
    ///  - FFCLASTSTAND    : 전투마다 한 번, 죽음에 이르는 피해를 받으면 HP 1 + 3초 무적
    /// </summary>
    internal static class ItemEffects
    {
        private sealed class StandState { public bool Used; public bool Subscribed; }

        private static readonly ConditionalWeakTable<object, StandState> Stands = new ConditionalWeakTable<object, StandState>();

        private static Type avatarType;
        private static MethodInfo getStat, healPercent, getFormula, getDamage, applyDamage, tempInvulnerable;
        private static FieldInfo origin, elemental, critChance, critDamageRate, damageAmount, damageResult, damageId, fromType, factionLayers, isDead;
        private static PropertyInfo maxHp;
        private static bool inExtraDamage;
        private static bool loggedFailure;
        private static readonly bool[] Logged = new bool[8];

        public static void Apply(Harmony harmony)
        {
            avatarType = Reflect.GameType("UnitAvatar");
            Type damage = Reflect.GameType("DamageInstance");
            Type weapon = Reflect.GameType("WeaponSimple");
            Type spawner = Reflect.GameType("PlayerSpawner");
            if (avatarType == null || damage == null)
            {
                Log.Warn("아티팩트 효과에 필요한 게임 타입을 찾지 못했습니다.");
                return;
            }

            getStat = avatarType.GetMethod("GetCustomStatUnsafe", new[] { typeof(string) });
            healPercent = avatarType.GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "HealPercent" && m.GetParameters().Length == 3);
            getFormula = avatarType.GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "GetStatWithFormula" && m.GetParameters().Length == 2);
            tempInvulnerable = avatarType.GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "StartTempInvulnerable" && m.GetParameters().Length == 1);
            applyDamage = AccessTools.GetDeclaredMethods(avatarType).FirstOrDefault(m => m.Name == "ApplyDamage" && m.GetParameters().Length == 1);
            getDamage = damage.GetMethods(Reflect.Any).FirstOrDefault(m => m.IsStatic && m.Name == "GetDamage" && m.GetParameters().Length == 10);
            maxHp = avatarType.GetProperty("MaxHp", Reflect.Any);
            isDead = avatarType.GetField("IsDead", Reflect.Any);
            origin = damage.GetField("origin", Reflect.Any);
            elemental = damage.GetField("elementalType", Reflect.Any);
            critChance = damage.GetField("criticalChancePercent", Reflect.Any);
            critDamageRate = damage.GetField("criticalDamageRate", Reflect.Any);
            damageAmount = damage.GetField("damage", Reflect.Any);
            damageResult = damage.GetField("damageResult", Reflect.Any);
            damageId = damage.GetField("id", Reflect.Any);
            fromType = damage.GetField("fromType", Reflect.Any);
            factionLayers = damage.GetField("targetFactionLayers", Reflect.Any);

            if (getStat == null || applyDamage == null || origin == null || elemental == null || critChance == null || damageResult == null)
            {
                Log.Warn("피해 처리 지점을 찾지 못해 아티팩트의 일부 효과가 동작하지 않습니다.");
            }
            else
            {
                harmony.Patch(applyDamage, prefix: new HarmonyMethod(typeof(ItemEffects), nameof(DamagePrefix)),
                    postfix: new HarmonyMethod(typeof(ItemEffects), nameof(DamagePostfix)));
            }

            MethodInfo related = weapon != null ? AccessTools.GetDeclaredMethods(weapon).FirstOrDefault(m => m.Name == "GetRelatedStatMultiplier") : null;
            if (related != null)
                harmony.Patch(related, postfix: new HarmonyMethod(typeof(ItemEffects), nameof(RelatedStatPostfix)));
            else
                Log.Warn("무기 피해 계산 지점을 찾지 못해 '얼음속성 피해 기반 변경'이 동작하지 않습니다.");

            MethodInfo die = AccessTools.GetDeclaredMethods(avatarType).FirstOrDefault(m => m.Name == "Die" && m.GetParameters().Length == 2);
            if (die != null && tempInvulnerable != null)
                harmony.Patch(die, prefix: new HarmonyMethod(typeof(ItemEffects), nameof(DiePrefix)));
            else
                Log.Warn("사망 처리 지점을 찾지 못해 '죽음 방지'가 동작하지 않습니다.");

            MethodInfo start = spawner != null ? AccessTools.GetDeclaredMethods(spawner).FirstOrDefault(m => m.Name == "OnStartServer") : null;
            if (start != null)
                harmony.Patch(start, postfix: new HarmonyMethod(typeof(ItemEffects), nameof(SpawnerPostfix)));
            else
                Log.Warn("보상 목록 지점을 찾지 못해 새 아티팩트가 보상으로 나오지 않습니다.");

            Log.Info("아티팩트 효과 패치 적용");
        }

        public static int Stat(object avatar, string key)
        {
            return (int)getStat.Invoke(avatar, new object[] { key });
        }

        private static void LogOnce(int index, string message)
        {
            if (Logged[index])
                return;
            Logged[index] = true;
            Log.Info(message);
        }

        private static void Fail(Exception e)
        {
            if (loggedFailure)
                return;
            loggedFailure = true;
            Log.Error("아티팩트 효과 처리 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
        }

        // 얼음 속성(혼합 포함)인지. EDamageElementalType: Ice=2, Chaos=4, IceAndLightning=6, FireAndIce=7
        private static bool IsIce(object type)
        {
            int value = Convert.ToInt32(type);
            return value == 2 || value == 4 || value == 6 || value == 7;
        }

        /// <summary>우주 속성의 표시 색(별빛 보라)</summary>
        public static readonly Color CosmicColor = new Color(0.66f, 0.52f, 1f, 1f);

        // 종말기수의 무기 공격을 우주 속성으로 표시합니다. 게임의 속성 목록에는 없는 속성이라, 내부적으로는 어느 속성에도
        // 속하지 않는 피해(Normal)로 두고 피해 숫자의 색만 우주 속성 색으로 바꿉니다. 혼돈 판정은 아닙니다.
        private static void MarkCosmic(object damage)
        {
            if (fromType == null || Convert.ToInt32(fromType.GetValue(damage)) != 1) // DirectAttack
                return;
            string id = damageId == null ? null : damageId.GetValue(damage) as string;
            if (id == null || !id.StartsWith("Weapon_"))
                return;
            elemental.SetValue(damage, Enum.ToObject(elemental.FieldType, 5)); // Normal
            Reflect.Call(damage, null, "SetCustomColor", false, CosmicColor);
            LogOnce(7, "종말기수 확인: 무기 공격을 우주 속성 피해로 처리");
        }

        // 피해가 계산되기 전: 얼음 속성 공격의 치명타 확률.
        private static void DamagePrefix(object damage)
        {
            try
            {
                if (damage == null)
                    return;
                object attacker = origin.GetValue(damage);
                if (Reflect.IsNull(attacker) || !avatarType.IsInstanceOfType(attacker))
                    return;
                if (LanceAddon.KindOf(attacker) == LanceAddon.Doom)
                    MarkCosmic(damage);
                if (!IsIce(elemental.GetValue(damage)))
                    return;
                int bonus = Stat(attacker, ItemContent.IceCrit);
                int critDamage = 0;
                if (LanceAddon.IsBlue(attacker)) // 블루비틀 전용 아티팩트
                {
                    bonus += Stat(attacker, ItemContent.LanceIceCrit);
                    critDamage = Stat(attacker, ItemContent.LanceIceCritDamage);
                }
                if (bonus > 0)
                {
                    critChance.SetValue(damage, (float)critChance.GetValue(damage) + bonus);
                    LogOnce(0, "얼음 속성 치명타 확률 +" + bonus + "% 적용 확인");
                }
                if (critDamage > 0 && critDamageRate != null)
                {
                    critDamageRate.SetValue(damage, (int)critDamageRate.GetValue(damage) + critDamage);
                    LogOnce(6, "얼음 속성 치명타 피해 +" + critDamage + "% 적용 확인");
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // 피해가 들어간 뒤: 피해 시 회복, 최대 HP 비례 추가 피해.
        private static void DamagePostfix(object __instance, object damage)
        {
            try
            {
                if (damage == null || inExtraDamage)
                    return;
                object attacker = origin.GetValue(damage);
                if (Reflect.IsNull(attacker) || !avatarType.IsInstanceOfType(attacker) || ReferenceEquals(attacker, __instance))
                    return;
                if (Convert.ToInt32(damageResult.GetValue(damage)) <= 0)
                    return;

                if (fromType != null && Convert.ToInt32(fromType.GetValue(damage)) == 1) // DirectAttack
                    LanceAddon.OnWeaponHit(attacker, __instance, damage);

                int heal = Stat(attacker, ItemContent.HealOnHit);
                int extra = Stat(attacker, ItemContent.MaxHpDamage);
                if (heal <= 0 && extra <= 0)
                    return;
                // 추가 피해를 만들면 damage 가 재사용될 수 있으므로 필요한 값은 먼저 읽어 둡니다.
                bool direct = fromType != null && Convert.ToInt32(fromType.GetValue(damage)) == 1; // DirectAttack
                bool own = damageId != null && (string)damageId.GetValue(damage) == ItemContent.MaxHpDamageId;
                object layers = factionLayers != null ? factionLayers.GetValue(damage) : 0L;

                if (heal > 0 && healPercent != null && !(bool)isDead.GetValue(attacker))
                {
                    healPercent.Invoke(attacker, new object[] { heal / 100f, true, false });
                    LogOnce(1, "피해 시 회복 적용 확인 (최대 HP 의 " + (heal / 100f) + "%)");
                }

                if (extra > 0 && direct && !own && getDamage != null && getFormula != null && !(bool)isDead.GetValue(__instance))
                {
                    var arguments = new object[] { "HIGHEST", null };
                    getFormula.Invoke(attacker, arguments);
                    float amount = (float)maxHp.GetValue(attacker, null) * extra / 100f;
                    ParameterInfo[] p = getDamage.GetParameters();
                    object position = ((Component)__instance).transform.position;
                    object created = getDamage.Invoke(null, new[]
                    {
                        attacker, ItemContent.MaxHpDamageId, position, layers, amount,
                        Enum.ToObject(p[5].ParameterType, 0), Enum.ToObject(p[6].ParameterType, 0),
                        Activator.CreateInstance(p[7].ParameterType), 0, 0f
                    });
                    elemental.SetValue(created, arguments[1]);
                    inExtraDamage = true;
                    try
                    {
                        applyDamage.Invoke(__instance, new[] { created });
                    }
                    finally
                    {
                        inExtraDamage = false;
                    }
                    LogOnce(2, "최대 HP 비례 추가 피해 적용 확인 (" + amount.ToString("0") + ", 속성 " + arguments[1] + ")");
                }
            }
            catch (Exception e)
            {
                inExtraDamage = false;
                Fail(e);
            }
        }

        // 무기 공격의 기반 능력치: 얼음속성 피해 기반 + 피해 감소.
        private static void RelatedStatPostfix(object owner, ref float __result, ref EDamageElementalType result)
        {
            try
            {
                if (Reflect.IsNull(owner) || getStat == null)
                    return;
                if (LanceAddon.KindOf(owner) == LanceAddon.Doom)
                {
                    // 종말기수: 무기 공격은 우주 속성 피해 수치만으로 계산합니다.
                    __result = Math.Max(1, Stat(owner, ItemContent.Cosmic));
                    result = EDamageElementalType.Normal;
                    return;
                }
                int reduce = Stat(owner, ItemContent.WeaponToIce);
                if (reduce <= 0)
                    return;
                if (reduce > 100)
                    reduce = 100;
                __result = Stat(owner, "ICEDAMAGE") * (1f - reduce / 100f);
                result = EDamageElementalType.Ice;
                LogOnce(3, "무기 공격 얼음속성 기반 변경 확인 (피해 -" + reduce + "%)");
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // 죽음에 이르는 피해: 전투마다 한 번 HP 1 로 버티고 3초 무적.
        private static bool DiePrefix(object __instance)
        {
            try
            {
                if (Reflect.IsNull(__instance) || (bool)isDead.GetValue(__instance) || Stat(__instance, ItemContent.LastStand) <= 0)
                    return true;
                StandState state = Stands.GetOrCreateValue(__instance);
                if (state.Used)
                    return true;
                if (!state.Subscribed)
                {
                    // 다음 전투가 시작되면 다시 쓸 수 있게 합니다.
                    EventInfo battle = avatarType.GetEvent("OnStartBattle", Reflect.Any);
                    if (battle != null)
                    {
                        Action reset = () => state.Used = false;
                        battle.AddEventHandler(__instance, reset);
                        state.Subscribed = true;
                    }
                }
                state.Used = true;
                Reflect.Set(__instance, "Networkhp", 1f);
                tempInvulnerable.Invoke(__instance, new object[] { 3f });
                LogOnce(4, "죽음 방지 발동 확인 (HP 1, 3초 무적)");
                return false;
            }
            catch (Exception e)
            {
                Fail(e);
                return true;
            }
        }

        // 새 아티팩트를 보상 목록에 넣습니다(게임은 기본 아이템만 넣습니다).
        private static void SpawnerPostfix(object __instance)
        {
            try
            {
                object list = Reflect.Get(__instance, "unlockedCharms");
                if (list == null)
                    return;
                MethodInfo contains = list.GetType().GetMethod("Contains", new[] { typeof(int) });
                MethodInfo add = list.GetType().GetMethod("Add", new[] { typeof(int) });
                if (add == null)
                    return;
                foreach (int id in ItemContent.ItemIds)
                    if (contains == null || !(bool)contains.Invoke(list, new object[] { id }))
                        add.Invoke(list, new object[] { id });
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }
    }
}
