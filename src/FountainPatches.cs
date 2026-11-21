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
    /// 해당 기능만 꺼지고 로그에 경고가 남습니다. 게임이 깨지지는 않습니다.
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
        private const string CapacityMethodName = "GetCapacity";
        private const string OpenMethodName = "Open";
        private const string PoolParameterName = "cachedDimensionPocketItem";

        private static Type itemMetadataType;
        private static bool loggedOpen;
        private static bool loggedPoolFailure;

        public static void Apply(Harmony harmony)
        {
            Type panel = GameReflection.FindType(PanelTypeName);
            if (panel == null)
            {
                Log.Warn(PanelTypeName + " 타입을 찾지 못했습니다. 게임이 업데이트되어 모드 수정이 필요합니다.");
                return;
            }

            PatchCapacity(harmony, panel);
            PatchOpen(harmony, panel);
        }

        // ───────────────────────── 용량 제한 해제 ─────────────────────────

        private static void PatchCapacity(Harmony harmony, Type panel)
        {
            List<MethodInfo> targets = AccessTools.GetDeclaredMethods(panel)
                .Where(m => m.Name == CapacityMethodName && m.ReturnType == typeof(int) && !m.IsAbstract && !m.IsGenericMethodDefinition)
                .ToList();

            if (targets.Count == 0)
            {
                Log.Warn(PanelTypeName + "." + CapacityMethodName + " 을 찾지 못해 용량 제한 해제를 쓸 수 없습니다.");
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

        private static void PatchOpen(Harmony harmony, Type panel)
        {
            itemMetadataType = GameReflection.FindType("ItemMetadata");
            if (itemMetadataType == null)
            {
                Log.Warn("ItemMetadata 타입을 찾지 못해 '모든 아이템 선택'을 쓸 수 없습니다.");
                return;
            }

            Type listType = typeof(List<>).MakeGenericType(itemMetadataType);
            List<MethodInfo> targets = AccessTools.GetDeclaredMethods(panel)
                .Where(m => m.Name == OpenMethodName && !m.IsAbstract && !m.IsGenericMethodDefinition
                            && m.GetParameters().Any(p => p.ParameterType == listType))
                .ToList();

            if (targets.Count == 0)
            {
                Log.Warn(PanelTypeName + "." + OpenMethodName + "(…List<ItemMetadata>…) 을 찾지 못해 '모든 아이템 선택'을 쓸 수 없습니다.");
                return;
            }

            var prefix = new HarmonyMethod(typeof(FountainPatches), nameof(OpenPrefix)) { priority = Priority.First };
            foreach (MethodInfo target in targets)
            {
                harmony.Patch(target, prefix: prefix);
                Log.Info("목록 패치 적용: " + GameReflection.Describe(target));
            }
        }

        // 패널이 열릴 때 넘어오는 '선택 가능 목록'에, 게임(및 다른 모드)에 등록된 나머지 아이템을 덧붙입니다.
        private static void OpenPrefix(MethodBase __originalMethod, object[] __args)
        {
            try
            {
                ParameterInfo[] parameters = __originalMethod.GetParameters();
                int poolIndex = FindPoolParameter(parameters);

                if (!loggedOpen)
                {
                    loggedOpen = true;
                    Log.Info("소원 분수 열림: " + GameReflection.Describe(__originalMethod) + " / " + DescribeArgs(parameters, __args)
                             + " / 목록 인자=" + (poolIndex >= 0 ? parameters[poolIndex].Name : "없음"));
                }

                if (!Settings.AllItems || poolIndex < 0)
                    return;

                var original = __args[poolIndex] as IList;
                IList expanded = ExpandPool(parameters[poolIndex].ParameterType, original);
                if (expanded != null)
                    __args[poolIndex] = expanded;
            }
            catch (Exception e)
            {
                if (!loggedPoolFailure)
                {
                    loggedPoolFailure = true;
                    Log.Error("'모든 아이템 선택' 처리 중 오류(원래 목록으로 진행합니다): " + e);
                }
            }
        }

        private static int FindPoolParameter(ParameterInfo[] parameters)
        {
            Type listType = typeof(List<>).MakeGenericType(itemMetadataType);
            int fallback = -1;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType != listType)
                    continue;
                if (string.Equals(parameters[i].Name, PoolParameterName, StringComparison.OrdinalIgnoreCase))
                    return i;
                string name = parameters[i].Name ?? "";
                if (name.IndexOf("cached", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("pocket", StringComparison.OrdinalIgnoreCase) >= 0)
                    fallback = i;
            }
            return fallback;
        }

        // 원본 리스트는 세이브 데이터일 수 있으므로 건드리지 않고, 새 리스트를 만들어 넘깁니다.
        private static IList ExpandPool(Type listType, IList original)
        {
            int[] allIds = GameReflection.GetAllItemIds();
            if (allIds == null || allIds.Length == 0)
                return null;

            var result = (IList)Activator.CreateInstance(listType);
            var present = new HashSet<int>();

            if (original != null)
            {
                foreach (object entry in original)
                {
                    result.Add(entry);
                    int id;
                    if (GameReflection.TryGetEntityId(entry, out id))
                        present.Add(id);
                }
            }

            int added = 0;
            Array.Sort(allIds);
            foreach (int id in allIds)
            {
                if (present.Contains(id))
                    continue;
                if (!GameReflection.IsSelectableItem(id, Settings.IncludeHidden))
                    continue;

                object metadata = GameReflection.CreateItemMetadata(itemMetadataType, id);
                if (metadata == null)
                    return null; // 생성 방법을 모르면 원래 목록 그대로 둡니다.

                result.Add(metadata);
                present.Add(id);
                added++;
            }

            if (added == 0)
                return null;

            Log.Info("소원 분수 목록에 아이템 " + added + "개 추가 (기존 " + (original != null ? original.Count : 0) + "개).");
            return result;
        }

        private static string DescribeArgs(ParameterInfo[] parameters, object[] args)
        {
            var parts = new List<string>();
            for (int i = 0; i < parameters.Length && i < args.Length; i++)
            {
                object arg = args[i];
                string value;
                if (arg == null) value = "null";
                else if (arg is ICollection) value = "Count=" + ((ICollection)arg).Count;
                else if (arg is int) value = arg.ToString();
                else value = arg.GetType().Name;
                parts.Add(parameters[i].Name + "=" + value);
            }
            return string.Join(", ", parts.ToArray());
        }
    }
}
