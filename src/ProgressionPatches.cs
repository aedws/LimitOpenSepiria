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
    ///   카테고리마다 플러스 과일(드랍률 증가)과 마이너스 과일(드랍률 감소, 겹치면 등장 금지)을 고르는데,
    ///   게임은 둘을 합친 개수를 '상수 fruitSkewerDefaultCount + 플레이어의 FRUITCOUNT 스탯'으로 제한합니다.
    ///   이 모드는 마이너스 과일을 그 개수에서 빼 줍니다. 플러스 과일의 개수 제한과,
    ///   카테고리 하나에 겹칠 수 있는 횟수 제한은 그대로입니다.
    ///     - 과일 꼬치 화면: 최대 개수를 '게임 값 + 고른 마이너스 과일 수'로 돌려줍니다.
    ///     - 런 시작 시 서버 적용(DungeonManager.LoadStageAndMove)과 프리셋 화면: 앞에서부터 최대 개수만큼만
    ///       적용/표시하므로, 그 계산 동안만 상수를 크게 돌려줘 고른 과일이 모두 적용되게 합니다.
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

        // 재능별 레벨 상한: 원래 값을 기억해 두었다가 되돌립니다.
        private const int LiftedLevelCap = 9999;
        private static FieldInfo passiveData;
        private static FieldInfo passiveMaxLevel;
        private static readonly System.Collections.Generic.Dictionary<object, int> OriginalMaxLevels = new System.Collections.Generic.Dictionary<object, int>();

        private static MethodInfo fruitMaxGetter;

        [ThreadStatic]
        private static bool adjusting;


        [ThreadStatic]
        private static int readingFruitMax;

        [ThreadStatic]
        private static int applyingFruits;

        [ThreadStatic]
        private static bool addingMinusFruit;

        private const int FruitConstBoost = 10000;
        private static MethodInfo getConstValue;
        private static FieldInfo fruitSelected;
        private static FieldInfo fruitDataValue;
        private static FieldInfo fruitIcons;
        private static FieldInfo fruitPlusCounts;
        private static FieldInfo fruitMinusCounts;
        private static FieldInfo iconPlusButton;
        private static FieldInfo iconMinusButton;
        private static MethodInfo iconSetLimitMessageBox;
        private static PropertyInfo buttonInteractable;

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
            Settings.TalentLevelCapChanged = null;
            ApplyToAllPlayers();
            ApplyLevelCap();
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
            PatchLevelCap(harmony, avatar);
            talentReady = true;
            Settings.TalentPointsChanged = ApplyToAllPlayers;
            Log.Info("재능 포인트 패치 적용 (최대치 설정 " + Settings.TalentPoints + ")");
        }

        // 재능 하나의 레벨 상한은 서버가 포인트를 더할 때 PassiveEntity.maxLevel(기본 20)로만 검사합니다.
        // 효과는 '레벨 × 기본값'으로 계산되므로 상한을 올려도 계산에는 문제가 없습니다.
        private static void PatchLevelCap(Harmony harmony, Type avatar)
        {
            Type database = GameReflection.FindType("PassiveDatabase");
            Type entity = GameReflection.FindType("PassiveEntity");
            passiveData = database != null ? database.GetField("data", GameReflection.Any) : null;
            passiveMaxLevel = entity != null ? entity.GetField("maxLevel", GameReflection.Any) : null;
            MethodInfo addOnServer = AccessTools.GetDeclaredMethods(avatar).FirstOrDefault(m => m.Name == "AddPassiveStatOnServer");

            if (passiveData == null || passiveMaxLevel == null || addOnServer == null)
            {
                Log.Warn("재능 레벨 상한 검사를 찾지 못해 레벨 상한 해제를 쓸 수 없습니다.");
                passiveData = null;
                return;
            }

            // 저장된 재능을 불러올 때도 이 함수를 거치므로, 실행 직전에 상한을 설정에 맞춰 둡니다.
            harmony.Patch(addOnServer, prefix: new HarmonyMethod(typeof(ProgressionPatches), nameof(ApplyLevelCap)));
            Settings.TalentLevelCapChanged = ApplyLevelCap;
        }

        public static void ApplyLevelCap()
        {
            if (passiveData == null)
                return;
            try
            {
                var data = passiveData.GetValue(null) as IDictionary;
                if (data == null)
                    return;
                bool lift = !disabled && Settings.UnlimitedTalentLevel;
                foreach (object entity in data.Values)
                {
                    if (GameReflection.IsNull(entity))
                        continue;
                    int current = (int)passiveMaxLevel.GetValue(entity);
                    int original;
                    if (!OriginalMaxLevels.TryGetValue(entity, out original))
                    {
                        if (!lift)
                            continue;
                        original = current;
                        OriginalMaxLevels[entity] = original;
                    }
                    int wanted = lift ? Math.Max(original, LiftedLevelCap) : original;
                    if (current != wanted)
                        passiveMaxLevel.SetValue(entity, wanted);
                }
            }
            catch (Exception e)
            {
                LogFailureOnce("재능 레벨 상한 적용", e);
            }
        }

        /// <summary>이 모드의 보너스를 뺀, 게임이 준 재능 포인트 최대치.</summary>
        public static int VanillaTalentMax(object avatar)
        {
            int current = (int)maxPassivePoint.GetValue(avatar);
            Bonus bonus;
            return Bonuses.TryGetValue(avatar, out bonus) ? current - bonus.Value : current;
        }

        public static int CurrentTalentMax(object avatar)
        {
            return (int)maxPassivePoint.GetValue(avatar);
        }

        public static bool CanAdjustTalent(object avatar)
        {
            return talentReady && !GameReflection.IsNull(avatar) && (bool)isServer.GetValue(avatar, null);
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
            Type icon = GameReflection.FindType("UI_FruitSkewerCategoryIcon");
            Type dungeon = GameReflection.FindType("DungeonManager");
            Type preset = GameReflection.FindType("UI_PresetPanel");

            getConstValue = keywords != null
                ? AccessTools.GetDeclaredMethods(keywords).FirstOrDefault(m => m.Name == "GetConstValue" && m.ReturnType == typeof(int)
                    && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(string) && m.GetParameters()[0].Name == "key")
                : null;
            PropertyInfo maxProperty = panel != null ? panel.GetProperty("MaxSelectedFruitCount", GameReflection.Any) : null;
            fruitMaxGetter = maxProperty != null ? maxProperty.GetGetMethod(true) : null;
            MethodInfo addFruit = panel != null ? AccessTools.GetDeclaredMethods(panel).FirstOrDefault(m => m.Name == "AddFruit"
                && m.GetParameters().Length == 3 && m.GetParameters()[1].Name == "value") : null;
            MethodInfo refresh = panel != null ? AccessTools.DeclaredMethod(panel, "RefreshCategoryButtonLockState", Type.EmptyTypes) : null;
            MethodInfo applyOnServer = dungeon != null ? AccessTools.GetDeclaredMethods(dungeon).FirstOrDefault(m => m.Name == "LoadStageAndMove") : null;
            MethodInfo presetInfo = preset != null ? AccessTools.GetDeclaredMethods(preset).FirstOrDefault(m => m.Name == "UpdateFruitSkewerInfo") : null;

            if (panel != null)
            {
                fruitSelected = panel.GetField("selectedFruitDataPairList", GameReflection.Any);
                fruitIcons = panel.GetField("fruitIconList", GameReflection.Any);
                fruitPlusCounts = panel.GetField("categoryPlusSelectedCount", GameReflection.Any);
                fruitMinusCounts = panel.GetField("categoryMinusSelectedCount", GameReflection.Any);
                Type data = panel.GetNestedType("FruitData", GameReflection.Any);
                fruitDataValue = data != null ? data.GetField("value", GameReflection.Any) : null;
            }
            if (icon != null)
            {
                iconPlusButton = icon.GetField("plusButton", GameReflection.Any);
                iconMinusButton = icon.GetField("minusButton", GameReflection.Any);
                iconSetLimitMessageBox = AccessTools.GetDeclaredMethods(icon).FirstOrDefault(m => m.Name == "SetLimitMessageBox");
                buttonInteractable = iconMinusButton != null
                    ? iconMinusButton.FieldType.GetProperty("interactable", BindingFlags.Public | BindingFlags.Instance) : null;
            }

            if (getConstValue == null || fruitMaxGetter == null || addFruit == null || refresh == null || applyOnServer == null
                || fruitSelected == null || fruitDataValue == null || fruitIcons == null || fruitPlusCounts == null || fruitMinusCounts == null
                || iconMinusButton == null || buttonInteractable == null)
            {
                Log.Warn("과일 꼬치 화면의 구성 요소를 찾지 못해 감소·금지 개수 제한 해제를 쓸 수 없습니다.");
                fruitMaxGetter = null;
                return;
            }

            harmony.Patch(getConstValue, postfix: new HarmonyMethod(typeof(ProgressionPatches), nameof(ConstValuePostfix)) { priority = Priority.Last });
            harmony.Patch(fruitMaxGetter,
                prefix: new HarmonyMethod(typeof(ProgressionPatches), nameof(FruitMaxPrefix)),
                postfix: new HarmonyMethod(typeof(ProgressionPatches), nameof(FruitMaxPostfix)),
                finalizer: new HarmonyMethod(typeof(ProgressionPatches), nameof(FruitMaxFinalizer)));
            harmony.Patch(addFruit,
                prefix: new HarmonyMethod(typeof(ProgressionPatches), nameof(AddFruitPrefix)),
                finalizer: new HarmonyMethod(typeof(ProgressionPatches), nameof(MinusFlagFinalizer)));
            harmony.Patch(refresh,
                prefix: new HarmonyMethod(typeof(ProgressionPatches), nameof(RefreshLockPrefix)),
                postfix: new HarmonyMethod(typeof(ProgressionPatches), nameof(RefreshLockPostfix)),
                finalizer: new HarmonyMethod(typeof(ProgressionPatches), nameof(MinusFlagFinalizer)));

            var scopePrefix = new HarmonyMethod(typeof(ProgressionPatches), nameof(ApplyScopePrefix));
            var scopeFinalizer = new HarmonyMethod(typeof(ProgressionPatches), nameof(ApplyScopeFinalizer));
            harmony.Patch(applyOnServer, prefix: scopePrefix, finalizer: scopeFinalizer);
            if (presetInfo != null)
                harmony.Patch(presetInfo, prefix: scopePrefix, finalizer: scopeFinalizer);

            Log.Info("과일 꼬치 패치 적용 (감소·금지 제한 해제=" + Settings.UnlimitedFruitBans + ")");
        }

        public static bool FruitReady { get { return fruitMaxGetter != null; } }

        // 서버 적용과 프리셋 화면이 과일을 세는 동안에만 상수를 크게 돌려줍니다.
        private static void ApplyScopePrefix()
        {
            applyingFruits++;
        }

        private static void ApplyScopeFinalizer()
        {
            if (applyingFruits > 0)
                applyingFruits--;
        }

        private static void ConstValuePostfix(string key, ref int __result)
        {
            if (applyingFruits <= 0 || readingFruitMax > 0 || !Settings.UnlimitedFruitBans || key == null || key.Length != FruitConstKey.Length)
                return;
            if (string.Equals(key, FruitConstKey, StringComparison.OrdinalIgnoreCase))
                __result += FruitConstBoost;
        }

        // 과일 꼬치 화면의 최대 개수 = 게임 값 + 고른 마이너스 과일 수.
        // 마이너스 과일을 추가하는 중에는 한 칸을 더 열어, 플러스가 가득 차 있어도 추가되게 합니다.
        private static void FruitMaxPrefix()
        {
            readingFruitMax++;
        }

        private static void FruitMaxPostfix(object __instance, ref int __result)
        {
            if (!Settings.UnlimitedFruitBans)
                return;
            try
            {
                __result += CountMinusFruits(__instance) + (addingMinusFruit ? 1 : 0);
            }
            catch (Exception e)
            {
                LogFailureOnce("과일 꼬치 개수 계산", e);
            }
        }

        private static void FruitMaxFinalizer()
        {
            if (readingFruitMax > 0)
                readingFruitMax--;
        }

        private static int CountMinusFruits(object panel)
        {
            var selected = fruitSelected.GetValue(panel) as IDictionary;
            if (selected == null)
                return 0;
            int count = 0;
            foreach (object data in selected.Keys)
            {
                if (data != null && (int)fruitDataValue.GetValue(data) < 0)
                    count++;
            }
            return count;
        }

        private static void AddFruitPrefix(int value, out bool __state)
        {
            __state = addingMinusFruit;
            addingMinusFruit = value < 0 && Settings.UnlimitedFruitBans;
        }

        // 버튼 잠금 상태는 '추가 중' 표시 없이 계산해야 플러스 쪽 잠금이 게임과 같게 나옵니다.
        private static void RefreshLockPrefix(out bool __state)
        {
            __state = addingMinusFruit;
            addingMinusFruit = false;
        }

        private static void MinusFlagFinalizer(bool __state)
        {
            addingMinusFruit = __state;
        }

        // 게임은 전체 개수가 가득 차면 마이너스 버튼까지 잠급니다. 마이너스 버튼은 그 카테고리에
        // 더 겹칠 수 있으면(또는 플러스 과일을 상쇄할 수 있으면) 항상 누를 수 있게 되돌립니다.
        private static void RefreshLockPostfix(object __instance)
        {
            if (!Settings.UnlimitedFruitBans)
                return;
            try
            {
                var icons = fruitIcons.GetValue(__instance) as IDictionary;
                var plus = fruitPlusCounts.GetValue(__instance) as System.Collections.Generic.Dictionary<string, int>;
                var minus = fruitMinusCounts.GetValue(__instance) as System.Collections.Generic.Dictionary<string, int>;
                if (icons == null || plus == null || minus == null)
                    return;

                int minusLimit = (int)getConstValue.Invoke(null, new object[] { "fruitSkewerMinusLimitCount", true });
                int plusLimit = (int)getConstValue.Invoke(null, new object[] { "fruitSkewerPlusLimitCount", true });
                foreach (DictionaryEntry entry in icons)
                {
                    var category = entry.Key as string;
                    if (category == null || GameReflection.IsNull(entry.Value))
                        continue;
                    int plusCount, minusCount;
                    plus.TryGetValue(category, out plusCount);
                    minus.TryGetValue(category, out minusCount);

                    object button = iconMinusButton.GetValue(entry.Value);
                    if (!GameReflection.IsNull(button))
                        buttonInteractable.SetValue(button, minusCount < minusLimit || plusCount > 0, null);

                    // '한도' 표시는 이 카테고리가 겹침 한도에 닿았을 때만 남깁니다.
                    if (iconSetLimitMessageBox != null)
                        iconSetLimitMessageBox.Invoke(entry.Value, new object[] { plusCount >= plusLimit || minusCount >= minusLimit });
                }
            }
            catch (Exception e)
            {
                LogFailureOnce("과일 꼬치 버튼 잠금 조정", e);
            }
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
