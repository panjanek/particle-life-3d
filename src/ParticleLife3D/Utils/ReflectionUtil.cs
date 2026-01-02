using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ParticleLife3D.Utils
{
    public static class ReflectionUtil
    {
        public static T GetObjectValue<T>(object obj, string path)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));

            object current = obj;
            Type currentType = obj.GetType();

            foreach (string memberName in path.Split('.'))
            {
                if (current == null)
                    throw new NullReferenceException(
                        $"Null encountered while resolving '{memberName}' in path '{path}'.");

                // Try property first
                PropertyInfo prop = currentType.GetProperty(
                    memberName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (prop != null)
                {
                    current = prop.GetValue(current);
                    currentType = prop.PropertyType;
                    continue;
                }

                // Try field
                FieldInfo field = currentType.GetField(
                    memberName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (field != null)
                {
                    current = field.GetValue(current);
                    currentType = field.FieldType;
                    continue;
                }

                throw new MissingMemberException(
                    currentType.FullName, memberName);
            }

            if (current is T value)
                return value;

            throw new InvalidCastException(
                $"Cannot cast value of type '{currentType.FullName}' to '{typeof(T).FullName}'.");
        }

        public static void SetObjectValue<T>(object obj, string path, T value)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            string[] members = path.Split('.');

            var stack = new Stack<(object instance, MemberInfo member)>();

            object current = obj;
            Type currentType = obj.GetType();

            // Traverse and record path
            for (int i = 0; i < members.Length; i++)
            {
                string name = members[i];

                MemberInfo member =
                    (MemberInfo)currentType.GetProperty(
                        name,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? currentType.GetField(
                        name,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (member == null)
                    throw new MissingMemberException(currentType.FullName, name);

                stack.Push((current, member));

                if (i < members.Length - 1)
                {
                    current = GetMemberValue(current, member)
                              ?? throw new NullReferenceException($"'{name}' is null.");
                    currentType = current.GetType();
                }
            }

            // Set deepest value
            var (parent, lastMember) = stack.Pop();
            SetMemberValue(parent, lastMember, value);

            // Write back structs
            while (stack.Count > 0)
            {
                var (container, member) = stack.Pop();
                SetMemberValue(container, member, parent);
                parent = container;
            }
        }

        private static object GetMemberValue(object obj, MemberInfo member)
        {
            return member switch
            {
                PropertyInfo p => p.GetValue(obj),
                FieldInfo f => f.GetValue(obj),
                _ => throw new InvalidOperationException()
            };
        }

        private static void SetMemberValue(object obj, MemberInfo member, object value)
        {
            switch (member)
            {
                case PropertyInfo p:
                    p.SetValue(obj, value);
                    break;
                case FieldInfo f:
                    f.SetValue(obj, value);
                    break;
            }
        }
    }
}
