using System;
using Unity.Collections;

public static class NativeMultiHashMapExtensions {
   public static bool Remove<K, T>(this NativeMultiHashMap<K, T> HashMap, K Key, T Value) where T : struct, IEquatable<T> where K : struct, IEquatable<K> {
      if (HashMap.SelectIterator(Key, Value, out var It)) {
         HashMap.Remove(It);

         return true;
      }
      return false;
   }

   public static bool SelectIterator<K, T>(this NativeMultiHashMap<K, T> HashMap, Predicate<T> Operate, K Key, out NativeMultiHashMapIterator<K> Iterator) where T : struct where K : struct, IEquatable<K> {
      for (bool Success = HashMap.TryGetFirstValue(Key, out var Value, out var It); Success;) {
         if (Operate(Value)) {
            Iterator = It;

            return true;
         }

         Success = HashMap.TryGetNextValue(out Value, ref It);
      }
      Iterator = new NativeMultiHashMapIterator<K>();

      return false;
   }

   public static bool SelectIterator<K, T>(this NativeMultiHashMap<K, T> HashMap, K Key, T Value, out NativeMultiHashMapIterator<K> Iterator) where T : struct, IEquatable<T> where K : struct, IEquatable<K> {
      for (bool Success = HashMap.TryGetFirstValue(Key, out var ItValue, out var It); Success;) {
         if (Value.Equals(ItValue)) {
            Iterator = It;

            return true;
         }

         Success = HashMap.TryGetNextValue(out ItValue, ref It);
      }
      Iterator = new NativeMultiHashMapIterator<K>();

      return false;
   }

   public static void ForEeach<K, T>(this NativeMultiHashMap<K, T> HashMap, Predicate<T> Operate, K Key) where K : struct, IEquatable<K> where T : struct {
      for (bool Success = HashMap.TryGetFirstValue(Key, out var Value, out var It); Success;) {
         if (!Operate(Value)) {
            break;
         }

         Success = HashMap.TryGetNextValue(out Value, ref It);
      }
   }

   public static void ForEeach<K, T, A0>(this NativeMultiHashMap<K, T> HashMap, Func<T, A0, bool> Operate, K Key, A0 Arg0) where K : struct, IEquatable<K> where T : struct {
      for (bool Success = HashMap.TryGetFirstValue(Key, out var Value, out var It); Success;) {
         if (!Operate(Value, Arg0)) {
            break;
         }

         Success = HashMap.TryGetNextValue(out Value, ref It);
      }
   }

   public static void ForEeach<K, T>(this NativeMultiHashMap<K, T> HashMap, Action<T> Operate, K Key) where K : struct, IEquatable<K> where T : struct {
      for (bool Success = HashMap.TryGetFirstValue(Key, out var Value, out var It); Success;) {
         Operate(Value);

         Success = HashMap.TryGetNextValue(out Value, ref It);
      }
   }

   public static void ForEeach<K, T, A0>(this NativeMultiHashMap<K, T> HashMap, Action<T, A0> Operate, K Key, A0 Arg0) where K : struct, IEquatable<K> where T : struct {
      for (bool Success = HashMap.TryGetFirstValue(Key, out var Value, out var It); Success;) {
         Operate(Value, Arg0);

         Success = HashMap.TryGetNextValue(out Value, ref It);
      }
   }

   public static void ForEeach<K, T, A0, A1>(this NativeMultiHashMap<K, T> HashMap, Action<T, A0, A1> Operate, K Key, A0 Arg0, A1 Arg1) where K : struct, IEquatable<K> where T : struct {
      for (bool Success = HashMap.TryGetFirstValue(Key, out var Value, out var It); Success;) {
         Operate(Value, Arg0, Arg1);

         Success = HashMap.TryGetNextValue(out Value, ref It);
      }
   }
}