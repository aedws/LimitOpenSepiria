using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;

namespace LimitOpenSepiria
{
    /// <summary>
    /// 재능 탭과 과일 꼬치 탭 안의 조절 버튼.
    /// 게임이 모드용으로 공개한 UIWidgetFactory(HorayModAPI.TryGetUIWidgets)로 버튼을 만들어
    /// 각 화면의 왼쪽 위 모서리에 붙입니다.
    ///
    ///   재능 탭      [-10] [-1] [재능 최대 140] [+1] [+10]
    ///                [레벨 20 상한: 적용 중/해제됨]
    ///   과일 꼬치 탭 [-1] [과일 최대 21] [+1]
    ///
    /// 가운데 버튼은 '게임 기본값 ↔ 모드 최대값'을 오갑니다.
    /// </summary>
    internal static class ProgressionUi
    {
        // 화면 왼쪽 위 모서리에서의 여백과 버튼 크기(UI 단위).
        private const float MarginX = 8f;
        private const float MarginY = 8f;
        private const float RowHeight = 24f;
        private const float RowGap = 4f;
        private const float Spacing = 2f;

        private sealed class TalentBar
        {
            public List<Component> Rows = new List<Component>();
            public Component Value;
            public Component LevelCap;
        }

        private sealed class FruitBar
        {
            public Component Row;
            public Component Value;
        }

        private static readonly ConditionalWeakTable<object, TalentBar> TalentBars = new ConditionalWeakTable<object, TalentBar>();
        private static readonly ConditionalWeakTable<object, FruitBar> FruitBars = new ConditionalWeakTable<object, FruitBar>();

        private static FieldInfo talentAvatar;
        private static MethodInfo talentCreateUi;
        private static MethodInfo talentResetPoints;
        private static FieldInfo avatarPassiveStats;
        private static FieldInfo fruitPreviousCount;
        private static FieldInfo buttonText;
        private static PropertyInfo textProperty;

        private static bool loggedFailure;

        public static void Apply(Harmony harmony)
        {
            Type button = GameReflection.FindType("UI_HorayButton");
            buttonText = button != null ? button.GetField("text", GameReflection.Any) : null;
            textProperty = buttonText != null ? buttonText.FieldType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance) : null;
            if (textProperty == null)
            {
                Log.Warn("버튼 글자에 접근하지 못해 재능·과일 꼬치 탭의 조절 버튼을 만들 수 없습니다. 옵션 화면에서 조절하세요.");
                return;
            }

            Type talentPanel = GameReflection.FindType("UI_PassivePanel");
            Type avatar = GameReflection.FindType("PlayerAvatar");
            MethodInfo talentOpened = talentPanel != null ? AccessTools.DeclaredMethod(talentPanel, "OnOpened", Type.EmptyTypes) : null;
            if (talentOpened != null && avatar != null)
            {
                talentAvatar = talentPanel.GetField("playerAvatar", GameReflection.Any);
                talentCreateUi = AccessTools.DeclaredMethod(talentPanel, "CreateUI", Type.EmptyTypes);
                talentResetPoints = AccessTools.DeclaredMethod(talentPanel, "ResetPoints", Type.EmptyTypes);
                avatarPassiveStats = avatar.GetField("passiveStats", GameReflection.Any);
            }
            if (talentOpened != null && talentAvatar != null)
                harmony.Patch(talentOpened, postfix: new HarmonyMethod(typeof(ProgressionUi), nameof(TalentOpenedPostfix)));
            else
                Log.Warn("재능 탭을 찾지 못해 탭 안의 조절 버튼을 만들 수 없습니다. 옵션 화면에서 조절하세요.");

            Type fruitPanel = GameReflection.FindType("UI_FruitSkewerPanel");
            MethodInfo fruitOpened = fruitPanel != null ? AccessTools.DeclaredMethod(fruitPanel, "OnOpened", Type.EmptyTypes) : null;
            if (fruitOpened != null)
            {
                fruitPreviousCount = fruitPanel.GetField("pevCurSelectedFruitCount", GameReflection.Any);
                harmony.Patch(fruitOpened, postfix: new HarmonyMethod(typeof(ProgressionUi), nameof(FruitOpenedPostfix)));
            }
            else
            {
                Log.Warn("과일 꼬치 탭을 찾지 못해 탭 안의 조절 버튼을 만들 수 없습니다. 옵션 화면에서 조절하세요.");
            }
        }

        // ───────────────────────── 재능 탭 ─────────────────────────

        private static void TalentOpenedPostfix(object __instance)
        {
            Guard("재능 탭 버튼 구성", () =>
            {
                TalentBar bar = TalentBars.GetOrCreateValue(__instance);
                if (bar.Rows.Count == 0 || bar.Rows.Any(GameReflection.IsNull))
                    BuildTalentBar(__instance, bar);
                RefreshTalentBar(__instance, bar);
            });
        }

        private static void BuildTalentBar(object panel, TalentBar bar)
        {
            UIWidgetFactory widgets;
            if (!HorayModAPI.TryGetUIWidgets(out widgets))
                return;

            bar.Rows.Clear();
            Transform root = ((Component)panel).transform;

            var points = new RowBuilder(widgets, root, 0);
            points.Add("-10", 28f, () => StepTalent(panel, -10));
            points.Add("-1", 24f, () => StepTalent(panel, -1));
            bar.Value = points.Add("", 104f, () => ToggleTalent(panel));
            points.Add("+1", 24f, () => StepTalent(panel, 1));
            points.Add("+10", 28f, () => StepTalent(panel, 10));
            bar.Rows.Add(points.Finish());

            var cap = new RowBuilder(widgets, root, 1);
            bar.LevelCap = cap.Add("", 216f, () =>
            {
                Settings.SetUnlimitedTalentLevel(!Settings.UnlimitedTalentLevel);
                RefreshTalentBar(panel, bar);
            });
            bar.Rows.Add(cap.Finish());
        }

        private static void RefreshTalentBar(object panel, TalentBar bar)
        {
            if (bar.Rows.Count == 0)
                return;

            // 최대치는 서버(호스트)가 정합니다. 참가자 화면에서는 버튼을 숨깁니다.
            object avatar = talentAvatar.GetValue(panel);
            bool canAdjust = ProgressionPatches.CanAdjustTalent(avatar);
            foreach (Component row in bar.Rows)
            {
                if (!GameReflection.IsNull(row))
                    row.gameObject.SetActive(canAdjust);
            }
            if (!canAdjust)
                return;

            int current = ProgressionPatches.CurrentTalentMax(avatar);
            bool isDefault = current <= ProgressionPatches.VanillaTalentMax(avatar);
            SetLabel(bar.Value, Text("재능 최대 ", "Talent max ") + current + (isDefault ? Text(" (기본)", " (default)") : ""));
            SetLabel(bar.LevelCap, Settings.UnlimitedTalentLevel
                ? Text("레벨 20 상한: 해제됨", "Level cap 20: removed")
                : Text("레벨 20 상한: 적용 중", "Level cap 20: active"));
        }

        private static void StepTalent(object panel, int delta)
        {
            Guard("재능 포인트 조절", () =>
            {
                object avatar = talentAvatar.GetValue(panel);
                if (!ProgressionPatches.CanAdjustTalent(avatar))
                    return;
                int vanilla = ProgressionPatches.VanillaTalentMax(avatar);
                int current = ProgressionPatches.CurrentTalentMax(avatar);
                ApplyTalent(panel, avatar, NextValue(current, vanilla, delta, Settings.TalentPointLimit));
            });
        }

        private static void ToggleTalent(object panel)
        {
            Guard("재능 포인트 조절", () =>
            {
                object avatar = talentAvatar.GetValue(panel);
                if (!ProgressionPatches.CanAdjustTalent(avatar))
                    return;
                bool isDefault = ProgressionPatches.CurrentTalentMax(avatar) <= ProgressionPatches.VanillaTalentMax(avatar);
                ApplyTalent(panel, avatar, isDefault ? Settings.TalentPointLimit : 0);
            });
        }

        private static void ApplyTalent(object panel, object avatar, int value)
        {
            Settings.SetTalentPoints(value);

            // 최대치가 이미 쓴 포인트보다 작아지면 게임의 '초기화'로 포인트를 돌려받게 합니다.
            if (SpentTalentPoints(avatar) > ProgressionPatches.CurrentTalentMax(avatar) && talentResetPoints != null)
                talentResetPoints.Invoke(panel, null);

            // 남은 포인트 숫자를 다시 그립니다.
            if (talentCreateUi != null)
                talentCreateUi.Invoke(panel, null);

            TalentBar bar;
            if (TalentBars.TryGetValue(panel, out bar))
                RefreshTalentBar(panel, bar);
        }

        private static int SpentTalentPoints(object avatar)
        {
            var stats = avatarPassiveStats != null ? avatarPassiveStats.GetValue(avatar) as IEnumerable<KeyValuePair<ulong, int>> : null;
            return stats != null ? stats.Sum(pair => pair.Value) : 0;
        }

        // ───────────────────────── 과일 꼬치 탭 ─────────────────────────

        private static void FruitOpenedPostfix(object __instance)
        {
            Guard("과일 꼬치 탭 버튼 구성", () =>
            {
                FruitBar bar = FruitBars.GetOrCreateValue(__instance);
                if (GameReflection.IsNull(bar.Row))
                    BuildFruitBar(__instance, bar);
                RefreshFruitBar(__instance, bar);
            });
        }

        private static void BuildFruitBar(object panel, FruitBar bar)
        {
            UIWidgetFactory widgets;
            if (!HorayModAPI.TryGetUIWidgets(out widgets))
                return;

            var row = new RowBuilder(widgets, ((Component)panel).transform, 0);
            row.Add("-1", 24f, () => StepFruit(panel, bar, -1));
            bar.Value = row.Add("", 104f, () => StepFruit(panel, bar, 0));
            row.Add("+1", 24f, () => StepFruit(panel, bar, 1));
            bar.Row = row.Finish();
        }

        private static void RefreshFruitBar(object panel, FruitBar bar)
        {
            if (GameReflection.IsNull(bar.Row))
                return;
            int current = ProgressionPatches.FruitMax(panel, false);
            bool isDefault = current <= ProgressionPatches.FruitMax(panel, true);
            SetLabel(bar.Value, Text("과일 최대 ", "Fruit max ") + current + (isDefault ? Text(" (기본)", " (default)") : ""));
        }

        // delta 가 0 이면 '게임 기본값 ↔ 모드 최대값'을 오갑니다.
        private static void StepFruit(object panel, FruitBar bar, int delta)
        {
            Guard("과일 꼬치 개수 조절", () =>
            {
                int vanilla = ProgressionPatches.FruitMax(panel, true);
                int current = ProgressionPatches.FruitMax(panel, false);
                int value = delta == 0
                    ? (current <= vanilla ? Settings.FruitSkewerLimit : 0)
                    : NextValue(current, vanilla, delta, Settings.FruitSkewerLimit);
                Settings.SetFruitSkewer(value);

                // 화면의 '선택 수 / 최대' 글자는 선택 수가 바뀔 때만 갱신되므로, 다시 그리게 만듭니다.
                if (fruitPreviousCount != null)
                    fruitPreviousCount.SetValue(panel, -1);
                RefreshFruitBar(panel, bar);
            });
        }

        // ───────────────────────── 공통 ─────────────────────────

        // 설정값은 '게임 기본값보다 클 때만' 효과가 있으므로, 기본값 이하로 내려가면 0(기본)으로 둡니다.
        private static int NextValue(int current, int vanilla, int delta, int limit)
        {
            if (vanilla >= limit)
                return 0;
            int next = Math.Min(limit, Math.Max(current, vanilla) + delta);
            return next <= vanilla ? 0 : next;
        }

        private static void SetLabel(Component button, string text)
        {
            if (GameReflection.IsNull(button))
                return;
            object label = buttonText.GetValue(button);
            if (!GameReflection.IsNull(label))
                textProperty.SetValue(label, text, null);
        }

        private static string Text(string korean, string english)
        {
            return GameReflection.CurrentLanguage().StartsWith("ko", StringComparison.OrdinalIgnoreCase) ? korean : english;
        }

        private static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (loggedFailure)
                    return;
                loggedFailure = true;
                Log.Error(what + " 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
            }
        }

        /// <summary>버튼 한 줄을 만들어 화면 왼쪽 위에 배치합니다.</summary>
        private sealed class RowBuilder
        {
            private readonly UIWidgetFactory widgets;
            private readonly RectTransform row;
            private readonly int index;
            private float width;
            private int count;

            public RowBuilder(UIWidgetFactory widgets, Transform parent, int index)
            {
                this.widgets = widgets;
                this.index = index;
                row = widgets.CreateHorizontalLayout(parent, Spacing);
                row.name = "LimitOpenSepiria_Row" + index;
            }

            public Component Add(string label, float buttonWidth, Action onClick)
            {
                // UI_HorayButton 은 Unity 의 Button(Component)을 상속합니다. UnityEngine.UI 참조 없이도 빌드되도록 object 를 거칩니다.
                object created = widgets.CreateButton(row, label, new UnityAction(onClick));
                var button = (Component)created;
                UIWidgetFactory.SetPreferredSize(button, buttonWidth, RowHeight);
                width += buttonWidth;
                count++;
                return button;
            }

            public Component Finish()
            {
                var topLeft = new Vector2(0f, 1f);
                row.anchorMin = topLeft;
                row.anchorMax = topLeft;
                row.pivot = topLeft;
                row.sizeDelta = new Vector2(width + Spacing * Math.Max(0, count - 1), RowHeight);
                row.anchoredPosition = new Vector2(MarginX, -(MarginY + index * (RowHeight + RowGap)));
                return row;
            }
        }
    }
}
