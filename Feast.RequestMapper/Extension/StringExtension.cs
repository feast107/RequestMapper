using System;

namespace Feast.RequestMapper.Extension
{
    public static class StringExtension
    {
        public static string ToUpperCamelCase(this string value) => $"{(char)(value[0] - 32)}{value.Substring(1)}";
        public static string ToLowerCamelCase(this string value) => $"{(char)(value[0] + 32)}{value.Substring(1)}";
        public static string AnotherCamelCase(this string value) =>
            value.Length == 0
                ? throw new ArgumentNullException($"At least one char in string {value}")
                : value[0] switch
                {
                    >= 'a' and <= 'z' => value.ToUpperCamelCase(),
                    >= 'A' and <= 'Z' => value.ToLowerCamelCase(),
                    _ => throw new ArgumentOutOfRangeException($"First char should be letter but {value[0]}")
                };
    }
}
