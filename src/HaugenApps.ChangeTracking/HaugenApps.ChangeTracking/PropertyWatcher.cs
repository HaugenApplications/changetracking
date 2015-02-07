using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using HaugenApps.HaugenCore;

namespace HaugenApps.ChangeTracking
{
    public class PropertyWatcher : INotifyPropertyChanged
    {
        public enum PropertyWatcherAccessMode
        {
            /// <summary>
            /// Default. The property watcher can be edited at any time.
            /// </summary>
            ReadWrite,

            /// <summary>
            /// The property watcher can only be read.
            /// </summary>
            ReadOnly,

            /// <summary>
            /// The property watcher can be read or cleared, but not set.
            /// </summary>
            NoSet
        }

        public PropertyWatcher(Type type, bool LogHistory = false)
        {
            this._Type = type;
            this._LogHistory = LogHistory;
        }

        public PropertyWatcher(Type type, object initial, bool LogHistory = false)
            : this(type, LogHistory)
        {
            if (!type.IsInstanceOfType(initial))
                throw new ArgumentException("Argument \"initial\" must be of type \"type.\"");

            foreach (var v in type.GetProperties())
            {
                Set(v, v.GetValue(initial, null));
            }
        }

        public PropertyWatcher(object initial, bool LogHistory = false) : this(initial.GetType(), initial, LogHistory) { }

        public PropertyWatcher(PropertyWatcher ReferenceCloneFrom, PropertyWatcherAccessMode AccessMode)
            : this(ReferenceCloneFrom._Type, ReferenceCloneFrom.LogHistory)
        {
            this._Histories = ReferenceCloneFrom._Histories;
            this._Values = ReferenceCloneFrom._Values;

            this._AccessMode = AccessMode;
        }

        private readonly Dictionary<string, List<object>> _Histories = new Dictionary<string, List<object>>();
        private readonly Dictionary<string, object> _Values = new Dictionary<string, object>();
        private readonly Type _Type;

        private readonly PropertyWatcherAccessMode _AccessMode;
        public PropertyWatcherAccessMode AccessMode { get { return _AccessMode; } }

        /// <summary>
        /// Creates a content-synchronized copy of this watcher, typically with a new access mode.
        /// </summary>
        /// <param name="AccessMode">The access mode to give the new watcher. Note that the supplied access mode must be at least as restrictive as the current watcher's.</param>
        public PropertyWatcher MakeReferenceCopy(PropertyWatcherAccessMode AccessMode)
        {
            if ((this.AccessMode == PropertyWatcherAccessMode.ReadOnly && (AccessMode == PropertyWatcherAccessMode.ReadWrite || AccessMode == PropertyWatcherAccessMode.NoSet)) ||
                (this.AccessMode == PropertyWatcherAccessMode.NoSet && AccessMode == PropertyWatcherAccessMode.ReadWrite))
            {
                throw new ArgumentException("The supplied access mode must be at least as restrictive as the current watcher's.");
            }

            return new PropertyWatcher(this, AccessMode);
        }

        private PropertyInfo GetPropertyInfo(string PropertyName)
        {
            return this._Type.GetProperty(PropertyName);
        }

        public bool HasValue(string PropertyName)
        {
            return HasValue(GetPropertyInfo(PropertyName));
        }
        public bool HasValue(PropertyInfo Property)
        {
            return _Values.ContainsKey(Property.Name);
        }

        protected virtual void OnSet(PropertyInfo info, object newValue)
        {
            var propChanged = PropertyChanged;
            if (propChanged != null)
                propChanged(this, new PropertyChangedEventArgs(info.Name));
        }

        public IEnumerable<KeyValuePair<PropertyInfo, object>> GetValues()
        {
            return _Values.Select(c => new KeyValuePair<PropertyInfo, object>(GetPropertyInfo(c.Key), c.Value));
        }

        public IEnumerable<object> GetHistory(PropertyInfo prop)
        {
            if (!LogHistory)
                throw new NotSupportedException("Not logging history!");

            List<object> ret;
            if (_Histories.TryGetValue(prop.Name, out ret))
                return ret;
            else
                return Enumerable.Empty<object>();
        }
        public IEnumerable<object> GetHistory(string PropertyName)
        {
            return GetHistory(GetPropertyInfo(PropertyName));
        }

        public void ClearHistory()
        {
            if (this.AccessMode == PropertyWatcherAccessMode.ReadOnly)
                throw new NotSupportedException(Error_IllegalAction);

            if (!LogHistory)
                throw new NotSupportedException("Not logging history!");

            _Histories.Clear();
        }

        public void ClearHistory(PropertyInfo prop)
        {
            if (this.AccessMode == PropertyWatcherAccessMode.ReadOnly)
                throw new NotSupportedException(Error_IllegalAction);

            if (!LogHistory)
                throw new NotSupportedException("Not logging history!");

            _Histories.Remove(prop.Name);
        }
        public void ClearHistory(string PropertyName)
        {
            ClearHistory(GetPropertyInfo(PropertyName));
        }

        public PropertyWatcher Set(string PropertyName, object Value)
        {
            return this.Set(this.GetPropertyInfo(PropertyName), Value);

        }
        public PropertyWatcher Set(PropertyInfo Property, object Value)
        {
            if (this.AccessMode == PropertyWatcherAccessMode.ReadOnly || this.AccessMode == PropertyWatcherAccessMode.NoSet)
                throw new NotSupportedException(Error_IllegalAction);

            _Values[Property.Name] = Value;

            if (LogHistory)
            {
                List<object> hist;
                if (!_Histories.TryGetValue(Property.Name, out hist))
                    _Histories[Property.Name] = (hist = new List<object>());

                hist.Add(Value);
            }

            OnSet(Property, Value);

            return this;
        }

        public PropertyWatcher Clear()
        {
            if (this.AccessMode == PropertyWatcherAccessMode.ReadOnly)
                throw new NotSupportedException(Error_IllegalAction);

            _Values.Clear();

            return this;
        }

        public PropertyWatcher Clear(string PropertyName)
        {
            return Clear(GetPropertyInfo(PropertyName));
        }
        public PropertyWatcher Clear(PropertyInfo Property)
        {
            if (this.AccessMode == PropertyWatcherAccessMode.ReadOnly)
                throw new NotSupportedException(Error_IllegalAction);

            _Values.Remove(Property.Name);

            return this;
        }

        private const string Error_IllegalAction = "Cannot perform this action on a watcher with this access mode.";

        public object Get(PropertyInfo Property)
        {
            return _Values[Property.Name];
        }
        public object Get(string PropertyName)
        {
            return Get(GetPropertyInfo(PropertyName));
        }

        public Type GetPropertyType(string PropertyName)
        {
            return GetPropertyInfo(PropertyName).PropertyType;
        }

        public object this[PropertyInfo PropertyName]
        {
            get { return Get(PropertyName); }
            set { Set(PropertyName, value); }
        }
        public object this[string PropertyName]
        {
            get { return Get(PropertyName); }
            set { Set(PropertyName, value); }
        }

        public bool TryGet(string PropertyName, out object result)
        {
            return TryGet(GetPropertyInfo(PropertyName), out result);
        }
        public virtual bool TryGet(PropertyInfo Property, out object result)
        {
            if (HasValue(Property))
            {
                result = Get(Property);
                return true;
            }
            else
            {
                result = null;
                return false;
            }
        }

        public PropertyWatcher SetIfEmpty(string PropertyName, object Value)
        {
            return SetIfEmpty(GetPropertyInfo(PropertyName), Value);
        }
        public virtual PropertyWatcher SetIfEmpty(PropertyInfo Property, object Value)
        {
            if (!HasValue(Property))
                return Set(Property, Value);
            else
                return this;
        }

        private readonly bool _LogHistory;
        public bool LogHistory { get { return _LogHistory; } }

        public event PropertyChangedEventHandler PropertyChanged;
    }
    public class PropertyWatcher<T> : PropertyWatcher
    {
        public PropertyWatcher(bool LogHistory = false) : base(typeof(T), LogHistory) { }
        public PropertyWatcher(T Initial, bool LogHistory = false) : base(typeof(T), Initial, LogHistory) { }

        protected PropertyWatcher(PropertyWatcher<T> ReferenceCloneFrom, PropertyWatcherAccessMode AccessMode)
            : base(ReferenceCloneFrom, AccessMode)
        {

        }

        public new PropertyWatcher<T> MakeReferenceCopy(PropertyWatcherAccessMode AccessMode)
        {
            return new PropertyWatcher<T>(this, AccessMode);
        }

        public new PropertyWatcher<T> Clear()
        {
            return (PropertyWatcher<T>)base.Clear();
        }

        public IEnumerable<T2> GetHistory<T2>(Expression<Func<T, T2>> Property)
        {
            return GetHistory(Reflection.GetPropertyInfo(Property)).Cast<T2>();
        }
        public void ClearHistory<T2>(Expression<Func<T, T2>> Property)
        {
            ClearHistory(Reflection.GetPropertyInfo(Property));
        }


        public bool HasValue(Expression<Func<T, object>> Property)
        {
            return HasValue(Reflection.GetPropertyInfo(Property));
        }


        public PropertyWatcher<T> Set<T2>(Expression<Func<T, T2>> Property, T2 Value)
        {
            return (PropertyWatcher<T>)Set(Reflection.GetPropertyInfo(Property), Value);
        }

        public PropertyWatcher<T> Clear(Expression<Func<T, object>> Property)
        {
            return (PropertyWatcher<T>)Clear(Reflection.GetPropertyInfo(Property));
        }

        public T2 Get<T2>(Expression<Func<T, T2>> Property)
        {
            return (T2)Get(Reflection.GetPropertyInfo(Property));
        }
        public bool TryGet<T2>(Expression<Func<T, T2>> Property, out T2 result)
        {
            object ret;
            if (TryGet(Reflection.GetPropertyInfo(Property), out ret))
            {
                result = (T2)ret;
                return true;
            }
            else
            {
                result = default(T2);
                return false;
            }
        }
        public PropertyWatcher<T> SetIfEmpty<T2>(Expression<Func<T, T2>> Property, T2 Value)
        {
            return (PropertyWatcher<T>)SetIfEmpty(Reflection.GetPropertyInfo(Property), Value);
        }
        public T LoadToInstance(ref T ret)
        {
            foreach (var v in GetValues())
            {
                v.Key.SetValue(ret, v.Value, null);
            }

            return ret;
        }
    }
}