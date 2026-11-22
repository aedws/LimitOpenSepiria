using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// '서리기수 : 돌격창 블루비틀'의 창격을 3·4·5타만 반복하게 합니다.
    ///
    /// 창격의 각 타가 애니메이터의 어느 상태·어느 시점에서 시작하는지는 게임 데이터에 있어 코드로는 알 수 없습니다.
    /// 그래서 공격이 시작될 때마다 (타 번호 → 상태, 시점)을 기록해 두고, 3타의 위치를 알게 된 뒤에는
    /// 1·2타가 시작되려 할 때 곧바로 3타 위치로 넘깁니다. 기록은 모드 폴더의 lance_motion.txt 에 남겨
    /// 다음 실행부터는 처음부터 3타로 시작합니다(처음 한 번만 1타부터 끝까지 나갑니다).
    /// </summary>
    internal static class LanceMotion
    {
        private struct Point { public int Hash; public float Time; }

        private const int LoopFrom = 2;      // 0부터 세어 세 번째 타
        private const int MaxComboId = 14;   // 그보다 큰 번호는 특수 동작
        private const string FileName = "lance_motion.txt";

        private static readonly SortedDictionary<int, Point> Points = new SortedDictionary<int, Point>();
        private static FieldInfo layerField;
        private static MethodInfo play;
        private static bool loaded, loggedLoop, loggedFailure;

        public static void Apply(Harmony harmony)
        {
            Type controller = Reflect.GameType("WeaponControllerSimple");
            MethodInfo begin = controller != null
                ? AccessTools.GetDeclaredMethods(controller).FirstOrDefault(m => m.Name == "BeginAttackAnimation" && m.GetParameters().Length == 1)
                : null;
            layerField = controller != null ? controller.GetField("currentWeaponAnimationLayer", Reflect.Any) : null;
            if (begin == null || layerField == null)
            {
                Log.Warn("공격 시작 지점을 찾지 못해 '창격 3·4·5타 반복'은 동작하지 않습니다.");
                return;
            }
            harmony.Patch(begin, prefix: new HarmonyMethod(typeof(LanceMotion), nameof(BeginPrefix)));
        }

        private static string FilePath
        {
            get { return string.IsNullOrEmpty(Entry.packageRoot) ? null : Path.Combine(Entry.packageRoot, FileName); }
        }

        private static void Load()
        {
            loaded = true;
            try
            {
                string path = FilePath;
                if (path == null || !File.Exists(path))
                    return;
                foreach (string line in File.ReadAllLines(path))
                {
                    string[] parts = line.Split('=', ',');
                    int id, hash;
                    float time;
                    if (parts.Length == 3 && int.TryParse(parts[0], out id) && int.TryParse(parts[1], out hash)
                        && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out time))
                        Points[id] = new Point { Hash = hash, Time = time };
                }
                if (Points.Count > 0)
                    Log.Info("창격 동작 기록 " + Points.Count + "개를 불러왔습니다.");
            }
            catch (Exception e)
            {
                Log.Warn("창격 동작 기록을 읽지 못했습니다: " + e.Message);
            }
        }

        private static void Save()
        {
            try
            {
                string path = FilePath;
                if (path != null)
                    File.WriteAllLines(path, Points.Select(p => p.Key + "=" + p.Value.Hash + "," + p.Value.Time.ToString("0.####", CultureInfo.InvariantCulture)).ToArray());
            }
            catch (Exception)
            {
                // 폴더에 쓸 수 없으면 이번 실행 동안만 기억합니다.
            }
        }

        private static bool BeginPrefix(object __instance, int fireId)
        {
            try
            {
                if (fireId < 0 || fireId > MaxComboId)
                    return true;
                var weapon = Reflect.Get(__instance, "currentWeapon") as Component;
                if (Reflect.IsNull(weapon) || Reflect.IsNull(weapon.GetComponent(typeof(LanceAddon))))
                    return true;
                object animator = Reflect.Get(__instance, "animator");
                if (Reflect.IsNull(animator))
                    return true;
                if (!loaded)
                    Load();
                int layer = (int)layerField.GetValue(__instance);

                // 지금 시작된 타의 위치를 기록합니다(전환 중이면 들어가고 있는 상태가 그 타입니다).
                bool transition = (bool)Reflect.Call(animator, null, "IsInTransition", layer);
                object info = Reflect.Call(animator, null, transition ? "GetNextAnimatorStateInfo" : "GetCurrentAnimatorStateInfo", layer);
                int hash = (int)Reflect.Get(info, "fullPathHash");
                float time = (float)Reflect.Get(info, "normalizedTime");
                time -= (float)Math.Floor(time);
                Point known;
                if (hash != 0 && (!Points.TryGetValue(fireId, out known) || known.Hash != hash || Math.Abs(known.Time - time) > 0.05f))
                {
                    Points[fireId] = new Point { Hash = hash, Time = time };
                    Save();
                    Log.Info("창격 동작 기록: " + fireId + "번 타 = 상태 " + hash + ", 시점 " + time.ToString("0.###", CultureInfo.InvariantCulture));
                }

                if (Points.Count <= LoopFrom)
                    return true;
                int first = Points.Keys.ElementAt(LoopFrom);
                if (fireId >= first)
                    return true;

                // 1·2타가 시작되려 함 → 3타 위치로 넘깁니다. 3타는 자기 시작 신호로 다시 이 함수를 지나갑니다.
                Point target = Points[first];
                if (target.Hash == hash && Math.Abs(target.Time - time) < 0.02f)
                    return true; // 기록이 같은 위치를 가리키면 넘기지 않습니다(제자리 반복 방지)
                if (play == null)
                    play = animator.GetType().GetMethod("Play", new[] { typeof(int), typeof(int), typeof(float) });
                if (play == null)
                    return true;
                play.Invoke(animator, new object[] { target.Hash, layer, Math.Max(0f, target.Time - 0.002f) });
                if (!loggedLoop)
                {
                    loggedLoop = true;
                    Log.Info("창격 반복 동작 확인: " + fireId + "번 타 → " + first + "번 타부터");
                }
                return false;
            }
            catch (Exception e)
            {
                if (!loggedFailure)
                {
                    loggedFailure = true;
                    Log.Error("창격 반복 처리 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
                }
                return true;
            }
        }
    }
}
