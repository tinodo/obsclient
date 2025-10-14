namespace OBSStudioClient
{
    using System;
    using System.Text;

    internal static class StringBuilderCache
    {
        [ThreadStatic]
        private static StringBuilder? _cachedInstance;

        public static StringBuilder Acquire()
        {
            var sb = _cachedInstance;
            if (sb == null)
                return new StringBuilder(1024);
            _cachedInstance = null;
            sb.Clear();
            return sb;
        }

        public static void Release(StringBuilder sb)
        {
            _cachedInstance = sb;
        }
    }
}
