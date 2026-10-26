namespace OBSStudioClient.Tests.Conformance
{
    using System.Reflection;

    /// <summary>Reads the registries in which the client keeps the response type of each request and the arguments type of each event.</summary>
    internal static class Registries
    {
        /// <param name="owner">The class that keeps the registry: <c>RequestResponseMessage</c> or <c>EventMessage</c>.</param>
        public static Dictionary<TKey, Type> ReadTypeMap<TKey>(Type owner)
            where TKey : notnull
        {
            var field = owner.GetField("_responseTypeMap", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException($"{owner.Name} no longer has the _responseTypeMap registry these tests inspect.");
            return (Dictionary<TKey, Type>)field.GetValue(null)!;
        }
    }
}
