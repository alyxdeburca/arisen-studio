#if SELFCHECK
using System;
using System.Reflection;

namespace ArisenStudio.Tests.SelfCheck
{
    /// <summary>
    /// Reflection helpers for reaching private members of the forms under test. An exception thrown by the invoked
    /// method is unwrapped, so a check fails with the real exception and not with TargetInvocationException.
    /// </summary>
    internal static class SelfCheckReflect
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static T GetField<T>(object target, string name)
        {
            FieldInfo field = FindField(target.GetType(), name);
            if (field == null)
            {
                throw new SelfCheckFailure("field '" + name + "' not found on " + target.GetType().Name + " (renamed?)");
            }
            return (T)field.GetValue(target);
        }

        public static object Call(object target, string name, params object[] args)
        {
            MethodInfo method = FindMethod(target.GetType(), name, Instance, args == null ? 0 : args.Length);
            return Invoke(method, target, name, target.GetType(), args);
        }

        public static object CallStatic(Type type, string name, params object[] args)
        {
            MethodInfo method = FindMethod(type, name, Static, args == null ? 0 : args.Length);
            return Invoke(method, null, name, type, args);
        }

        /// <summary>
        /// Raises a control's Click event without needing the control to be visible or enabled (PerformClick needs both).
        /// </summary>
        public static void RaiseClick(System.Windows.Forms.Control control)
        {
            MethodInfo method = typeof(System.Windows.Forms.Control).GetMethod("OnClick", Instance);
            if (method == null)
            {
                throw new SelfCheckFailure("Control.OnClick not found");
            }
            Invoke(method, control, "OnClick", control.GetType(), new object[] { EventArgs.Empty });
        }

        private static object Invoke(MethodInfo method, object target, string name, Type type, object[] args)
        {
            if (method == null)
            {
                throw new SelfCheckFailure("method '" + name + "' not found on " + type.Name + " (renamed?)");
            }

            try
            {
                return method.Invoke(target, args);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo field = t.GetField(name, Instance | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }
            return null;
        }

        private static MethodInfo FindMethod(Type type, string name, BindingFlags flags, int argCount)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                foreach (MethodInfo method in t.GetMethods(flags | BindingFlags.DeclaredOnly))
                {
                    if (method.Name == name && method.GetParameters().Length == argCount)
                    {
                        return method;
                    }
                }
            }
            return null;
        }
    }
}
#endif
