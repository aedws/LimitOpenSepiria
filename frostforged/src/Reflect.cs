using System;
using System.Linq;
using System.Reflection;

namespace FrostforgedCrescent
{
    /// <summary>게임 내부 타입에 이름으로 접근하는 도우미. 찾지 못하면 null 을 돌려줍니다.</summary>
    internal static class Reflect
    {
        public const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        public static Type GameType(string name)
        {
            return typeof(WorkshopModBase).Assembly.GetType(name, false);
        }

        public static Type UnityType(string fullName, string assembly)
        {
            return Type.GetType(fullName + ", " + assembly, false);
        }

        // Unity 오브젝트는 파괴되거나 비어 있으면 Equals(null) 이 true 가 됩니다.
        public static bool IsNull(object value)
        {
            return value == null || value.Equals(null);
        }

        public static object Get(object target, string name)
        {
            if (target == null)
                return null;
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Any | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field.GetValue(target);
                PropertyInfo property = type.GetProperty(name, Any | BindingFlags.DeclaredOnly);
                if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                    return property.GetValue(target, null);
            }
            return null;
        }

        public static object GetStatic(Type type, string name)
        {
            if (type == null)
                return null;
            FieldInfo field = type.GetField(name, Any);
            if (field != null)
                return field.GetValue(null);
            PropertyInfo property = type.GetProperty(name, Any);
            return property != null ? property.GetValue(null, null) : null;
        }

        public static bool Set(object target, string name, object value)
        {
            if (target == null)
                return false;
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Any | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    field.SetValue(target, field.FieldType.IsEnum && value is int ? Enum.ToObject(field.FieldType, value) : value);
                    return true;
                }
                PropertyInfo property = type.GetProperty(name, Any | BindingFlags.DeclaredOnly);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(target, property.PropertyType.IsEnum && value is int ? Enum.ToObject(property.PropertyType, value) : value, null);
                    return true;
                }
            }
            return false;
        }

        /// <summary>인자 개수가 맞는 첫 메서드를 부릅니다(인스턴스 또는 정적).</summary>
        public static object Call(object target, Type type, string name, params object[] args)
        {
            for (Type current = type ?? target.GetType(); current != null; current = current.BaseType)
            {
                MethodInfo method = current.GetMethods(Any | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == args.Length && !m.IsGenericMethodDefinition);
                if (method != null)
                    return method.Invoke(method.IsStatic ? null : target, args);
            }
            throw new MissingMethodException((type ?? target.GetType()).Name, name);
        }
    }
}
