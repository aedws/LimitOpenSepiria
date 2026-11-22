using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 진단용: 초승달 식으로 공격을 시작한 직후에 게임이 재생하는 소리와 카타나가 받는 동작 신호를 기록합니다.
    /// 공격 때 들리는 소리가 어디서 나는지 확인하기 위한 것으로, 게임 동작은 바꾸지 않습니다(처음 40줄만 기록).
    /// </summary>
    internal static class SoundProbe
    {
        private const int WindowMs = 450;
        private static int lastAttackTick = int.MinValue;
        private static int lastFireId;
        private static int lines;

        public static void Apply(Harmony harmony)
        {
            try
            {
                Type sound = Reflect.GameType("SoundManager");
                if (sound != null)
                {
                    foreach (MethodInfo method in sound.GetMethods(Reflect.Any).Where(m => m.IsStatic && (m.Name == "PlaySoundEmitter" || m.Name == "PlayOneShot" || m.Name == "TryCreateSoundInstance")))
                        harmony.Patch(method, prefix: new HarmonyMethod(typeof(SoundProbe), nameof(SoundPrefix)));
                }
                Type katana = Reflect.GameType("WeaponSimple_Katana");
                MethodInfo message = katana != null ? AccessTools.GetDeclaredMethods(katana).FirstOrDefault(m => m.Name == "OnServerReceivedMessage") : null;
                if (message != null)
                    harmony.Patch(message, prefix: new HarmonyMethod(typeof(SoundProbe), nameof(MessagePrefix)));
            }
            catch (Exception e)
            {
                Log.Warn("소리 진단 준비 실패: " + e.Message);
            }
        }

        public static void MarkAttack(int fireId)
        {
            lastAttackTick = Environment.TickCount;
            lastFireId = fireId;
        }

        private static bool InWindow()
        {
            return lines < 40 && unchecked(Environment.TickCount - lastAttackTick) < WindowMs;
        }

        private static void SoundPrefix(MethodBase __originalMethod, object[] __args)
        {
            try
            {
                if (!InWindow() || __args == null || __args.Length == 0)
                    return;
                object first = __args[0];
                string what;
                var emitter = first as Component;
                if (!Reflect.IsNull(emitter))
                    what = "'" + emitter.name + "' " + Describe(Reflect.Get(emitter, "EventReference"));
                else
                    what = first is string ? (string)first : Describe(first);
                lines++;
                Log.Info("소리 " + __originalMethod.Name + " " + what + " (공격 " + lastFireId + "번 시작 후 " + unchecked(Environment.TickCount - lastAttackTick) + "ms)");
            }
            catch (Exception)
            {
            }
        }

        private static void MessagePrefix(object __instance, string message)
        {
            try
            {
                if (!InWindow())
                    return;
                var weapon = __instance as Component;
                if (Reflect.IsNull(weapon) || Reflect.IsNull(weapon.GetComponent(typeof(CrescentAddon))))
                    return;
                lines++;
                Log.Info("카타나 신호 '" + message + "' (공격 " + lastFireId + "번 시작 후 " + unchecked(Environment.TickCount - lastAttackTick) + "ms)");
            }
            catch (Exception)
            {
            }
        }

        private static string Describe(object reference)
        {
            if (reference == null)
                return "(없음)";
            object path = Reflect.Get(reference, "Path");
            object guid = Reflect.Get(reference, "Guid");
            return (path != null && path.ToString().Length > 0 ? path.ToString() : "") + " " + (guid != null ? guid.ToString() : reference.ToString());
        }
    }
}
