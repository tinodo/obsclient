namespace OBSStudioClient.Tests.Unit
{
    using System.Text;
    using Xunit;

    /// <summary>
    /// Unit tests for the internal <see cref="StringBuilderCache"/> helper used by the receive loop.
    /// </summary>
    public class StringBuilderCacheTests
    {
        [Fact]
        public void Acquire_WithoutPriorRelease_ReturnsEmptyBuilder()
        {
            var sb = StringBuilderCache.Acquire();

            Assert.Equal(0, sb.Length);
        }

        [Fact]
        public void Release_ThenAcquire_ReturnsClearedSameInstance()
        {
            var first = StringBuilderCache.Acquire();
            first.Append("leftover content");

            StringBuilderCache.Release(first);
            var second = StringBuilderCache.Acquire();

            Assert.Same(first, second);
            Assert.Equal(0, second.Length);
        }
    }
}
