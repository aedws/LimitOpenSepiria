using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace LimitOpenSepiria
{
    /// <summary>
    /// 소원 분수 화면 안의 모드 UI.
    ///  - 희귀도/즐겨찾기 버튼 줄에 모드 토글 버튼(용량 해제, 모든 아이템, 확장 +6, 확장 +12)을 추가합니다.
    ///    게임의 검색 버튼 프리팹을 그대로 복제하므로 모양과 조작이 게임 버튼과 같습니다.
    ///  - 용량 제한 해제가 켜져 있으면 '주머니 용량' 표시를 숨깁니다.
    /// </summary>
    internal static class FountainUi
    {
        private const string CapacityData = "LIMITOPEN_CAPACITY";
        private const string AllItemsData = "LIMITOPEN_ALLITEMS";
        private const string Inventory6Data = "LIMITOPEN_INVENTORY6";
        private const string Inventory12Data = "LIMITOPEN_INVENTORY12";

        private sealed class Widgets
        {
            public Component CapacityButton;
            public Component AllItemsButton;
            public Component Inventory6Button;
            public Component Inventory12Button;
            public GameObject CapacityDisplay;
            public bool CapacityDisplayResolved;
        }

        private static readonly ConditionalWeakTable<object, Widgets> Panels = new ConditionalWeakTable<object, Widgets>();
        private static readonly ConditionalWeakTable<object, object> ButtonOwners = new ConditionalWeakTable<object, object>();

        private static FieldInfo containerField;
        private static FieldInfo prefabField;
        private static FieldInfo favoriteButtonField;
        private static FieldInfo defaultColorField;
        private static FieldInfo capacityImageField;
        private static FieldInfo[] unrelatedFields;

        private static MethodInfo buttonInitialize;
        private static MethodInfo buttonSetSelected;
        private static FieldInfo buttonData;
        private static Delegate clickHandler;

        private static FieldInfo iconItemIcon;
        private static PropertyInfo itemIconItem;

        private static bool ready;
        private static bool loggedFailure;

        public static void Initialize(Type panel)
        {
            containerField = panel.GetField("searchOptionButtonContainer", GameReflection.Any);
            prefabField = panel.GetField("searchOptionButtonPrefab", GameReflection.Any);
            favoriteButtonField = panel.GetField("favoriteButton", GameReflection.Any);
            defaultColorField = panel.GetField("tagButtonDefaultColor", GameReflection.Any);
            capacityImageField = panel.GetField("capacityImage", GameReflection.Any);
            unrelatedFields = new[] { "contentsZone", "searchOptionButtonContainer", "skipButton", "capacityText", "tooltipPosition" }
                .Select(name => panel.GetField(name, GameReflection.Any))
                .Where(field => field != null)
                .ToArray();

            Type icon = GameReflection.FindType("UI_DimensionVaultItemIcon");
            iconItemIcon = icon != null ? icon.GetField("itemIcon", GameReflection.Any) : null;
            itemIconItem = iconItemIcon != null ? iconItemIcon.FieldType.GetProperty("Item", GameReflection.Any) : null;

            Type button = prefabField != null ? prefabField.FieldType : null;
            if (button != null)
            {
                buttonInitialize = button.GetMethods(GameReflection.Any).FirstOrDefault(m => m.Name == "Initialize" && m.GetParameters().Length == 5);
                buttonSetSelected = button.GetMethods(GameReflection.Any).FirstOrDefault(m => m.Name == "SetSelected" && m.GetParameters().Length == 1);
                buttonData = button.GetField("data", GameReflection.Any);

                // Initialize 가 받는 Action<버튼 타입> 을 만듭니다.
                MethodInfo handler = typeof(FountainUi).GetMethod(nameof(OnButtonClicked), BindingFlags.NonPublic | BindingFlags.Static)
                    .MakeGenericMethod(button);
                clickHandler = Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(button), handler);
            }

            ready = containerField != null && prefabField != null && buttonInitialize != null && buttonSetSelected != null
                    && buttonData != null && defaultColorField != null;
            if (!ready)
                Log.Warn("분수 화면의 버튼 프리팹을 찾지 못해 모드 토글은 옵션 화면에서만 바꿀 수 있습니다.");
        }

        /// <summary>아이콘이 가리키는 ItemMetadata 를 돌려줍니다.</summary>
        public static object GetIconItem(object vaultIcon)
        {
            if (vaultIcon == null || iconItemIcon == null || itemIconItem == null)
                return null;
            object itemIcon = iconItemIcon.GetValue(vaultIcon);
            return GameReflection.IsNull(itemIcon) ? null : itemIconItem.GetValue(itemIcon, null);
        }

        /// <summary>분수 화면이 구성될 때마다 불립니다. 버튼을 만들고(처음 한 번), 표시를 현재 설정에 맞춥니다.</summary>
        public static void Sync(object panel)
        {
            try
            {
                Widgets widgets = Panels.GetOrCreateValue(panel);
                if (ready)
                    EnsureButtons(panel, widgets);
                UpdateCapacityDisplay(panel, widgets);
            }
            catch (Exception e)
            {
                if (loggedFailure)
                    return;
                loggedFailure = true;
                Log.Error("분수 화면 UI 구성 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
            }
        }

        private static void EnsureButtons(object panel, Widgets widgets)
        {
            var container = containerField.GetValue(panel) as Transform;
            if (GameReflection.IsNull(container))
                return;

            if (GameReflection.IsNull(widgets.CapacityButton) || GameReflection.IsNull(widgets.AllItemsButton)
                || GameReflection.IsNull(widgets.Inventory6Button) || GameReflection.IsNull(widgets.Inventory12Button))
            {
                var prefab = prefabField.GetValue(panel) as UnityEngine.Object;
                if (GameReflection.IsNull(prefab))
                    return;

                // 즐겨찾기 버튼 바로 뒤(희귀도 줄의 끝)에 둡니다.
                int index = -1;
                var favorite = favoriteButtonField != null ? favoriteButtonField.GetValue(panel) as Component : null;
                if (!GameReflection.IsNull(favorite))
                    index = favorite.transform.GetSiblingIndex() + 1;

                widgets.CapacityButton = CreateButton(panel, prefab, container, Text("용량 해제", "No Limit"), CapacityData, index);
                widgets.AllItemsButton = CreateButton(panel, prefab, container, Text("모든 아이템", "All Items"), AllItemsData, index < 0 ? -1 : index + 1);
                widgets.Inventory6Button = CreateButton(panel, prefab, container, Text("확장 +6", "Bag +6"), Inventory6Data, index < 0 ? -1 : index + 2);
                widgets.Inventory12Button = CreateButton(panel, prefab, container, Text("확장 +12", "Bag +12"), Inventory12Data, index < 0 ? -1 : index + 3);
            }

            buttonSetSelected.Invoke(widgets.CapacityButton, new object[] { Settings.UnlimitedCapacity });
            buttonSetSelected.Invoke(widgets.AllItemsButton, new object[] { Settings.AllItems });
            buttonSetSelected.Invoke(widgets.Inventory6Button, new object[] { Settings.InventoryBonus == 6 });
            buttonSetSelected.Invoke(widgets.Inventory12Button, new object[] { Settings.InventoryBonus == 12 });

            // 게임은 일반 등급 아이템만 있을 때 버튼 줄 전체를 숨깁니다. 모드 버튼은 항상 보여야 합니다.
            container.gameObject.SetActive(true);
        }

        private static Component CreateButton(object panel, UnityEngine.Object prefab, Transform container, string text, string data, int siblingIndex)
        {
            var button = (Component)UnityEngine.Object.Instantiate(prefab, container);
            button.name = "LimitOpenSepiria_" + data;
            buttonInitialize.Invoke(button, new[] { text, data, clickHandler, defaultColorField.GetValue(panel), null });
            if (siblingIndex >= 0)
                button.transform.SetSiblingIndex(siblingIndex);
            ButtonOwners.Add(button, panel);
            return button;
        }

        private static void OnButtonClicked<T>(T button)
        {
            try
            {
                object panel;
                if (button == null || !ButtonOwners.TryGetValue(button, out panel))
                    return;

                string data = buttonData.GetValue(button) as string;
                if (data == CapacityData)
                    Settings.SetUnlimitedCapacity(!Settings.UnlimitedCapacity);
                else if (data == AllItemsData)
                    Settings.SetAllItems(!Settings.AllItems);
                else if (data == Inventory6Data || data == Inventory12Data)
                {
                    // 인벤토리 확장은 둘 중 하나만 켜집니다. 켜진 버튼을 다시 누르면 꺼집니다.
                    int amount = data == Inventory6Data ? 6 : 12;
                    Settings.SetInventoryBonus(Settings.InventoryBonus == amount ? 0 : amount);
                    Sync(panel);
                    return;
                }
                else
                    return;

                // 화면을 다시 구성하면 Sync 가 불려 버튼 선택 표시와 용량 표시도 함께 맞춰집니다.
                FountainPatches.Rebuild(panel);
            }
            catch (Exception e)
            {
                Log.Error("분수 화면 버튼 처리 중 오류: " + e);
            }
        }

        // '주머니 용량' 글자와 막대를 함께 숨깁니다. 막대의 부모가 그 둘만 묶는 그룹이면 부모를,
        // 부모가 목록·버튼 등 다른 UI 까지 품고 있으면 막대만 숨깁니다.
        private static void UpdateCapacityDisplay(object panel, Widgets widgets)
        {
            if (!widgets.CapacityDisplayResolved)
            {
                widgets.CapacityDisplayResolved = true;
                widgets.CapacityDisplay = FindCapacityDisplay(panel);
            }

            if (!GameReflection.IsNull(widgets.CapacityDisplay))
                widgets.CapacityDisplay.SetActive(!Settings.UnlimitedCapacity);
        }

        private static GameObject FindCapacityDisplay(object panel)
        {
            var bar = capacityImageField != null ? capacityImageField.GetValue(panel) as Component : null;
            if (GameReflection.IsNull(bar))
                return null;

            Transform group = bar.transform.parent;
            var panelTransform = ((Component)panel).transform;
            if (GameReflection.IsNull(group) || ReferenceEquals(group, panelTransform))
                return bar.gameObject;

            foreach (FieldInfo field in unrelatedFields)
            {
                Transform other = ToTransform(field.GetValue(panel));
                if (other != null && other.IsChildOf(group))
                    return bar.gameObject;
            }
            return group.gameObject;
        }

        private static Transform ToTransform(object value)
        {
            if (GameReflection.IsNull(value))
                return null;
            var transform = value as Transform;
            if (transform != null)
                return transform;
            var component = value as Component;
            if (component != null)
                return component.transform;
            var gameObject = value as GameObject;
            return gameObject != null ? gameObject.transform : null;
        }

        private static string Text(string korean, string english)
        {
            return GameReflection.CurrentLanguage().StartsWith("ko", StringComparison.OrdinalIgnoreCase) ? korean : english;
        }
    }
}
