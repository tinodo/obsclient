namespace OBSStudioClient.Tests.TestSupport
{
    using Xunit;

    /// <summary>
    /// Where the source files of the model are (<c>OBSClient\Model</c>): the folder next to <c>OBSClient.sln</c> that is above the place where the tests run, or null when the tests run
    /// somewhere else (copied away from the repository).
    /// </summary>
    internal static class ModelSource
    {
        public static string? Directory { get; } = Find();

        private static string? Find()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                var model = Path.Combine(directory.FullName, "OBSClient", "Model");
                if (File.Exists(Path.Combine(directory.FullName, "OBSClient.sln")) && System.IO.Directory.Exists(model))
                {
                    return model;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// A fact that reads the source files of the model, and is skipped when the tests do not run in the repository.
    /// </summary>
    internal sealed class ModelSourceFactAttribute : FactAttribute
    {
        public ModelSourceFactAttribute()
        {
            if (ModelSource.Directory is null)
            {
                this.Skip = "The source files of the model are not above the place where the tests run.";
            }
        }
    }
}
