using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace HaugenApps.ChangeTracking
{
    public static class PassivePropertyWatcher
    {
        private const string FieldName = "__PropertyWatcher";

        private static readonly ConcurrentDictionary<Tuple<Type, bool>, Type> _Cache = new ConcurrentDictionary<Tuple<Type, bool>, Type>();

        public static Type GetWrapperType<T>(bool logHistory = false)
        {
            return GetWrapperType(typeof(T), logHistory);
        }

        public static Type GetWrapperType(Type type, bool logHistory = false)
        {
            return _Cache.GetOrAdd(Tuple.Create(type, logHistory), t => GetWrapperTypeNoCache(t.Item1, t.Item2));
        }

        private static Type GetWrapperTypeNoCache(Type type, bool logHistory)
        {
            if (type.IsSealed)
                throw new ArgumentException("The supplied type must not be sealed.");

            AppDomain myDomain = AppDomain.CurrentDomain;
            AssemblyName myAsmName = new AssemblyName(type.Assembly.FullName.Remove(type.Assembly.FullName.IndexOf(",")) + ".__PropertyWatcher");
            AssemblyBuilder myAssembly = myDomain.DefineDynamicAssembly(myAsmName, AssemblyBuilderAccess.RunAndSave);

            var module = myAssembly.DefineDynamicModule(myAsmName.Name, myAsmName.Name + ".dll");

            TypeBuilder typeBuilder = module.DefineType(type.Name, TypeAttributes.Public, type);

            var propertyWatcherType = typeof(PropertyWatcher<>).MakeGenericType(type);

            var propertyWatcherField = typeBuilder.DefineField(FieldName, propertyWatcherType, FieldAttributes.Public | FieldAttributes.InitOnly);
            var propertyWatcherConstructor = propertyWatcherType.GetConstructor(new[] { typeof(bool) });

            foreach (var oldConstructor in type.GetConstructors())
            {
                Type[] paramTypes = oldConstructor.GetParameters().Select(c => c.ParameterType).ToArray();

                ConstructorBuilder newConstructorMethodBuilder = typeBuilder.DefineConstructor(oldConstructor.Attributes, oldConstructor.CallingConvention, paramTypes);
                ILGenerator constructorIL = newConstructorMethodBuilder.GetILGenerator();

                constructorIL.Emit(OpCodes.Ldarg_0);

                constructorIL.Emit(logHistory ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                constructorIL.Emit(OpCodes.Newobj, propertyWatcherConstructor);
                constructorIL.Emit(OpCodes.Stfld, propertyWatcherField);

                constructorIL.Emit(OpCodes.Ldarg_0);

                for (int v = 0; v < paramTypes.Length; v++)
                {
                    constructorIL.Emit(OpCodes.Ldarga_S, v + 1);
                }

                constructorIL.Emit(OpCodes.Call, oldConstructor);

                constructorIL.Emit(OpCodes.Ret);
            }

            var setMethod = propertyWatcherType.GetMethod("Set", new[] { typeof(string), typeof(object) });

            foreach (var prop in type.GetProperties())
            {
                if (prop.CanWrite)
                {
                    var oldSetter = prop.GetSetMethod();

                    if (!oldSetter.IsVirtual)
                        throw new ArgumentException(string.Format("The property {0} must be made virtual.", prop.Name));


                    MethodBuilder pSet = typeBuilder.DefineMethod(oldSetter.Name, oldSetter.Attributes, oldSetter.ReturnType, new Type[] { prop.PropertyType });
                    ILGenerator pILSet = pSet.GetILGenerator();

                    pILSet.Emit(OpCodes.Ldarg_0);
                    pILSet.Emit(OpCodes.Ldarg_1);
                    pILSet.Emit(OpCodes.Call, oldSetter);

                    pILSet.Emit(OpCodes.Ldarg_0);
                    pILSet.Emit(OpCodes.Ldfld, propertyWatcherField);
                    pILSet.Emit(OpCodes.Ldstr, prop.Name);
                    pILSet.Emit(OpCodes.Ldarg_1);
                    pILSet.Emit(OpCodes.Callvirt, setMethod);
                    pILSet.Emit(OpCodes.Pop);
                    pILSet.Emit(OpCodes.Ret);

                    typeBuilder.DefineMethodOverride(pSet, oldSetter);
                }
            }

            return typeBuilder.CreateType();
        }

        public static T GetWrapper<T>(bool logHistory = false)
            where T : class, new()
        {
            var type = GetWrapperType(typeof(T), logHistory);

            // ReSharper disable once PossibleNullReferenceException
            return (T)type.GetConstructor(Type.EmptyTypes).Invoke(new object[0]);
        }

        private static PropertyWatcher GetWriteablePropertyWatcher(object arg)
        {
            var actualType = arg.GetType();

            var field = actualType.GetField(FieldName);
            if (field == null)
                throw new ArgumentException("Object was not passed through formatter.");

            return (PropertyWatcher)field.GetValue(arg);
        }
        public static PropertyWatcher GetPropertyWatcher(object arg)
        {
            return GetWriteablePropertyWatcher(arg).MakeReferenceCopy(PropertyWatcher.PropertyWatcherAccessMode.NoSet);
        }
        public static PropertyWatcher<T> GetPropertyWatcher<T>(T arg)
        {
            return ((PropertyWatcher<T>)GetWriteablePropertyWatcher(arg)).MakeReferenceCopy(PropertyWatcher.PropertyWatcherAccessMode.NoSet);
        }
    }

    public class PassivePropertyWatcher<T> where T : class, new()
    {
        public PassivePropertyWatcher(bool LogHistory = false)
        {
            this._instance = PassivePropertyWatcher.GetWrapper<T>(LogHistory);
            this._propertyWatcherCache = PassivePropertyWatcher.GetPropertyWatcher(this._instance);
        }
        public PassivePropertyWatcher(PropertyWatcher<T> CopyFrom, bool LogHistory = false)
            : this(LogHistory)
        {
            CopyFrom.LoadToInstance(ref this._instance);
        }

        private readonly T _instance;
        public T Instance { get { return this._instance; } }

        private readonly PropertyWatcher<T> _propertyWatcherCache;
        public PropertyWatcher<T> PropertyWatcher { get { return this._propertyWatcherCache; } }
    }
}
