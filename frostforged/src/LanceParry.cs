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
    /// 블루비틀의 우클릭: 패링 → 퓨리 → 돌파.
    ///
    ///  - 퓨리가 없을 때 우클릭: 창의 원래 특수 공격이 그대로 나가고, 그 동작이 시작될 때 패링 판정이 걸립니다
    ///    (무기 종류와 상관없는 게임의 공용 패링 기능 WeaponControllerSimple.StartParryInvincible).
    ///  - 패링에 성공하면(게임이 "PARRY!" 를 띄우는 순간) 퓨리 3개를 얻습니다.
    ///  - 퓨리가 있을 때 우클릭: 퓨리 1개를 써서 '돌파'. 조준 방향으로 돌진하며 창격의 마지막 찌르기를 특수 공격 판정으로
    ///    내지르고, 돌진이 시작해서 끝날 때까지 무적입니다(단검 퓨리와 같은 무적을 씁니다).
    /// 모두 서버(호스트)에서 처리합니다.
    /// </summary>
    internal static class LanceParry
    {
        public const float ParryWindow = 0.5f;     // 패링 판정 시간(초)
        public const int FuryPerParry = 3;         // 패링 한 번에 얻는 퓨리
        public const int MaxFury = 9;              // 쌓아 둘 수 있는 퓨리
        public const float BreakDamage = 2.5f;     // 돌파 피해 배율(마지막 찌르기 대비)
        public const float DashSpeed = 26f;        // 돌파 속도(칸/초)
        public const float DashTime = 0.18f;       // 돌파 시간(초)
        private const float BreakInterval = 0.25f; // 돌파 연타 간격(초)
        private const string HudEntity = "CHARGEDSWEEP"; // 숫자를 띄울 수 있는 게임의 상태 표시
        private const string HudName = "FFC_BlueBeetle_Fury";

        private sealed class State { public int Fury; public long ArmedTicks; public long BreakTicks; public bool Hud; }

        private static readonly ConditionalWeakTable<object, State> States = new ConditionalWeakTable<object, State>();
        private static readonly Dictionary<object, UnityEngine.Object> BreakData = new Dictionary<object, UnityEngine.Object>();
        private static bool loggedParry, loggedFury, loggedBreak, loggedFailure;

        public static void Apply(Harmony harmony)
        {
            Type staff = Reflect.GameType("WeaponSimple_QuartterStaff");
            Type unit = Reflect.GameType("UnitAvatar");
            List<MethodInfo> methods = staff == null ? new List<MethodInfo>() : AccessTools.GetDeclaredMethods(staff);
            MethodInfo sub = methods.FirstOrDefault(m => m.Name == "SubAttackButtonDown" && m.GetParameters().Length == 0);
            MethodInfo start = methods.FirstOrDefault(m => m.Name == "StartSpecialAttackAnimation" && m.GetParameters().Length == 1);
            MethodInfo parried = unit == null ? null : AccessTools.GetDeclaredMethods(unit).FirstOrDefault(m => m.Name == "RpcCreateParryFx" && m.GetParameters().Length == 2);
            if (sub == null || start == null || parried == null)
            {
                Log.Warn("우클릭·패링 지점을 찾지 못해 블루비틀의 패링과 돌파는 동작하지 않습니다(원래 특수 공격만 나갑니다).");
                return;
            }
            harmony.Patch(sub, prefix: new HarmonyMethod(typeof(LanceParry), nameof(SubPrefix)));
            harmony.Patch(start, postfix: new HarmonyMethod(typeof(LanceParry), nameof(StartPostfix)));
            harmony.Patch(parried, prefix: new HarmonyMethod(typeof(LanceParry), nameof(ParriedPrefix)));
        }

        private static void Fail(string where, Exception e)
        {
            if (loggedFailure)
                return;
            loggedFailure = true;
            Log.Error("블루비틀 패링 처리 중 오류(" + where + ", 이후 같은 오류는 기록하지 않습니다): " + e);
        }

        private static bool IsLance(Component weapon)
        {
            return !Reflect.IsNull(weapon) && !Reflect.IsNull(weapon.GetComponent(typeof(LanceAddon)));
        }

        // 우클릭(서버). 퓨리가 있으면 돌파, 없으면 원래 특수 공격(= 패링 동작)을 그대로 진행시킵니다.
        private static bool SubPrefix(Component __instance)
        {
            try
            {
                if (!IsLance(__instance))
                    return true;
                object controller = Reflect.Get(__instance, "Networkowner");
                object avatar = Reflect.IsNull(controller) ? null : Reflect.Get(controller, "unitAvatar");
                if (Reflect.IsNull(avatar) || !Equals(Reflect.Get(controller, "isServer"), true))
                    return true;

                State state = States.GetOrCreateValue(avatar);
                long now = DateTime.UtcNow.Ticks;
                if (state.Fury > 0)
                {
                    if (now - state.BreakTicks < (long)(BreakInterval * TimeSpan.TicksPerSecond))
                        return false;
                    state.BreakTicks = now;
                    SetFury(avatar, state, state.Fury - 1);
                    Break(__instance, controller, avatar);
                    return false;
                }
                state.ArmedTicks = now; // 이 우클릭으로 특수 공격 동작이 시작되면 패링을 겁니다
            }
            catch (Exception e)
            {
                Fail("우클릭", e);
            }
            return true;
        }

        // 특수 공격 동작이 시작됨(서버): 방금 우클릭한 것이면 패링 판정을 겁니다.
        private static void StartPostfix(Component __instance)
        {
            try
            {
                if (!IsLance(__instance))
                    return;
                object controller = Reflect.Get(__instance, "Networkowner");
                object avatar = Reflect.IsNull(controller) ? null : Reflect.Get(controller, "unitAvatar");
                if (Reflect.IsNull(avatar) || !Equals(Reflect.Get(controller, "isServer"), true))
                    return;
                State state = States.GetOrCreateValue(avatar);
                if (state.ArmedTicks == 0 || DateTime.UtcNow.Ticks - state.ArmedTicks > 2 * TimeSpan.TicksPerSecond)
                    return;
                state.ArmedTicks = 0; // 한 번의 우클릭에 한 번만(특수 공격의 두 번째 휘두르기에는 걸지 않습니다)
                Reflect.Call(controller, null, "StartParryInvincible", ParryWindow);
                if (!loggedParry)
                {
                    loggedParry = true;
                    Log.Info("블루비틀 패링 발동 확인 (판정 " + ParryWindow + "초)");
                }
            }
            catch (Exception e)
            {
                Fail("패링 시작", e);
            }
        }

        // 패링 성공: 게임이 패링 이펙트를 띄우는 순간(서버). 블루비틀을 들고 있으면 퓨리를 얻습니다.
        private static void ParriedPrefix(object __instance)
        {
            try
            {
                if (LanceAddon.Find(__instance) == null)
                    return;
                State state = States.GetOrCreateValue(__instance);
                SetFury(__instance, state, Math.Min(MaxFury, state.Fury + FuryPerParry));
                if (!loggedFury)
                {
                    loggedFury = true;
                    Log.Info("블루비틀 패링 성공 확인: 퓨리 " + state.Fury + "개");
                }
            }
            catch (Exception e)
            {
                Fail("패링 성공", e);
            }
        }

        /// <summary>무기를 내려놓을 때: 퓨리와 표시를 지웁니다.</summary>
        public static void Clear(object avatar)
        {
            try
            {
                State state;
                if (!Reflect.IsNull(avatar) && States.TryGetValue(avatar, out state))
                    SetFury(avatar, state, 0);
            }
            catch (Exception)
            {
            }
        }

        private static void SetFury(object avatar, State state, int value)
        {
            state.Fury = value;
            try
            {
                if (value > 0)
                {
                    if (!state.Hud)
                    {
                        Reflect.Call(avatar, null, "CreateEffectHUD", HudEntity, HudName);
                        state.Hud = true;
                    }
                    Reflect.Call(avatar, null, "SetEffectHUDValue", HudName, value.ToString());
                }
                else if (state.Hud)
                {
                    Reflect.Call(avatar, null, "DestroyEffectHUD", HudName);
                    state.Hud = false;
                }
            }
            catch (Exception e)
            {
                Fail("퓨리 표시", e);
            }
        }

        // 돌파에 쓰는 공격 데이터: 창격의 마지막(긴) 찌르기를 복제해 퓨리 공격 표식을 붙입니다(퓨리에 반응하는 아티팩트용).
        private static object BreakFireData(Component weapon)
        {
            var attacks = Reflect.Get(weapon, "basicComboAttacks") as IList;
            if (attacks == null || attacks.Count == 0)
                return null;
            var source = attacks[attacks.Count - 1] as UnityEngine.Object;
            if (Reflect.IsNull(source))
                return null;
            UnityEngine.Object data;
            if (BreakData.TryGetValue(source, out data) && !Reflect.IsNull(data))
                return data;
            data = UnityEngine.Object.Instantiate(source);
            data.name = source.name + "_FFCBreak";
            Reflect.Set(data, "hideFlags", 61);
            Reflect.Set(data, "swingID", "FURY");
            LanceFx.Claim(data);
            BreakData[source] = data;
            return data;
        }

        private static void Break(Component weapon, object controller, object avatar)
        {
            object data = BreakFireData(weapon);
            if (data == null)
                return;

            Vector3 origin = ((Component)avatar).transform.position;
            var aim = (Vector3)Reflect.Get(controller, "aimedPositionClientside");
            float dx = aim.x - origin.x, dy = aim.y - origin.y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.01f)
            {
                dx = 1f;
                dy = 0f;
                length = 1f;
            }
            float nx = dx / length, ny = dy / length;

            // 찌르기가 나가는 자리: 무기를 휘두르는 지점의 바닥 위치(없으면 캐릭터 위치)
            Vector3 position = origin;
            object shoulder = Reflect.Get(controller, "shoulder");
            var swingPoint = Reflect.IsNull(shoulder) ? null : Reflect.Get(shoulder, "swingPoint") as Transform;
            if (!Reflect.IsNull(swingPoint))
            {
                Vector3 swing = swingPoint.position;
                object shoulderPosition = Reflect.Get(shoulder, "Position");
                float height = shoulderPosition is Vector3 ? ((Vector3)shoulderPosition).y : 0f;
                position = new Vector3(swing.x, swing.y - height, swing.z);
            }

            // 돌진하는 동안 무적(단검 퓨리의 무적). 돌진보다 조금 길게 잡아 끝나는 지점까지 덮습니다.
            Reflect.Call(controller, null, "StartFuryInvincible", DashTime + 0.08f);
            MethodInfo dash = avatar.GetType().GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "SetAttackDash" && m.GetParameters().Length == 3);
            if (dash != null)
                dash.Invoke(avatar, new[] { (object)new Vector3(nx * DashSpeed, ny * DashSpeed, 0f), Enum.ToObject(dash.GetParameters()[1].ParameterType, 1), DashTime }); // 1 = Uniform

            // 특수 공격 판정으로 내지릅니다.
            MethodInfo create = typeof(WeaponSimple).GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "CreateSpecialAttackProjectileFullyManual" && m.GetParameters().Length == 9);
            if (create == null)
                return;
            create.Invoke(weapon, new[] { data, position, (object)new Vector3(dx, dy, 0f), null, 0f, 0, 0, 0, BreakDamage });

            // 찌르기 궤적(이 화면에만 표시됩니다)
            try
            {
                MethodInfo fx = typeof(WeaponSimple).GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "CreateBasicAttackSwingFxFullyManual" && m.GetParameters().Length == 1);
                if (fx != null && Equals(Reflect.Get(weapon, "isClient"), true))
                    fx.Invoke(weapon, new[] { data });
            }
            catch (Exception)
            {
            }

            if (!loggedBreak)
            {
                loggedBreak = true;
                Log.Info("블루비틀 돌파 발동 확인 (특수 공격 판정, 피해 " + BreakDamage + "배)");
            }
        }
    }
}
