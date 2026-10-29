namespace OBSStudioClient.Tests.Unit
{
    using System.Reflection;
    using Xunit;

    /// <summary>
    /// The shape of the public API that 4.0.0 froze. A public type cannot be made internal, and a class that can be derived from cannot
    /// be sealed, without breaking the code of someone; the other way round costs nobody anything. So the API starts closed, and these
    /// tests keep it from opening by accident: what is made public, or left open for derived classes, has to be added here on purpose.
    /// </summary>
    public class PublicApiTests
    {
        private static readonly Assembly Library = typeof(ObsClient).Assembly;

        private static IEnumerable<Type> PublicTypes => Library.GetExportedTypes();

        [Fact]
        public void TheWireProtocol_IsNotPublic()
        {
            // What the client and OBS Studio exchange to connect, to deliver an event and to answer a batch. An application gets what
            // these carry through ObsClient and its events, and has no use for the messages themselves.
            string[] wire =
            [
                "HelloMessage", "IdentifyMessage", "IdentifiedMessage", "ReidentifyMessage", "EventMessage", "RequestBatchResponseMessage",
                "IMessage", "Authentication", "EventTypeConverter", "NullableNumberToNumberConverter",
            ];

            var exposed = PublicTypes.Select(type => type.Name).Intersect(wire).Order().ToList();

            Assert.Empty(exposed);
        }

        [Fact]
        public void TheMessages_ThatAreNeededForABatch_AreTheOnlyPublicOnes()
        {
            string[] expected = ["RequestBatchMessage", "RequestMessage", "RequestResponseMessage"];

            var messages = PublicTypes.Where(type => type.Namespace == "OBSStudioClient.Messages").Select(type => type.Name).Order().ToList();

            Assert.Equal(expected, messages);
        }

        [Fact]
        public void TheOnlyPublicInterface_IsIResponse()
        {
            var interfaces = PublicTypes.Where(type => type.IsInterface).Select(type => type.FullName).ToList();

            Assert.Equal(["OBSStudioClient.Interfaces.IResponse"], interfaces);
        }

        [Fact]
        public void EveryPublicClass_IsSealed_ExceptTheExceptionsAndTheBaseOfTransitionResponse()
        {
            // Transition is what TransitionResponse (the answer to GetCurrentSceneTransition) adds to, so it cannot be sealed. An exception
            // can be derived from by the code that catches it, as it can in the framework.
            var open = PublicTypes
                .Where(type => type.IsClass && !type.IsSealed && !typeof(Exception).IsAssignableFrom(type))
                .Select(type => type.FullName)
                .Order()
                .ToList();

            Assert.Equal(["OBSStudioClient.Classes.Transition"], open);
        }
    }
}
