namespace Shittim.Extensions
{
    /// <summary>
    /// Extension methods for string manipulation and formatting.
    /// </summary>
    public static class StringExtension
    {
        /// <summary>
        /// Capitalizes the first character of the string using invariant culture.
        /// Used primarily for command name formatting.
        /// </summary>
        /// <param name="str">The string to capitalize.</param>
        /// <returns>A new string with the first character in uppercase.</returns>
        public static string Capitalize(this string str)
        {
            return char.ToUpperInvariant(str[0]) + str.Substring(1);
        }
    }
}
