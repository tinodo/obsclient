namespace OBSStudioClient.Tests.Unit
{
    /// <summary>
    /// The package is one thing: the library. The protocol definition and the code generator that produce parts of it
    /// are inputs of the build under tools/; nothing of them may travel with the assembly, not even as an embedded file
    /// (tools/verify-package.py checks the package itself in CI).
    /// </summary>
    public class PackagingTests
    {
        [Fact]
        public void TheLibrary_CarriesNoEmbeddedFiles()
        {
            Assert.Empty(typeof(ObsClient).Assembly.GetManifestResourceNames());
        }

        [Fact]
        public void TheLibrary_ReferencesNothingBesidesTheFrameworkAndTheMicrosoftExtensions()
        {
            var unexpected = typeof(ObsClient).Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(name => !name.StartsWith("System", StringComparison.Ordinal)
                    && !name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
                    && name is not ("mscorlib" or "netstandard"))
                .ToList();

            Assert.Empty(unexpected);
        }
    }
}
