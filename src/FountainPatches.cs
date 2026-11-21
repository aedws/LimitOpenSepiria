using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    ///       → [OpenPrefix] 그 목록에 게임에 등록된 나머지 아이템을 전부 덧붙여 넘깁니다(설정과 무관).
    ///         다른 모드의 필터가 전체 목록에 적용되게 하기 위해서입니다.
    ///  2. OnOpened 가 목록에서 Eternal·아티팩트 외 종류·'분수 선택 불가' 아이템을 걸러 아이콘을 만듭니다.
    ///       → [ListingPrefix] 현재 설정에 맞는 아이템만 남기고, 실행되는 동안만 걸러내는 조건을 풉니다.
    ///       → [OnOpenedPostfix] 무기 불일치 잠금을 풀고, 분수 화면의 모드 버튼과 용량 표시를 맞춥니다.
    ///  3. HandleItemClick 이 클릭 시 '보상 불가/선택 불가/숨김'과 용량을 다시 검사합니다.
    ///       → [ClickPrefix] 실행되는 동안만 조건을 풉니다.
    ///  4. 용량은 클라이언트 UI 와 서버 지급(PlayerSpawner.AddDimensionPocketItemsOnServer)이 모두
    ///     UI_DimensionPocketPanel.GetCapacity(희귀도) 로 계산합니다.
    ///       → [CapacityPostfix] 결과를 0 으로 만들어 양쪽 계산을 함께 무효화합니다.
    ///         아이콘 아래 용량 표시(SetCost)를 그릴 때만 1 을 돌려줘 등급색 원 1개로 보이게 합니다.
    ///
    /// 다른 모드와의 공존:
    ///  - 용량: 다른 모드(예: ModMaker 의 커스텀 희귀도 비용)가 계산한 뒤, 가장 마지막에 결과만 덮습니다.
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
        private static FieldInfo currentSelectedField;
        private static MethodInfo updateCapacityText;
        private static MethodInfo sortingIcons;
        private static MethodInfo onOpenedMethod;

        // 패널별 상태: 다른 모드의 필터까지 거친 전체 목록과, 그중 게임이 원래 넣어 준 아이템.
        private sealed class PanelState
        {
            public HashSet<int> VanillaIds = new HashSet<int>();
            public bool FreshOpen;
            public IList FullPool;
        }

        private static readonly ConditionalWeakTable<object, PanelState> States = new ConditionalWeakTable<object, PanelState>();

        [ThreadStatic]
        private static int drawingCost;

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
                harmony.Patch(target, postfix: postfix);

            // 아이콘 아래의 용량 표시는 '용량 × 5 + 1' 픽셀 너비로 그려집니다(원 1개 = 5픽셀).
            Type icon = GameReflection.FindType("UI_DimensionVaultItemIcon");
            MethodInfo setCost = icon != null ? AccessTools.GetDeclaredMethods(icon).FirstOrDefault(m => m.Name == "SetCost") : null;
            if (setCost != null)
            {
                harmony.Patch(setCost,
                    prefix: new HarmonyMethod(typeof(FountainPatches), nameof(SetCostPrefix)),
                    finalizer: new HarmonyMethod(typeof(FountainPatches), nameof(SetCostFinalizer)));
            }
            else
            {
                Log.Warn("UI_DimensionVaultItemIcon.SetCost 를 찾지 못해 용량 표시가 점으로 보일 수 있습니다.");
            }

            Log.Info("용량 패치 적용: " + GameReflection.Describe(targets[0]));
        }

        // 아이템 한 개가 차지하는 용량(희귀도별 칸 수)을 0 으로 만들어 용량 계산 자체를 무효화합니다.
        private static void CapacityPostfix(ref int __result)
        {
            if (Settings.UnlimitedCapacity)
                __result = drawingCost > 0 ? 1 : 0;
        }

        private static void SetCostPrefix()
        {
            drawingCost++;
        }

        private static void SetCostFinalizer()
        {
            if (drawingCost > 0)
                drawingCost--;
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
            onOpenedMethod = AccessTools.DeclaredMethod(panel, "OnOpened", Type.EmptyTypes);
            MethodInfo handleItemClick = AccessTools.GetDeclaredMethods(panel).FirstOrDefault(m => m.Name == "HandleItemClick");

            poolField = panel.GetField(PoolFieldName, GameReflection.Any);
            weaponCheckField = panel.GetField("iconCharmWeaponCheckList", GameReflection.Any);
            selectedIconsField = panel.GetField("selectedVaultIcons", GameReflection.Any);
            storageField = panel.GetField("cachedDimensionPocketStorage", GameReflection.Any);
            currentSelectedField = panel.GetField("currentSelectedItem", GameReflection.Any);
            updateCapacityText = AccessTools.GetDeclaredMethods(panel).FirstOrDefault(m => m.Name == "UpdateCapacityText" && m.GetParameters().Length == 2);
            sortingIcons = AccessTools.DeclaredMethod(panel, "SortingIcons", Type.EmptyTypes);

            if (open == null || onOpenedMethod == null || handleItemClick == null || poolField == null)
            {
                Log.Warn("소원 분수 패널의 Open/OnOpened/HandleItemClick 을 찾지 못해 '모든 아이템 선택'을 쓸 수 없습니다.");
                return;
            }

            FountainUi.Initialize(panel);

            harmony.Patch(open,
                prefix: new HarmonyMethod(typeof(FountainPatches), nameof(OpenPrefix)) { priority = Priority.First });

            // 조건을 푸는 쪽은 다른 모드의 패치보다 바깥에서 감싸도록 가장 먼저 시작하고 가장 나중에 되돌립니다.
            harmony.Patch(onOpenedMethod,
                prefix: new HarmonyMethod(typeof(FountainPatches), nameof(ListingPrefix)) { priority = Priority.First },
                postfix: new HarmonyMethod(typeof(FountainPatches), nameof(OnOpenedPostfix)),
                finalizer: new HarmonyMethod(typeof(FountainPatches), nameof(RestoreFinalizer)) { priority = Priority.Last });

            harmony.Patch(handleItemClick,
                prefix: new HarmonyMethod(typeof(FountainPatches), nameof(ClickPrefix)) { priority = Priority.First },
                finalizer: new HarmonyMethod(typeof(FountainPatches), nameof(RestoreFinalizer)) { priority = Priority.Last });

            Log.Info("목록 패치 적용: " + GameReflection.Describe(open) + ", OnOpened, HandleItemClick");
        }

        // 패널이 열릴 때 넘어오는 '선택 가능 목록'에, 게임(및 다른 모드)에 등록된 나머지 아이템을 덧붙입니다.
        // 무엇을 실제로 보여줄지는 ListingPrefix 가 현재 설정으로 정합니다.
        private static void OpenPrefix(object __instance, MethodBase __originalMethod, object[] __args)
        {
            try
            {
                ParameterInfo[] parameters = __originalMethod.GetParameters();
                int poolIndex = Array.FindIndex(parameters, p => p.Name == PoolFieldName);
                if (poolIndex < 0)
                    return;

                var original = __args[poolIndex] as IList;
                PanelState state = States.GetOrCreateValue(__instance);
                state.VanillaIds.Clear();
                state.FullPool = null;
                state.FreshOpen = true;

                var result = (IList)Activator.CreateInstance(parameters[poolIndex].ParameterType);
                if (original != null)
                {
                    foreach (object entry in original)
                    {
                        result.Add(entry);
                        state.VanillaIds.Add(GameReflection.GetEntityId(entry));
                    }
                }

                int[] allIds = GameReflection.GetAllItemIds();
                Array.Sort(allIds);
                foreach (int id in allIds)
                {
                    if (state.VanillaIds.Contains(id))
                        continue;
                    object entity = GameReflection.FindItem(id);
                    // 패널은 아이템의 resourcePrefab 을 검사 없이 읽으므로, 없는 아이템은 넣으면 안 됩니다.
                    if (entity == null || !GameReflection.HasPrefab(entity))
                        continue;
                    result.Add(GameReflection.CreateItemMetadata(id));
                }

                if (!loggedOpen)
                {
                    loggedOpen = true;
                    Log.Info("소원 분수 열림: 기본 목록 " + state.VanillaIds.Count + "개, 전체 후보 " + result.Count + "개, 설정: 용량해제="
                             + Settings.UnlimitedCapacity + ", 모든아이템=" + Settings.AllItems + ", 기타종류="
                             + Settings.IncludeOtherTypes + ", 숨김=" + Settings.IncludeHidden);
                }

                __args[poolIndex] = result;
            }
            catch (Exception e)
            {
                LogFailureOnce("목록 확장", e);
            }
        }

        private static void ListingPrefix(object __instance, out List<GameReflection.Restore> __state)
        {
            try
            {
                ApplyPoolFilter(__instance);
            }
            catch (Exception e)
            {
                LogFailureOnce("목록 구성", e);
            }
            __state = RelaxPool(__instance, true);
        }

        // 전체 후보 중 현재 설정에서 보여줄 아이템만 패널의 목록으로 넣습니다.
        private static void ApplyPoolFilter(object panel)
        {
            PanelState state;
            if (!States.TryGetValue(panel, out state))
                return;

            if (state.FreshOpen)
            {
                // Open 의 인자가 다른 모드의 필터까지 거쳐 패널에 저장된 상태입니다.
                state.FreshOpen = false;
                state.FullPool = poolField.GetValue(panel) as IList;
            }

            if (state.FullPool == null)
                return;

            var visible = (IList)Activator.CreateInstance(state.FullPool.GetType());
            foreach (object entry in state.FullPool)
            {
                int id = GameReflection.GetEntityId(entry);
                if (state.VanillaIds.Contains(id))
                {
                    visible.Add(entry);
                    continue;
                }
                if (!Settings.AllItems)
                    continue;

                object entity = GameReflection.FindItem(id);
                if (entity == null)
                    continue;
                if (!Settings.IncludeOtherTypes && !GameReflection.IsCharm(entity))
                    continue;
                if (!Settings.IncludeHidden && GameReflection.IsHiddenOrDisabled(entity))
                    continue;
                visible.Add(entry);
            }

            poolField.SetValue(panel, visible);
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

        private static void OnOpenedPostfix(object __instance)
        {
            try
            {
                if (Settings.AllItems)
                    UnlockWeaponChecks(__instance);
            }
            catch (Exception e)
            {
                LogFailureOnce("무기 조건 해제", e);
            }

            FountainUi.Sync(__instance);
        }

        // 패널은 '현재 무기와 맞지 않는 무기 전용 아티팩트'와 아티팩트가 아닌 아이템을 선택 불가로 표시합니다.
        // 그 표시를 풀고, 표시를 다시 그리게 합니다.
        private static void UnlockWeaponChecks(object panel)
        {
            if (weaponCheckField == null)
                return;
            var checks = weaponCheckField.GetValue(panel) as IDictionary;
            if (checks == null || checks.Count == 0)
                return;

            foreach (object icon in new ArrayList(checks.Keys))
                checks[icon] = true;

            if (updateCapacityText != null && selectedIconsField != null && storageField != null)
                updateCapacityText.Invoke(panel, new[] { selectedIconsField.GetValue(panel), storageField.GetValue(panel) });
            if (sortingIcons != null)
                sortingIcons.Invoke(panel, null);
        }

        /// <summary>설정이 바뀐 뒤, 지금까지 고른 아이템을 유지한 채 열려 있는 분수 화면을 다시 구성합니다.</summary>
        public static void Rebuild(object panel)
        {
            try
            {
                var selectedIcons = selectedIconsField != null ? selectedIconsField.GetValue(panel) as IList : null;
                if (selectedIcons != null && currentSelectedField != null)
                {
                    var selection = (IList)Activator.CreateInstance(currentSelectedField.FieldType);
                    foreach (object icon in selectedIcons)
                    {
                        object metadata = FountainUi.GetIconItem(icon);
                        if (metadata != null)
                            selection.Add(metadata);
                    }
                    currentSelectedField.SetValue(panel, selection);
                }

                onOpenedMethod.Invoke(panel, null);
            }
            catch (Exception e)
            {
                LogFailureOnce("분수 화면 새로 고침", e);
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
