using UnityEngine;
using System.Diagnostics;

namespace FoW
{
    public enum EnableIfComparison
    {
        Equal,
        NotEqual,
        Greater,
        Less,
        GreaterThanOrEqual,
        LessThanOrEqual
    }

    [System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false)]
    [Conditional("UNITY_EDITOR")]
    public class EnableIfAttribute : PropertyAttribute
    {
        public string valueName;
        public EnableIfComparison comparison;
        public object[] targetValues;

        public EnableIfAttribute(string valuename, EnableIfComparison comparison, params object[] targetvalues)
        {
            valueName = valuename;
            this.comparison = comparison;
            targetValues = targetvalues;
        }

        bool DoTest<T>(T value, System.Func<T, T, bool> dotest)
        {
            if (targetValues.Length == 0)
                throw new System.Exception("No target values were specified for EnableIfAttribute.");

            foreach (object targetvalue in targetValues)
            {
                T target = (T)targetvalue;
                bool result = dotest(value, target);
                if (result)
                    return true;
            }
            return false;
        }

        public bool IsEnabled(bool value)
        {
            return DoTest(value, (v, t) =>
            {
                if (comparison == EnableIfComparison.Equal)
                    return t == v;
                if (comparison == EnableIfComparison.NotEqual)
                    return t != v;
                return false;
            });
        }

        public bool IsEnabled(int value)
        {
            return DoTest(value, (v, t) =>
            {
                if (comparison == EnableIfComparison.Equal)
                    return v == t;
                if (comparison == EnableIfComparison.NotEqual)
                    return v != t;
                if (comparison == EnableIfComparison.Greater)
                    return v > t;
                if (comparison == EnableIfComparison.Less)
                    return v < t;
                if (comparison == EnableIfComparison.GreaterThanOrEqual)
                    return v >= t;
                if (comparison == EnableIfComparison.LessThanOrEqual)
                    return v <= t;
                return false;
            });
        }

        public bool IsEnabled(float value)
        {
            return DoTest(value, (v, t) =>
            {
                if (comparison == EnableIfComparison.Equal)
                    return v == t;
                if (comparison == EnableIfComparison.NotEqual)
                    return v != t;
                if (comparison == EnableIfComparison.Greater)
                    return v > t;
                if (comparison == EnableIfComparison.Less)
                    return v < t;
                if (comparison == EnableIfComparison.GreaterThanOrEqual)
                    return v >= t;
                if (comparison == EnableIfComparison.LessThanOrEqual)
                    return v <= t;
                return false;
            });
        }

        public bool IsEnabled(Object value)
        {
            return DoTest(value, (v, t) =>
            {
                if (comparison == EnableIfComparison.Equal)
                    return v == t;
                if (comparison == EnableIfComparison.NotEqual)
                    return v != t;
                return false;
            });
        }
    }
}
