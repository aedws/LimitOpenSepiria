using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace LimitOpenSepiria
{
    /// <summary>
    /// 소원 분수(게임 내부 이름: DimensionPocket) 패치.
    ///
    /// 공식 API(HorayModAPI)에는 소원 분수에 개입하는 이벤트가 없어 Harmony 로 패치합니다.
    /// 게임 내부 타입은 전부 이름으로 찾아(리플렉션) 접근하므로, 게임 업데이트로 대상이 사라지면
    /// 해당 기능만 꺼지고 로그에 경고가 남습니다.
    ///
    /// 게임의 분수 흐름과 패치 지점:
    ///  1. StartingFountain / UI_PresetPanel 이 '해금한 아티팩트' 중 조건에 맞는 것만 모아
    ///     UI_DimensionPocketPanel.Open(…, cachedDimensionPocketItem, …) 으로 넘깁니다.
    ///       → [OpenPrefix] 그 목록에 게임에 등록된 나머지 아이템을 덧붙입니다.
    ///  2. OnOpened 가 목록에서 Eternal·아티팩트 외 종류·'분수 선택 불가' 아이템을 걸러 아이콘을 만듭니다.
    ///       → [Relax] 실행되는 동안만 그 조건을 풀었다가 되돌립니다.
    ///       → [OnOpenedPostfix] 무기 불일치로 잠긴 아이콘을 풉니다.
    ///  3. HandleItemClick 이 클릭 시 '보상 불가/선택 불가/숨김'과 용량을 다시 검사합니다.
    ///       → [Relax] 위와 같습니다.
    ///  4. 용량은 클라이언트 UI 와 서버 지급(PlayerSpawner.AddDimensionPocketItemsOnServer)이 모두
    ///     UI_DimensionPocketPanel.GetCapacity(희귀도) 로 계산합니다.
    ///       → [CapacityPostfix] 결과를 0 으로 만들어 양쪽 계산을 함께 무효화합니다.
    ///
    /// 다른 모드와의 공존:
    ///  - 용량: 다른 모드(예: ModMaker 의 커스텀 희귀도 비용)가 계산한 뒤, 가장 마지막에 결과만 0 으로 덮습니다.
    ///  - 목록: 가장 먼저 실행해 목록을 채우므로, 그 뒤에 도는 다른 모드의 필터(예: ModMaker 의
    ///          '소원 분수 금지' 아이템 제외)는 그대로 적용됩니다. 아이템은 열 때마다 ItemDatabase 에서
    ///          읽으므로 다른 모드가 추가한 아이템도 자동으로 포함됩니다.
    /// </summary>
    internal static class FountainPatches
    {
        private const string PanelTypeName = "UI_DimensionPocketPanel";
        private const string PoolFieldName = "cachedDimensionPocketItem";

        private static FieldInfo poolField;
        private static FieldInfo weaponCheckField;
        private static FieldInfo selectedIconsField;
        private static FieldInfo storageField;
        private static MethodInfo updateCapacityText;
        private static MethodInfo sortingIcons;

        private static bool loggedOpen;
        private static bool loggedFailure;

        public static void Apply(Harmony harmony)
        {
            Type panel = GameReflection.FindType(PanelTypeName);
            if (panel == null)
            {
                Log.Warn(PanelTypeName + " 타입을 찾지 못했습니다. 게임이 업데이트되어 모드 수정이 필요합니다.");
                return;
            }

            PatchCapacity(harmony, panel);
            PatchItemPool(harmony, panel);
        }

        // ───────────────────────── 용량 제한 해제 ─────────────────────────

        private static void PatchCapacity(Harmony harmony, Type panel)
        {
            List<MethodInfo> targets = AccessTools.GetDeclaredMethods(panel)
                .Where(m => m.Name == "GetCapacity" && m.ReturnType == typeof(int) && !m.IsAbstract && !m.IsGenericMethodDefinition)
                .ToList();

            if (targets.Count == 0)
            {
                Log.Warn(PanelTypeName + ".GetCapacity 를 찾지 못해 용량 제한 해제를 쓸 수 없습니다.");
                return;
            }

            var postfix = new HarmonyMethod(typeof(FountainPatches), nameof(CapacityPostfix)) { priority = Priority.Last };
            foreach (MethodInfo target in targets)
            {
                harmony.Patch(target, postfix: postfix);
                Log.Info("용량 패치 적용: " + GameReflection.Describe(target));
            }
        }

        // 아이템 한 개가 차지하는 용량(희귀도별 칸 수)을 0 으로 만들어 용량 계산 자체를 무효화합니다.
        private static void CapacityPostfix(ref int __result)
        {
            if (Settings.UnlimitedCapacity)
                __result = 0;
        }

        // ───────────────────────── 모든 아이템 선택 ─────────────────────────

        private static void PatchItemPool(Harmony harmony, Type panel)
        {
            string missing;
            if (!GameReflection.Initialize(out missing))
            {
                Log.Warn("게임 내부 멤버를 찾지 못해 '모든 아이템 선택'을 쓸 수 없습니다: " + missing);
                return;
            }

            Type listType = typeof(List<>).MakeGenericType(GameReflection.ItemMetadataType);
            MethodInfo open = AccessTools.GetDeclaredMethods(panel).FirstOrDefault(m => m.Name == "Open"
                && m.GetParameters().Any(p => p.ParameterType == listType && p.Name == PoolFieldName));
            MethodInfo onOpened = AccessTools.DeclaredMethod(panel, "OnOpened", Type.EmptyTypes);
            MethodInfo handleItemClick = AccessTools.GetDeclaredMethods(panel).FirstOrDefault(m => m.Name == "HandleItemClick");

            poolField = panel.GetField(PoolFieldName, GameReflection.Any);
            weaponCheckField = panel.GetField("iconCharmWeaponCheckList", GameReflection.Any);
            selectedIconsField = panel.GetField("selectedVaultIcons", GameReflection.Any);
            storageField = panel.GetField("cachedDimensionPocketStorage", GameReflection.Any);
            updateCapacityText = AccessTools.GetDeclaredMethods(panel).FirstOrDefault(m => m.Name == "UpdateCapacityText" && m.GetParameters().Length == 2);
            sortingIcons = AccessTools.DeclaredMethod(panel, "SortingIcons", Type.EmptyTypes);

            if (open == null || onOpened == null || handleItemClick == null || poolField == null)
            {
                Log.Warn("소원 분수 패널의 Open/OnOpened/HandleItemClick 을 찾지 못해 '모든 아이템 선택'을 쓸 수 없습니다.");
                return;
            }

            harmony.Patch(open,
                prefix: new HarmonyMethod(typeof(FountainPatches), nameof(OpenPrefix)) { priority = Priority.First });

            // 조건을 푸는 쪽은 다른 모드의 패치보다 바깥에서 감싸도록 가장 먼저 시작하고 가장 나중에 되돌립니다.
            harmony.Patch(onOpened,
                prefix: new HarmonyMethod(typeof(FountainPatches), nameof(ListingPrefix)) { priority = Priority.First },
                postfix: new HarmonyMethod(typeof(FountainPatches), nameof(OnOpenedPostfix)),
                finalizer: new HarmonyMethod(typeof(FountainPatches), nameof(RestoreFinalizer)) { priority = Priority.Last });

            harmony.Patch(handleItemClick,
                prefix: new HarmonyMethod(typeof(FountainPatches), nameof(ClickPrefix)) { priority = Priority.First },
                finalizer: new HarmonyMethod(typeof(FountainPatches), nameof(RestoreFinalizer)) { priority = Priority.Last });

            Log.Info("목록 패치 적용: " + GameReflection.Describe(open) + ", OnOpened, HandleItemClick");
        }

        // 패널이 열릴 때 넘어오는 '선택 가능 목록'에, 게임(및 다른 모드)에 등록된 나머지 아이템을 덧붙입니다.
        private static void OpenPrefix(MethodBase __originalMethod, object[] __args)
        {
            try
            {
                ParameterInfo[] parameters = __originalMethod.GetParameters();
                int poolIndex = Array.FindIndex(parameters, p => p.Name == PoolFieldName);
                if (poolIndex < 0)
                    return;

                var original = __args[poolIndex] as IList;
                if (!loggedOpen)
                {
                    loggedOpen = true;
                    Log.Info("소원 분수 열림: 기본 목록 " + (original != null ? original.Count : 0) + "개, 설정: 용량해제="
                             + Settings.UnlimitedCapacity + ", 모든아이템=" + Settings.AllItems + ", 기타종류="
                             + Settings.IncludeOtherTypes + ", 숨김=" + Settings.IncludeHidden);
                }

                if (!Settings.AllItems)
                    return;

                IList expanded = ExpandPool(parameters[poolIndex].ParameterType, original);
                if (expanded != null)
                    __args[poolIndex] = expanded;
            }
            catch (Exception e)
            {
                LogFailureOnce("목록 확장", e);
            }
        }

        // 호출자가 만든 리스트는 건드리지 않고, 새 리스트를 만들어 넘깁니다.
        private static IList ExpandPool(Type listType, IList original)
        {
            var result = (IList)Activator.CreateInstance(listType);
            var present = new HashSet<int>();

            if (original != null)
            {
                foreach (object entry in original)
                {
                    result.Add(entry);
                    present.Add(GameReflection.GetEntityId(entry));
                }
            }

            int[] allIds = GameReflection.GetAllItemIds();
            Array.Sort(allIds);

            int added = 0;
            foreach (int id in allIds)
            {
                if (present.Contains(id))
                    continue;

                object entity = GameReflection.FindItem(id);
                if (entity == null || !GameReflection.HasPrefab(entity))
                    continue;
                if (!Settings.IncludeOtherTypes && !GameReflection.IsCharm(entity))
                    continue;
                if (!Settings.IncludeHidden && GameReflection.IsHiddenOrDisabled(entity))
                    continue;

                result.Add(GameReflection.CreateItemMetadata(id));
                present.Add(id);
                added++;
            }

            if (added == 0)
                return null;

            Log.Info("소원 분수 목록에 아이템 " + added + "개 추가 (기존 " + (original != null ? original.Count : 0) + "개).");
            return result;
        }

        private static void ListingPrefix(object __instance, out List<GameReflection.Restore> __state)
        {
            __state = RelaxPool(__instance, true);
        }

        private static void ClickPrefix(object __instance, out List<GameReflection.Restore> __state)
        {
            __state = RelaxPool(__instance, false);
        }

        // 원본 메서드가 예외로 끝나도 반드시 되돌립니다. 예외는 그대로 흘려보냅니다.
        private static void RestoreFinalizer(List<GameReflection.Restore> __state)
        {
            if (__state == null)
                return;
            for (int i = __state.Count - 1; i >= 0; i--)
            {
                try
                {
                    __state[i].Apply();
                }
                catch (Exception e)
                {
                    LogFailureOnce("아이템 조건 복원", e);
                }
            }
        }

        // 패널이 목록의 아이템을 걸러내는 조건을, 원본 메서드가 도는 동안만 풉니다.
        private static List<GameReflection.Restore> RelaxPool(object panel, bool forListing)
        {
            if (!Settings.AllItems)
                return null;

            var restores = new List<GameReflection.Restore>();
            try
            {
                var pool = poolField.GetValue(panel) as IList;
                if (pool == null)
                    return restores;

                var seen = new HashSet<int>();
                foreach (object entry in pool)
                {
                    int id = GameReflection.GetEntityId(entry);
                    if (!seen.Add(id))
                        continue;
                    object entity = GameReflection.FindItem(id);
                    if (entity != null)
                        GameReflection.Relax(entity, forListing, Settings.IncludeOtherTypes, restores);
                }
            }
            catch (Exception e)
            {
                LogFailureOnce("아이템 조건 해제", e);
            }
            return restores;
        }

        // 패널은 '현재 무기와 맞지 않는 무기 전용 아티팩트'와 아티팩트가 아닌 아이템을 선택 불가로 표시합니다.
        // 그 표시를 풀고, 표시를 다시 그리게 합니다.
        private static void OnOpenedPostfix(object __instance)
        {
            if (!Settings.AllItems || weaponCheckField == null)
                return;

            try
            {
                var checks = weaponCheckField.GetValue(__instance) as IDictionary;
                if (checks == null || checks.Count == 0)
                    return;

                foreach (object icon in new ArrayList(checks.Keys))
                    checks[icon] = true;

                if (updateCapacityText != null && selectedIconsField != null && storageField != null)
                    updateCapacityText.Invoke(__instance, new[] { selectedIconsField.GetValue(__instance), storageField.GetValue(__instance) });
                if (sortingIcons != null)
                    sortingIcons.Invoke(__instance, null);
            }
            catch (Exception e)
            {
                LogFailureOnce("무기 조건 해제", e);
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
