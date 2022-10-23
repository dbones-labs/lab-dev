namespace Dev.Infrastructure;

public static class DictionaryExtensions
{
    public static IDictionary<TKey, TValue> Update<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
    {
        if (dictionary.TryGetValue(key, out var entry))
        {
            if (entry.Equals(value))
            {
                dictionary[key] = value;
            }
        }
        else
        {
            dictionary.Add(key, value);
        }

        return dictionary;
    }

    public static IDictionary<TKey, TValue> UpdateRange<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, IDictionary<TKey, TValue> source)
    {
        foreach (var value in source)
        {
            dictionary.Update(value.Key, value.Value);
        }

        return dictionary;
    }
}