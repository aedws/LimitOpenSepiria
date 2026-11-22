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
    /// 먹구름 콤보 개선: 먹구름이 0 이어도 콤보(2)가 켜져 있으면 번개가 절반 피해로 발동합니다.
    /// 이렇게 먹구름을 쓰지 않고 나간 번개는 번개·소모 이펙트가 절반 크기로 나옵니다.
    /// 피해와 발동은 서버(호스트)에서 처리하고, 이펙트 축소는 호스트 본인 화면에만 적용됩니다.
    /// </summary>
    internal static class DarkCloudBoost
    {
        private const int WeakDamagePercent = 50;
        private const float WeakFxScale = 0.5f;
        private const string EffectKey = "ComboEffect_FFC_DarkCloudEmpty";

        private static FieldInfo cloud, active, playerCombo, timer, interval, comboCount;
        private static PropertyInfo avatar, ratio, serverActive;
        private static MethodInfo fire, useCloud, timerUpdate, statUnsafe, statEnum;
        private static object attackSpeed;

        // 지금 '빈 먹구름' 발동을 처리 중인 콤보
        private static readonly HashSet<object> Weak = new HashSet<object>();
        // 서버가 보낸 이펙트 요청 순서대로 약한 번개 여부를 적어 둡니다(같은 프로세스의 클라이언트가 꺼내 씁니다).
        private static readonly ConditionalWeakTable<object, Queue<bool>> FxFlags = new ConditionalWeakTable<object, Queue<bool>>();
        private static List<object> captured;
        private static bool logged, loggedFailure;

        public static void Apply(Harmony harmony)
        {
            Type combo = Reflect.GameType("ComboEffect_DarkCloud");
            Type unit = Reflect.GameType("UnitAvatar");
            Type timerType = Reflect.GameType("Timer");
            Type customStat = Reflect.GameType("ECustomStat");
            Type server = Reflect.UnityType("Mirror.NetworkServer", "Mirror");
            if (combo == null || unit == null || timerType == null)
            {
                Log.Warn("먹구름 콤보를 찾지 못해 먹구름 개선이 적용되지 않습니다.");
                return;
            }

            cloud = combo.GetField("darkCloud", Reflect.Any);
            active = combo.GetField("isDarkCloudActive", Reflect.Any);
            timer = combo.GetField("cloudTimer", Reflect.Any);
            interval = combo.GetField("lightningIntervalSpeed", Reflect.Any);
            comboCount = combo.GetField("darkCloudComboCount", Reflect.Any);
            ratio = combo.GetProperty("NetworkcloudRatio", Reflect.Any);
            playerCombo = AccessTools.Field(combo, "isPlayerCombo");
            avatar = AccessTools.Property(combo, "Networkavatar");
            serverActive = server != null ? server.GetProperty("active", Reflect.Any) : null;
            timerUpdate = timerType.GetMethod("Update", new[] { typeof(float) });
            statUnsafe = unit.GetMethod("GetCustomStatUnsafe", new[] { typeof(string) });
            if (customStat != null)
            {
                statEnum = unit.GetMethod("GetCustomStat", new[] { customStat });
                if (Enum.IsDefined(customStat, "AttackSpeed"))
                    attackSpeed = Enum.Parse(customStat, "AttackSpeed");
            }

            List<MethodInfo> methods = AccessTools.GetDeclaredMethods(combo);
            fire = methods.FirstOrDefault(m => m.Name == "FireLightning" && m.GetParameters().Length == 2);
            useCloud = methods.FirstOrDefault(m => m.Name == "UseCloud" && m.GetParameters().Length == 2
                                                   && m.GetParameters()[1].ParameterType == typeof(bool));
            MethodInfo update = methods.FirstOrDefault(m => m.Name == "Update" && m.GetParameters().Length == 0);
            MethodInfo coroutine = methods.FirstOrDefault(m => m.Name == "UseCloudCoroutine" && m.GetParameters().Length == 3);
            MethodInfo toTarget = methods.FirstOrDefault(m => m.Name == "UseCloudToTarget" && m.GetParameters().Length == 2);
            MethodInfo sendFx = methods.FirstOrDefault(m => m.Name == "TargetCreateFx");
            MethodInfo showFx = methods.FirstOrDefault(m => m.Name.StartsWith("UserCode_TargetCreateFx"));
            MethodInfo comboData = methods.FirstOrDefault(m => m.Name == "OnRequestComboData" && m.GetParameters().Length == 2);

            if (cloud == null || active == null || playerCombo == null || avatar == null || timer == null || interval == null
                || serverActive == null || timerUpdate == null || statUnsafe == null
                || fire == null || useCloud == null || update == null || coroutine == null)
            {
                Log.Warn("먹구름 콤보의 구조가 예상과 달라 먹구름 개선이 적용되지 않습니다.");
                return;
            }

            Type self = typeof(DarkCloudBoost);
            harmony.Patch(fire, prefix: new HarmonyMethod(self, nameof(FirePrefix)));
            harmony.Patch(coroutine, postfix: new HarmonyMethod(self, nameof(CoroutinePostfix)));
            harmony.Patch(update, postfix: new HarmonyMethod(self, nameof(UpdatePostfix)));
            if (toTarget != null)
                harmony.Patch(toTarget, prefix: new HarmonyMethod(self, nameof(ToTargetPrefix)));
            if (comboData != null && comboCount != null)
                harmony.Patch(comboData, postfix: new HarmonyMethod(self, nameof(ComboDataPostfix)));

            Type spriteFx = Reflect.GameType("SpriteFx");
            MethodInfo onSpawn = spriteFx != null ? AccessTools.DeclaredMethod(spriteFx, "OnSpawn") : null;
            if (sendFx != null && showFx != null && onSpawn != null)
            {
                harmony.Patch(sendFx, prefix: new HarmonyMethod(self, nameof(SendFxPrefix)));
                harmony.Patch(showFx, prefix: new HarmonyMethod(self, nameof(ShowFxPrefix)), postfix: new HarmonyMethod(self, nameof(ShowFxPostfix)));
                harmony.Patch(onSpawn, postfix: new HarmonyMethod(self, nameof(SpawnPostfix)));
            }
            else
            {
                Log.Warn("번개 이펙트 지점을 찾지 못해 빈 먹구름 번개의 크기는 줄어들지 않습니다.");
            }
            Log.Info("먹구름 개선 적용: 먹구름이 비어도 " + WeakDamagePercent + "% 피해로 발동");
        }

        // 먹구름이 0 이고, 플레이어의 먹구름 콤보(2)가 켜져 있는가
        private static bool IsEmptyButActive(object combo)
        {
            return (bool)playerCombo.GetValue(combo) && (bool)active.GetValue(combo) && (int)cloud.GetValue(combo) <= 0;
        }

        private static void Fail(string where, Exception e)
        {
            if (loggedFailure)
                return;
            loggedFailure = true;
            Log.Error("먹구름 개선 처리 중 오류(" + where + "): " + e);
        }

        // ── 피해: 빈 먹구름 발동이면 절반 ──────────────────────────────────────
        private static void FirePrefix(object __instance, ref int baseDamagePercent)
        {
            if (!Weak.Contains(__instance))
                return;
            baseDamagePercent = baseDamagePercent * WeakDamagePercent / 100;
            if (!logged)
            {
                logged = true;
                Log.Info("먹구름 개선 확인: 빈 먹구름 번개 발동 (피해 " + baseDamagePercent + "%)");
            }
        }

        // ── 발동: 원래 코루틴은 먹구름이 0 이면 쏘지 않으므로, 한 발 처리하는 동안만 1 을 빌려줍니다 ──
        private static void CoroutinePostfix(object __instance, ref IEnumerator __result, bool needCloudValue)
        {
            if (needCloudValue && __result != null)
                __result = Wrap(__instance, __result);
        }

        private static IEnumerator Wrap(object combo, IEnumerator inner)
        {
            while (true)
            {
                bool lent = false;
                bool more;
                try
                {
                    if (!Reflect.IsNull(combo) && IsEmptyButActive(combo))
                    {
                        // 동기화 없이 값만 잠깐 바꿉니다. 발동하면 원래 코드가 0 으로 되돌려 놓습니다.
                        cloud.SetValue(combo, 1);
                        lent = Weak.Add(combo);
                    }
                    more = inner.MoveNext();
                }
                finally
                {
                    if (lent)
                    {
                        Weak.Remove(combo);
                        if (!Reflect.IsNull(combo) && (int)cloud.GetValue(combo) != 0)
                            cloud.SetValue(combo, 0);
                    }
                }
                if (!more)
                    yield break;
                yield return inner.Current;
            }
        }

        // ── 자동 방전: 원래 Update 는 먹구름이 0 이면 타이머를 돌리지 않습니다 ──
        private static void UpdatePostfix(object __instance)
        {
            try
            {
                if (!(bool)serverActive.GetValue(null, null) || !IsEmptyButActive(__instance))
                    return;
                object unit = avatar.GetValue(__instance, null);
                if (Reflect.IsNull(unit) || (bool)Reflect.Get(unit, "IsDead") || !(bool)Reflect.Get(unit, "IsInBattle"))
                    return;

                float speed = (int)statUnsafe.Invoke(unit, new object[] { "DARKCLOUDSPEED" }) / 100f;
                float bonus = 0f;
                int attackSpeedBonus = (int)statUnsafe.Invoke(unit, new object[] { "DARKCLOUDATKSPEEDBONUS" });
                if (attackSpeedBonus > 0 && statEnum != null && attackSpeed != null)
                    bonus = (int)statEnum.Invoke(unit, new[] { attackSpeed }) * attackSpeedBonus / 100f / 100f;

                object cloudTimer = timer.GetValue(__instance);
                float step = Time.deltaTime * (float)interval.GetValue(__instance) * (1f + speed + bonus);
                if ((bool)timerUpdate.Invoke(cloudTimer, new object[] { step }))
                    useCloud.Invoke(__instance, new object[] { true, true });
                if (ratio != null)
                    ratio.SetValue(__instance, Reflect.Get(cloudTimer, "Ratio"), null);
            }
            catch (Exception e)
            {
                Fail("자동 방전", e);
            }
        }

        // ── 지정 대상 발동(구름 병 등) ─────────────────────────────────────────
        private static bool ToTargetPrefix(object __instance, object target, int baseDamagePercent)
        {
            try
            {
                if (Reflect.IsNull(target) || !IsEmptyButActive(__instance))
                    return true;
                bool added = Weak.Add(__instance);
                try
                {
                    fire.Invoke(__instance, new object[] { target, baseDamagePercent });
                }
                finally
                {
                    if (added)
                        Weak.Remove(__instance);
                }
                return false;
            }
            catch (Exception e)
            {
                Fail("지정 대상 발동", e);
                return true;
            }
        }

        // ── 콤보 설명에 한 줄 추가 ─────────────────────────────────────────────
        private static void ComboDataPostfix(object __instance, IList elements)
        {
            try
            {
                Type elementType = Reflect.GameType("ComboEffectElement");
                Type localized = Reflect.GameType("LocalizedString");
                if (elements == null || elementType == null || localized == null)
                    return;
                object element = Activator.CreateInstance(elementType);
                Reflect.Set(element, "comboCount", comboCount.GetValue(__instance));
                Reflect.Set(element, "effectName", Activator.CreateInstance(localized, EffectKey).ToString());
                elements.Add(element);
            }
            catch (Exception e)
            {
                Fail("콤보 설명", e);
            }
        }

        // ── 이펙트 크기 ───────────────────────────────────────────────────────
        // 서버 쪽: 이펙트 요청을 보낼 때 약한 번개인지 순서대로 기록합니다(이 PC 가 주인인 콤보만).
        private static void SendFxPrefix(object __instance)
        {
            try
            {
                if (!(Reflect.Get(__instance, "isOwned") is bool owned) || !owned)
                    return;
                Queue<bool> flags = FxFlags.GetOrCreateValue(__instance);
                if (flags.Count > 64)
                    flags.Clear();
                flags.Enqueue(Weak.Contains(__instance));
            }
            catch (Exception e)
            {
                Fail("이펙트 기록", e);
            }
        }

        // 클라이언트 쪽: 약한 번개면 이 호출에서 만들어지는 이펙트를 모아 둡니다.
        private static void ShowFxPrefix(object __instance, out bool __state)
        {
            captured = null;
            Queue<bool> flags;
            __state = FxFlags.TryGetValue(__instance, out flags) && flags.Count > 0 && flags.Dequeue();
            if (__state)
                captured = new List<object>();
        }

        private static void SpawnPostfix(object __instance)
        {
            if (captured != null)
                captured.Add(__instance);
        }

        private static void ShowFxPostfix(bool __state)
        {
            List<object> list = captured;
            captured = null;
            if (!__state || list == null)
                return;
            try
            {
                for (int i = 0; i < list.Count; i++)
                {
                    object animator = Reflect.Get(list[i], "animator2D");
                    if (Reflect.IsNull(animator))
                        continue;
                    Transform transform = ((Component)animator).transform;
                    Vector3 scale = transform.localScale;
                    transform.localScale = new Vector3(scale.x * WeakFxScale, scale.y * WeakFxScale, scale.z);
                    if (i == 0)
                    {
                        // 첫 번째는 구름에서 대상까지 늘어나는 번개 줄기: 굵기만 줄고 길이는 그대로여야 합니다.
                        float length = (float)Reflect.Get(Reflect.Call(animator, null, "GetSize"), "y");
                        Reflect.Call(animator, null, "SetSizeY", length / WeakFxScale);
                    }
                }
            }
            catch (Exception e)
            {
                Fail("이펙트 크기", e);
            }
        }
    }
}
