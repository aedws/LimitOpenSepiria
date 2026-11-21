using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace LimitOpenSepiria
{
    /// <summary>
    /// 재능 포인트 최대치와 과일 꼬치 최대 개수.
    ///
    /// 재능 포인트:
    ///   게임은 PlayerAvatar.maxPassivePoint(기본 5)에 나무 상점·하드 모드 보상을 '더하고 빼는' 식으로
    ///   최대치를 관리하고, 재능 탭은 그 값에서 쓴 포인트를 뺀 수를 보여 줍니다.
    ///   이 모드는 서버(호스트)에서 그 값에 보너스를 얹어 '설정한 최대치'가 되게 맞춥니다.
    ///   게임이 값을 바꿀 때마다(NetworkmaxPassivePoint setter) 보너스를 다시 계산하므로 합계는 설정값을 넘지 않습니다.
    ///   값은 게임이 원래 동기화하는 변수라 재능 탭·프리셋 화면에 그대로 표시됩니다.
    ///
    /// 과일 꼬치:
    ///   최대 개수는 세 곳(과일 꼬치 화면, 런 시작 시 서버 적용, 프리셋 화면)에서
    ///   '상수 fruitSkewerDefaultCount + 플레이어의 FRUITCOUNT 스탯'으로 계산됩니다.
    ///   상수를 설정값으로 올리고, 과일 꼬치 화면의 최대치는 정확히 설정값이 되게 맞춥니다.
    /// </summary>
    internal static class ProgressionPatches
    {
        private const string FruitConstKey = "fruitSkewerDefaultCount";

        // 아바타별로 이 모드가 얹은 보너스.
        private sealed class Bonus { public int Value; }
        private static readonly ConditionalWeakTable<object, Bonus> Bonuses = new ConditionalWeakTable<object, Bonus>();

        private static FieldInfo maxPassivePoint;
        private static PropertyInfo networkMaxPassivePoint;
        private static PropertyInfo isServer;
        private static FieldInfo multiplayerList;
        private static PropertyInfo spawnerAvatar;
        private static bool talentReady;
        private static bool disabled;

        [ThreadStatic]
        private static bool adjusting;

        [ThreadStatic]
        private static int readingFruitMax;

        private static bool loggedFailure;

        public static void Apply(Harmony harmony)
        {
            disabled = false;
            PatchTalentPoints(harmony);
            PatchFruitSkewer(harmony);
        }

        /// <summary>모드가 내려갈 때 얹어 둔 보너스를 걷어냅니다.</summary>
        public static void Remove()
        {
            disabled = true;
            Settings.TalentPointsChanged = null;
            ApplyToAllPlayers();
            talentReady = false;
        }

        // ───────────────────────── 재능 포인트 ─────────────────────────

        private static void PatchTalentPoints(Harmony harmony)
        {
            Type avatar = GameReflection.FindType("PlayerAvatar");
            Type spawner = GameReflection.FindType("PlayerSpawner");
            if (avatar != null)
            {
                maxPassivePoint = avatar.GetField("maxPassivePoint", GameReflection.Any);
                networkMaxPassivePoint = avatar.GetProperty("NetworkmaxPassivePoint", GameReflection.Any);
                isServer = avatar.GetProperty("isServer", BindingFlags.Public | BindingFlags.Instance);
            }
            if (spawner != null)
            {
                multiplayerList = spawner.GetField("MultiplayerList", GameReflection.Any);
                spawnerAvatar = spawner.GetProperty("PlayerAvatar", GameReflection.Any);
            }

            MethodInfo setter = networkMaxPassivePoint != null ? networkMaxPassivePoint.GetSetMethod(true) : null;
            if (maxPassivePoint == null || setter == null || isServer == null)
            {
                Log.Warn("PlayerAvatar.maxPassivePoint 를 찾지 못해 재능 포인트 최대치를 바꿀 수 없습니다.");
                return;
            }

            harmony.Patch(setter, postfix: new HarmonyMethod(typeof(ProgressionPatches), nameof(MaxPointSetterPostfix)));
            talentReady = true;
            Settings.TalentPointsChanged = ApplyToAllPlayers;
            Log.Info("재능 포인트 패치 적용 (최대치 설정 " + Settings.TalentPoints + ")");
        }

        // 게임이 최대치를 바꾼 직후(나무 상점 구매, 하드 모드 보상 등) 보너스를 다시 맞춥니다.
        private static void MaxPointSetterPostfix(object __instance)
        {
            if (!adjusting)
                Ensure(__instance);
        }

        /// <summary>플레이어가 스폰될 때(서버) 불립니다.</summary>
        public static void OnPlayerSpawned(object avatar)
        {
            Ensure(avatar);
        }

        public static void ApplyToAllPlayers()
        {
            if (!talentReady || multiplayerList == null || spawnerAvatar == null)
                return;
            try
            {
                var spawners = multiplayerList.GetValue(null) as IEnumerable;
                if (spawners == null)
                    return;
                foreach (object spawner in spawners.Cast<object>().ToArray())
                {
                    if (!GameReflection.IsNull(spawner))
                        Ensure(spawnerAvatar.GetValue(spawner, null));
                }
            }
            catch (Exception e)
            {
                LogFailureOnce("재능 포인트 적용", e);
            }
        }

        private static void Ensure(object avatar)
        {
            if (!talentReady || GameReflection.IsNull(avatar))
                return;
            try
            {
                // 최대치는 서버가 정하고 동기화합니다. 참가자 쪽에서는 건드리지 않습니다.
                if (!(bool)isServer.GetValue(avatar, null))
                    return;

                Bonus bonus = Bonuses.GetOrCreateValue(avatar);
                int current = (int)maxPassivePoint.GetValue(avatar);
                int vanilla = current - bonus.Value;
                int wanted = disabled ? 0 : Math.Max(0, Settings.TalentPoints - vanilla);
                if (wanted == bonus.Value)
                    return;

                adjusting = true;
                try
                {
                    networkMaxPassivePoint.SetValue(avatar, vanilla + wanted, null);
                    bonus.Value = wanted;
                }
                finally
                {
                    adjusting = false;
                }
            }
            catch (Exception e)
            {
                LogFailureOnce("재능 포인트 적용", e);
            }
        }

        // ───────────────────────── 과일 꼬치 ─────────────────────────

        private static void PatchFruitSkewer(Harmony harmony)
        {
            Type keywords = GameReflection.FindType("KeywordDatabase");
            Type panel = GameReflection.FindType("UI_FruitSkewerPanel");

            MethodInfo getConst = keywords != null
                ? AccessTools.GetDeclaredMethods(keywords).FirstOrDefault(m => m.Name == "GetConstValue" && m.ReturnType == typeof(int)
                    && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(string) && m.GetParameters()[0].Name == "key")
                : null;
            PropertyInfo maxProperty = panel != null ? panel.GetProperty("MaxSelectedFruitCount", GameReflection.Any) : null;
            MethodInfo maxGetter = maxProperty != null ? maxProperty.GetGetMethod(true) : null;

            if (getConst == null || maxGetter == null)
            {
                Log.Warn("과일 꼬치 최대 개수 계산을 찾지 못해 과일 꼬치 개수를 바꿀 수 없습니다.");
                return;
            }

            harmony.Patch(getConst, postfix: new HarmonyMethod(typeof(ProgressionPatches), nameof(ConstValuePostfix)) { priority = Priority.Last });
            harmony.Patch(maxGetter,
                prefix: new HarmonyMethod(typeof(ProgressionPatches), nameof(FruitMaxPrefix)),
                postfix: new HarmonyMethod(typeof(ProgressionPatches), nameof(FruitMaxPostfix)),
                finalizer: new HarmonyMethod(typeof(ProgressionPatches), nameof(FruitMaxFinalizer)));
            Log.Info("과일 꼬치 패치 적용 (최대 개수 설정 " + Settings.FruitSkewer + ")");
        }

        // 서버 적용과 프리셋 화면이 쓰는 상수. 여기에 플레이어 스탯이 더해지므로 상한은 설정값 이상이 됩니다.
        private static void ConstValuePostfix(string key, ref int __result)
        {
            if (readingFruitMax > 0 || key == null || key.Length != FruitConstKey.Length)
                return;
            int wanted = Settings.FruitSkewer;
            if (wanted > __result && string.Equals(key, FruitConstKey, StringComparison.OrdinalIgnoreCase))
                __result = wanted;
        }

        // 과일 꼬치 화면의 최대치는 게임 원래 값을 구한 뒤, 설정값과 비교해 큰 쪽으로 정합니다.
        private static void FruitMaxPrefix()
        {
            readingFruitMax++;
        }

        private static void FruitMaxPostfix(ref int __result)
        {
            if (Settings.FruitSkewer > __result)
                __result = Settings.FruitSkewer;
        }

        private static void FruitMaxFinalizer()
        {
            if (readingFruitMax > 0)
                readingFruitMax--;
        }

        private static void LogFailureOnce(string what, Exception e)
        {
            if (loggedFailure)
                return;
            loggedFailure = true;
            Log.Error(what + " 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
        }
    }
}
