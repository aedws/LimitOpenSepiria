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
    /// 실패는 모두 null/false 로 돌려주고, 호출부가 기능을 끄는 쪽으로 처리합니다.
    /// </summary>
    internal static class GameReflection
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // 시작 아이템으로 의미가 없는 종류(미감정 등). '숨김 포함' 옵션이 꺼져 있을 때만 걸러냅니다.
        private static readonly string[] HiddenItemTypes = { "Identifiable" };
        private static readonly string[] HiddenFlagNames = { "disabled", "isDisabled", "isHidden", "hidden", "hideInJournal" };

        private static MethodInfo getAllItemId;
        private static MethodInfo findItemById;
        private static ConstructorInfo metadataCtor;
        private static bool metadataCtorResolved;
        private static readonly Dictionary<Type, MemberInfo> EntityIdMembers = new Dictionary<Type, MemberInfo>();

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

        public static int[] GetAllItemIds()
        {
            if (getAllItemId == null)
            {
                Type database = FindType("ItemDatabase");
                if (database == null)
                    return null;
                getAllItemId = database.GetMethods(Any).FirstOrDefault(m => m.Name == "GetAllItemID" && m.IsStatic && m.GetParameters().Length == 0);
                findItemById = database.GetMethods(Any).FirstOrDefault(m => m.Name == "FindItemById" && m.IsStatic
                    && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(int));
                if (getAllItemId == null)
                    return null;
            }

            object value = getAllItemId.Invoke(null, null);
            var array = value as int[];
            if (array != null)
                return (int[])array.Clone();
            var sequence = value as IEnumerable<int>;
            return sequence != null ? sequence.ToArray() : null;
        }

        public static bool IsSelectableItem(int id, bool includeHidden)
        {
            if (findItemById == null)
                return false;

            object entity = findItemById.Invoke(null, new object[] { id });
            // Unity 오브젝트는 파괴되면 Equals(null) 이 true 가 됩니다.
            if (entity == null || entity.Equals(null))
                return false;
            if (includeHidden)
                return true;

            Type type = entity.GetType();

            foreach (string flag in HiddenFlagNames)
            {
                object flagValue;
                if (TryGetMember(type, entity, flag, out flagValue) && flagValue is bool && (bool)flagValue)
                    return false;
            }

            object itemType;
            if (TryGetMember(type, entity, "type", out itemType) && itemType != null
                && HiddenItemTypes.Contains(itemType.ToString()))
                return false;

            // 아이콘이 없는 항목은 내부용 더미로 보고 제외합니다.
            object icon;
            if (TryGetMember(type, entity, "icon", out icon) && (icon == null || icon.Equals(null)))
                return false;

            return true;
        }

        public static bool TryGetEntityId(object metadata, out int id)
        {
            id = 0;
            if (metadata == null)
                return false;

            Type type = metadata.GetType();
            MemberInfo member;
            if (!EntityIdMembers.TryGetValue(type, out member))
            {
                member = (MemberInfo)type.GetField("entityID", Any) ?? type.GetProperty("entityID", Any);
                EntityIdMembers[type] = member;
            }

            object value = null;
            var field = member as FieldInfo;
            var property = member as PropertyInfo;
            if (field != null) value = field.GetValue(metadata);
            else if (property != null) value = property.GetValue(metadata, null);
            if (!(value is int))
                return false;

            id = (int)value;
            return true;
        }

        // 게임에서의 사용 형태: new ItemMetadata(instanceID, entityID, (sbyte)count)
        public static object CreateItemMetadata(Type metadataType, int entityId)
        {
            if (!metadataCtorResolved)
            {
                metadataCtorResolved = true;
                metadataCtor = metadataType.GetConstructors(Any).FirstOrDefault(c =>
                {
                    ParameterInfo[] p = c.GetParameters();
                    return p.Length == 3 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int)
                           && p[2].ParameterType == typeof(sbyte);
                });
                if (metadataCtor == null)
                    Log.Warn("ItemMetadata(int, int, sbyte) 생성자를 찾지 못해 '모든 아이템 선택'을 쓸 수 없습니다.");
            }

            if (metadataCtor == null)
                return null;
            return metadataCtor.Invoke(new object[] { 0, entityId, (sbyte)1 });
        }

        private static bool TryGetMember(Type type, object instance, string name, out object value)
        {
            value = null;
            FieldInfo field = type.GetField(name, Any);
            if (field != null)
            {
                value = field.GetValue(field.IsStatic ? null : instance);
                return true;
            }

            PropertyInfo property = type.GetProperty(name, Any);
            if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
            {
                value = property.GetValue(instance, null);
                return true;
            }

            return false;
        }
    }
}
