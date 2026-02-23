using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NiflySharp.Helpers
{
    /// <summary>
    /// Deep copy of members, ICloneable members and collection members.
    /// </summary>
    /// <remarks>
    /// - Supports circular references via a visited object cache.
    /// - Deep-copies all fields and properties(including private ones).
    /// - Recursively handles ICollection, IDictionary, arrays, and ICloneable objects.
    /// - Uses reflection for generality—no need to customize per-type.
    /// </remarks>
    public static class DeepCopyHelper
    {
        public static T DeepCopy<T>(T original)
        {
            return (T)DeepCopyInternal(original, []);
        }

        private static object DeepCopyInternal(object original, Dictionary<object, object> visited)
        {
            if (original == null)
                return null;

            Type type = original.GetType();

            // Prevent circular references
            if (visited.TryGetValue(original, out object visitedValue))
                return visitedValue;

            // Simple types or structs (value types except Nullable)
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type.IsValueType && Nullable.GetUnderlyingType(type) == null)
                return original;

            // ICloneable
            // Skip Clone() if it's a NiObject or derived from it
            if (original is ICloneable cloneable && !IsNiObject(type))
            {
                var clone = cloneable.Clone();
                visited[original] = clone;
                return clone;
            }

            // ICollection (List, Array, etc.)
            if (original is ICollection originalCollection)
            {
                var collectionType = type;
                var elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments().FirstOrDefault() ?? typeof(object);

                var newCollection = type.IsArray
                    ? Array.CreateInstance(elementType, originalCollection.Count)
                    : Activator.CreateInstance(collectionType);

                visited[original] = newCollection;

                if (newCollection is IList newList)
                {
                    foreach (var item in originalCollection)
                        newList.Add(DeepCopyInternal(item, visited));
                }
                else if (newCollection is IDictionary newDict && original is IDictionary originalDict)
                {
                    foreach (DictionaryEntry entry in originalDict)
                    {
                        var keyCopy = DeepCopyInternal(entry.Key, visited);
                        var valueCopy = DeepCopyInternal(entry.Value, visited);
                        newDict.Add(keyCopy, valueCopy);
                    }
                }
                else if (newCollection is Array newArray)
                {
                    int index = 0;
                    foreach (var item in originalCollection)
                    {
                        newArray.SetValue(DeepCopyInternal(item, visited), index++);
                    }
                }

                return newCollection;
            }

            // Complex object
            object copy = Activator.CreateInstance(type);
            visited[original] = copy;

            var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (var field in type.GetFields(bindingFlags))
            {
                var fieldValue = field.GetValue(original);
                var copiedValue = DeepCopyInternal(fieldValue, visited);

                if (copiedValue != null && copiedValue.GetType().IsGenericType && field.FieldType.IsGenericType)
                {
                    // Handle NiBlockRef<> mismatch via CloneRefAs
                    // and handle NiBlockPtr<> mismatch via ClonePtrAs
                    if ((copiedValue.GetType().GetGenericTypeDefinition() == typeof(NiBlockRef<>) && field.FieldType.GetGenericTypeDefinition() == typeof(NiBlockRef<>)) ||
                        (copiedValue.GetType().GetGenericTypeDefinition() == typeof(NiBlockPtr<>) && field.FieldType.GetGenericTypeDefinition() == typeof(NiBlockPtr<>)))
                    {
                        copiedValue = CloneNiBlockRefOrPtr(copiedValue, field.FieldType);
                    }
                    // Handle NiBlockRefArray<> mismatch via CloneRefAs on all content
                    // and handle NiBlockPtrArray<> mismatch via ClonePtrAs on all content
                    else if ((copiedValue.GetType().GetGenericTypeDefinition() == typeof(NiBlockRefArray<>) && field.FieldType.GetGenericTypeDefinition() == typeof(NiBlockRefArray<>)) ||
                        (copiedValue.GetType().GetGenericTypeDefinition() == typeof(NiBlockPtrArray<>) && field.FieldType.GetGenericTypeDefinition() == typeof(NiBlockPtrArray<>)))
                    {
                        copiedValue = CloneNiBlockRefOrPtrArray(copiedValue, field.FieldType);
                    }
                }

                if (copiedValue == null || field.FieldType.IsInstanceOfType(copiedValue))
                {
                    field.SetValue(copy, copiedValue);
                }
                else
                {
                    throw new Exception($"[DeepCopy]: Field '{field.Name}' cannot be set due to instance type mismatch.");
                }
            }

            foreach (var prop in type.GetProperties(bindingFlags).Where(p => p.CanRead && p.CanWrite))
            {
                try
                {
                    var propValue = prop.GetValue(original);
                    var copiedValue = DeepCopyInternal(propValue, visited);

                    if (copiedValue != null && copiedValue.GetType().IsGenericType && prop.PropertyType.IsGenericType)
                    {
                        // Handle NiBlockRef<> mismatch via CloneRefAs
                        // and handle NiBlockPtr<> mismatch via ClonePtrAs
                        if ((copiedValue.GetType().GetGenericTypeDefinition() == typeof(NiBlockRef<>) && prop.PropertyType.GetGenericTypeDefinition() == typeof(NiBlockRef<>)) ||
                            (copiedValue.GetType().GetGenericTypeDefinition() == typeof(NiBlockPtr<>) && prop.PropertyType.GetGenericTypeDefinition() == typeof(NiBlockPtr<>)))
                        {
                            copiedValue = CloneNiBlockRefOrPtr(copiedValue, prop.PropertyType);
                        }
                        // Handle NiBlockRefArray<> mismatch via CloneRefAs on all content
                        // and handle NiBlockPtrArray<> mismatch via ClonePtrAs on all content
                        else if ((copiedValue.GetType().GetGenericTypeDefinition() == typeof(NiBlockRefArray<>) && prop.PropertyType.GetGenericTypeDefinition() == typeof(NiBlockRefArray<>)) ||
                            (copiedValue.GetType().GetGenericTypeDefinition() == typeof(NiBlockPtrArray<>) && prop.PropertyType.GetGenericTypeDefinition() == typeof(NiBlockPtrArray<>)))
                        {
                            copiedValue = CloneNiBlockRefOrPtrArray(copiedValue, prop.PropertyType);
                        }
                    }

                    if (copiedValue == null || prop.PropertyType.IsInstanceOfType(copiedValue))
                    {
                        prop.SetValue(copy, copiedValue);
                    }
                    else
                    {
                        throw new Exception($"[DeepCopy]: Property '{prop.Name}' cannot be set due to instance type mismatch.");
                    }
                }
                catch { /* Skip property if access fails */ }
            }

            return copy;
        }

        private static bool IsNiObject(Type type)
        {
            return typeof(NiObject).IsAssignableFrom(type);
        }

        private static object CloneNiBlockRefOrPtr(object original, Type targetType)
        {
            if (original == null || targetType == null)
                return null;

            var originalType = original.GetType();

            if (!targetType.IsGenericType)
                return null;

            var expectedGenericTypeDef = targetType.GetGenericTypeDefinition();
            var expectedGenericArg = targetType.GetGenericArguments().FirstOrDefault();

            if (expectedGenericArg == null)
                return null;

            // Determine if it's a NiBlockRef<T> or NiBlockPtr<T>
            MethodInfo cloneMethod = null;

            if (expectedGenericTypeDef == typeof(NiBlockRef<>))
            {
                cloneMethod = originalType.GetMethod("CloneRefAs");
            }
            else if (expectedGenericTypeDef == typeof(NiBlockPtr<>))
            {
                cloneMethod = originalType.GetMethod("ClonePtrAs");
            }

            if (cloneMethod == null)
                return null;

            // Make and invoke the appropriate generic method
            var genericMethod = cloneMethod.MakeGenericMethod(expectedGenericArg);
            return genericMethod.Invoke(original, null);
        }

        private static object CloneNiBlockRefOrPtrArray(object original, Type targetType)
        {
            if (original == null || !original.GetType().IsGenericType)
                return null;

            var originalType = original.GetType();
            var genericDef = originalType.GetGenericTypeDefinition();

            // Determine if it's a NiBlockRefArray<T> or NiBlockPtrArray<T>
            bool isRefArray = genericDef == typeof(NiBlockRefArray<>);
            bool isPtrArray = false;
            if (!isRefArray)
            {
                isPtrArray = genericDef == typeof(NiBlockPtrArray<>);
                if (!isPtrArray)
                    return null;
            }

            var sourceGenericArg = originalType.GetGenericArguments()[0];
            var targetGenericArg = targetType.GetGenericArguments()[0];

            // Get the internal _refs field (List<NiBlockRef<TSource>>)
            var refsField = originalType.GetField("_refs", BindingFlags.NonPublic | BindingFlags.Instance);
            var originalRefs = refsField.GetValue(original) as IEnumerable;

            // Create a List<NiBlockRef<TTarget>>
            // Currently, NiBlockPtrArray has a NiBlockRef list instead of a NiBlockPtr list
            var niBlockRefType = typeof(NiBlockRef<>).MakeGenericType(targetGenericArg);

            var listType = typeof(List<>).MakeGenericType(niBlockRefType);
            var newList = (IList)Activator.CreateInstance(listType);

            // Convert each NiBlockRef<TSource> to NiBlockRef<TTarget> via CloneNiBlockRefOrPtr
            foreach (var refItem in originalRefs)
            {
                if (refItem == null)
                {
                    newList.Add(null);
                }
                else
                {
                    var clonedRef = CloneNiBlockRefOrPtr(refItem, niBlockRefType);
                    newList.Add(clonedRef);
                }
            }

            // After the list is built, assign the .List property on each NiBlockRef<T>
            foreach (var item in newList)
            {
                if (item == null) continue;

                var listProp = item.GetType().GetProperty("List", BindingFlags.Public | BindingFlags.Instance);
                if (listProp != null && listProp.CanWrite)
                {
                    listProp.SetValue(item, newList);
                }
            }

            // Create new NiBlockRef/PtrArray<TTarget> with the list constructor
            var ctor = targetType.GetConstructor([listType]);
            if (ctor == null)
                throw new InvalidOperationException("Constructor with List<NiBlockRef<T>> parameter not found.");

            return ctor.Invoke([newList]);
        }
    }
}
