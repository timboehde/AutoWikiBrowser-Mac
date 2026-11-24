// Mac version of AWB/WikiFunctions/Extensions.cs, copied by script without the two
// text-box helpers (GoToLine, ResetFormatting), which belong to the app.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace WikiFunctions
{
    public static class Extensions
    {
        public static void AddIfTrue(this Dictionary<string, string> dict, bool input, string key, string value)
        {
            if (input)
            {
                dict.Add(key, value);
            }
        }

        public static void AddRangeIfNotNull<T>(this List<T> list, IEnumerable<T> collection)
        {
            if (collection != null)
            {
                list.AddRange(collection);
            }

        }

        public static bool IsIn<T>(this T @this, params T[] possibles)
        {
            return ((IList) possibles).Contains(@this);
        }


        public static IDictionary<TKey, TValue> SortBy<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary,
            IEnumerable<TKey> keys
        )
        {
            var sorter = new KeyComparer<TKey>(keys);
            return new SortedDictionary<TKey, TValue>(dictionary, sorter);
        }

        public static bool IsNullOrEmpty(this string str)
        {
            return string.IsNullOrEmpty(str);
        }

        public static List<string> DistinctList(this JToken token)
        {
            return token.Select(item => item.ToString()).Distinct().ToList();
        }
    }
}
