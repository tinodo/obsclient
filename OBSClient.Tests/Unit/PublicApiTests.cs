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
            // What the client and OBS Studio exchange to connect, to deliver an event and to answer a batch, and what those messages are made of
            // (their op codes, the names of the events, the modifier keys of a hotkey). An application gets what the messages carry through
            // ObsClient and its events, and has no use for the messages themselves.
            string[] wire =
            [
                "HelloMessage", "IdentifyMessage", "IdentifiedMessage", "ReidentifyMessage", "EventMessage", "RequestBatchResponseMessage",
                "IMessage", "Authentication", "EventTypeConverter", "NullableNumberToNumberConverter", "OpCode", "EventType", "KeyModifiers",
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

        [Fact]
        public void EveryPublicType_IsUsedByTheApi_ExceptTheClientTheExceptionsAndTheResponses()
        {
            // A type is public because something in the API takes it, returns it, holds it, raises it or derives from it. One that nothing
            // uses is dead, and it cannot be removed after 4.0.0 without breaking the code of someone: it goes now, or it is internal. The
            // responses are left out because a batch hands them out as IResponse, and the exceptions because they are thrown.
            var used = new HashSet<Type>();
            foreach (var type in PublicTypes)
            {
                var related = new List<Type>();
                if (type.BaseType is not null)
                {
                    related.Add(type.BaseType);
                }

                related.AddRange(type.GetInterfaces());
                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    switch (member)
                    {
                        case MethodBase { IsPublic: true } or MethodBase { IsFamily: true } or MethodBase { IsFamilyOrAssembly: true }:
                            var method = (MethodBase)member;
                            related.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
                            if (method is MethodInfo info)
                            {
                                related.Add(info.ReturnType);
                            }

                            break;
                        case PropertyInfo property when property.GetMethod?.IsPublic == true || property.SetMethod?.IsPublic == true:
                            related.Add(property.PropertyType);
                            break;
                        case FieldInfo { IsPublic: true } field:
                            related.Add(field.FieldType);
                            break;
                        case EventInfo { AddMethod.IsPublic: true } @event:
                            related.Add(@event.EventHandlerType!);
                            break;
                    }
                }

                foreach (var other in related.SelectMany(TypesIn).Where(other => other != type))
                {
                    used.Add(other);
                }
            }

            var unused = PublicTypes
                .Where(type => type != typeof(ObsClient)
                    && type.Namespace is not ("OBSStudioClient.Exceptions" or "OBSStudioClient.Responses")
                    && !used.Contains(type))
                .Select(type => type.FullName)
                .Order()
                .ToList();

            Assert.True(unused.Count == 0, "Public types that nothing in the API uses (remove them, or make them internal): " + string.Join(", ", unused) + ".");
        }

        // The type itself, and every type that it is made of: the element of an array, the arguments of a generic type.
        private static IEnumerable<Type> TypesIn(Type type)
        {
            while (type.HasElementType)
            {
                type = type.GetElementType()!;
            }

            yield return type;
            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    foreach (var inner in TypesIn(argument))
                    {
                        yield return inner;
                    }
                }
            }
        }
    }
}
