using System;

namespace Bazaar.Trinkets
{

    public static class StringExtensionMethods
    {

        public static int CountOccurrences(this string str, string value, StringComparison comparisonType = StringComparison.InvariantCulture)
        {
            int counter = 0;
            if (value != null)
            {
                int strLength = str.Length;
                int valueLength = value.Length;
                for (int i = 0; i < strLength; i++)
                {
                    if (i + valueLength <= strLength)
                    {
                        string snippet = str.Substring(i, valueLength);

                        if (str.Substring(i, valueLength).Equals(value, comparisonType))
                        {
                            counter++;
                        }

                    }
                }
            }
            return counter;
        }

    }

}
