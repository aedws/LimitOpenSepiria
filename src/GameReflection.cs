using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace LimitOpenSepiria
{
    /// <summary>
    /// 게임 내부(비공개 API) 타입에 이름으로 접근하는 도우미.
    /// 게임 1.1.x 의 Assembly-CSharp 을 기준으로 확인한 멤버 이름을 씁니다.
    /// 찾지 못한 멤버는 null 로 남고, 호출부가 해당 기능을 끄는 쪽으로 처리합니다.
    /// </summary>
    internal static class GameReflection
    {
        public const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // ItemDatabase
        private static MethodInfo getAllItemId;
        private static MethodInfo findItemById;

        // ItemMetadata
        public static Type ItemMetadataType { get; private set; }
        private static ConstructorInfo metadataCtor;
        private static FieldInfo metadataEntityId;

        // ItemEntity
        private static FieldInfo activeType;
        private static FieldInfo itemType;
        private static FieldInfo rarity;
        private static FieldInfo resourcePrefab;
        private static FieldInfo cannotSelectInDimensionPocket;
        private static FieldInfo cannotBeReward;

        private static object activeDefault;
        private static readonly HashSet<string> HiddenActiveTypes = new HashSet<string> { "Hidden", "Disabled", "TestOnly" };
        private static object typeCharm;
        private static object rarityCommon;
        private static object rarityEternal;

        public static bool Ready { get; private set; }

        public static Type FindType(string name)
        {
            // 게임 코드는 Assembly-CSharp 의 전역 네임스페이스에 있습니다.
            Type type = typeof(WorkshopModBase).Assembly.GetType(name, false);
            return type ?? AccessTools.TypeByName(name);
        }

        public static string Describe(MethodBase method)
        {
            string parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name).ToArray());
            return method.DeclaringType.Name + "." + method.Name + "(" + parameters + ")";
        }

        /// <summary>아이템 관련 멤버를 한 번에 찾습니다. 하나라도 없으면 false 와 함께 빠진 이름을 돌려줍니다.</summary>
        public static bool Initialize(out string missing)
        {
            var lost = new List<string>();

            Type database = FindType("ItemDatabase");
            Type entity = FindType("ItemEntity");
            ItemMetadataType = FindType("ItemMetadata");

            if (database != null)
            {
                getAllItemId = database.GetMethods(Any).FirstOrDefault(m => m.Name == "GetAllItemID" && m.IsStatic && m.GetParameters().Length == 0);
                findItemById = database.GetMethods(Any).FirstOrDefault(m => m.Name == "FindItemById" && m.IsStatic
                    && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(int));
            }
            if (getAllItemId == null) lost.Add("ItemDatabase.GetAllItemID");
            if (findItemById == null) lost.Add("ItemDatabase.FindItemById");

            if (ItemMetadataType != null)
            {
                metadataEntityId = ItemMetadataType.GetField("entityID", Any);
                metadataCtor = ItemMetadataType.GetConstructors(Any).FirstOrDefault(c =>
                {
                    ParameterInfo[] p = c.GetParameters();
                    return p.Length == 3 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int)
                           && p[2].ParameterType == typeof(sbyte);
                });
            }
            if (metadataEntityId == null) lost.Add("ItemMetadata.entityID");
            if (metadataCtor == null) lost.Add("ItemMetadata(int, int, sbyte)");

            if (entity != null)
            {
                activeType = entity.GetField("activeType", Any);
                itemType = entity.GetField("type", Any);
                rarity = entity.GetField("rarity", Any);
                resourcePrefab = entity.GetField("resourcePrefab", Any);
                cannotSelectInDimensionPocket = entity.GetField("cannotSelectInDimensionPocket", Any);
                cannotBeReward = entity.GetField("cannotBeReward", Any);
            }
            if (activeType == null) lost.Add("ItemEntity.activeType");
            if (itemType == null) lost.Add("ItemEntity.type");
            if (rarity == null) lost.Add("ItemEntity.rarity");
            if (resourcePrefab == null) lost.Add("ItemEntity.resourcePrefab");
            if (cannotSelectInDimensionPocket == null) lost.Add("ItemEntity.cannotSelectInDimensionPocket");
            if (cannotBeReward == null) lost.Add("ItemEntity.cannotBeReward");

            if (lost.Count == 0)
            {
                activeDefault = ParseEnum(activeType.FieldType, "Default", lost);
                typeCharm = ParseEnum(itemType.FieldType, "Charm", lost);
                rarityCommon = ParseEnum(rarity.FieldType, "Common", lost);
                rarityEternal = ParseEnum(rarity.FieldType, "Eternal", lost);
            }

            missing = string.Join(", ", lost.ToArray());
            Ready = lost.Count == 0;
            return Ready;
        }

        private static object ParseEnum(Type enumType, string name, List<string> lost)
        {
            if (enumType.IsEnum && Enum.IsDefined(enumType, name))
                return Enum.Parse(enumType, name);
            lost.Add(enumType.Name + "." + name);
            return null;
        }

        public static int[] GetAllItemIds()
        {
            object value = getAllItemId.Invoke(null, null);
            var array = value as int[];
            if (array != null)
                return (int[])array.Clone();
            var sequence = value as IEnumerable<int>;
            return sequence != null ? sequence.ToArray() : new int[0];
        }

        public static object FindItem(int id)
        {
            object entity = findItemById.Invoke(null, new object[] { id });
            return IsNull(entity) ? null : entity;
        }

        public static int GetEntityId(object metadata)
        {
            return (int)metadataEntityId.GetValue(metadata);
        }

        // 게임이 분수 목록에 넣는 형태와 같습니다: new ItemMetadata(-1, entityID, 1)
        public static object CreateItemMetadata(int entityId)
        {
            return metadataCtor.Invoke(new object[] { -1, entityId, (sbyte)1 });
        }

        public static bool IsCharm(object entity)
        {
            return Equals(itemType.GetValue(entity), typeCharm);
        }

        public static bool IsHiddenOrDisabled(object entity)
        {
            object value = activeType.GetValue(entity);
            return value != null && HiddenActiveTypes.Contains(value.ToString());
        }

        // 분수 패널은 아이템의 resourcePrefab 을 검사 없이 읽으므로, 없는 아이템은 목록에 넣으면 안 됩니다.
        public static bool HasPrefab(object entity)
        {
            return !IsNull(resourcePrefab.GetValue(entity));
        }

        /// <summary>
        /// 분수 패널이 아이템을 걸러내는 조건을 잠시 풀고, 되돌릴 기록을 restores 에 남깁니다.
        /// forListing 이 true 면 목록 구성(OnOpened)용으로 종류·희귀도 조건까지 풉니다.
        /// </summary>
        public static void Relax(object entity, bool forListing, bool allowOtherTypes, List<Restore> restores)
        {
            Override(entity, cannotSelectInDimensionPocket, false, restores);
            Override(entity, cannotBeReward, false, restores);

            object active = activeType.GetValue(entity);
            if (active != null && active.ToString() == "Hidden")
                Override(entity, activeType, activeDefault, restores);

            if (!forListing)
                return;

            // 패널은 Charm(아티팩트) 종류만, 그리고 Eternal 이 아닌 희귀도만 목록에 올립니다.
            if (allowOtherTypes && !IsCharm(entity))
                Override(entity, itemType, typeCharm, restores);

            // Eternal 의 용량은 게임에서 Common 과 같은 값(기본 분기)이라, Common 으로 바꿔도 용량 계산은 같습니다.
            if (Equals(rarity.GetValue(entity), rarityEternal))
                Override(entity, rarity, rarityCommon, restores);
        }

        private static void Override(object target, FieldInfo field, object value, List<Restore> restores)
        {
            object original = field.GetValue(target);
            if (Equals(original, value))
                return;
            field.SetValue(target, value);
            restores.Add(new Restore(target, field, original));
        }

        /// <summary>게임 언어 코드(예: ko-KR). 알 수 없으면 빈 문자열.</summary>
        public static string CurrentLanguage()
        {
            try
            {
                Type manager = FindType("LocalizationManager");
                if (manager == null)
                    return "";
                PropertyInfo instance = manager.GetProperty("Instance", Any);
                PropertyInfo language = manager.GetProperty("CurrentLanguage", Any);
                object value = instance != null ? instance.GetValue(null, null) : null;
                if (IsNull(value) || language == null)
                    return "";
                return language.GetValue(value, null) as string ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        // Unity 오브젝트는 파괴되거나 비어 있으면 Equals(null) 이 true 가 됩니다.
        public static bool IsNull(object value)
        {
            return value == null || value.Equals(null);
        }

        internal sealed class Restore
        {
            private readonly object target;
            private readonly FieldInfo field;
            private readonly object value;

            public Restore(object target, FieldInfo field, object value)
            {
                this.target = target;
                this.field = field;
                this.value = value;
            }

            public void Apply()
            {
                field.SetValue(target, value);
            }
        }
    }
}
