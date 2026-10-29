namespace OBSStudioClient.Tests.Fuzz
{
    using FsCheck;
    using FsCheck.Fluent;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Converters;
    using OBSStudioClient.Enums;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Xunit;

    /// <summary>
    /// Properties of the parts of the client that are plain functions of their input: a property says what has to be true for <em>all</em>
    /// input, and the generators of FsCheck bring the input, a hundred cases at a time, that nobody wrote an example for. (An example says
    /// that something works; a property says what the code promises, which is where <c>GetLevelDb(-1)</c> was wrong until a test of
    /// examples was written for it.) A failure says the case that failed, and the seed to repeat it.
    /// </summary>
    [Trait("Category", "Property")]
    public class PropertyTests
    {
        private static readonly JsonSerializerOptions NumberOptions = new() { Converters = { new NullableNumberToNumberConverter() } };

        private static readonly JsonSerializerOptions EventTypeOptions = new() { Converters = { new EventTypeConverter() } };

        private static readonly Gen<float> FiniteFloats = ArbMap.Default.GeneratorFor<float>().Where(float.IsFinite);

        // The levels that OBS Studio sends are multipliers: zero or more, up to what a float holds.
        private static readonly Gen<float> Multipliers = Gen.Frequency(
            (6, Gen.Choose(0, 10000).Select(level => level / 10000f)),
            (2, FiniteFloats.Select(Math.Abs)),
            (1, Gen.Constant(0f)),
            (1, Gen.Choose(-100, -1).Select(level => (float)level)));

        private static readonly Gen<string> NonEmptyText = FrameGenerators.Text.Where(text => text.Length > 0);

        private static readonly Gen<Guid> NonEmptyGuid = ArbMap.Default.GeneratorFor<Guid>().Where(guid => guid != Guid.Empty);

        [Fact]
        public void ANumberThatIsWritten_IsReadAsTheSameNumber()
        {
            Prop.ForAll(
                Arb.From(FiniteFloats),
                number => JsonSerializer.Deserialize<float>(JsonSerializer.Serialize(number, NumberOptions), NumberOptions) == number)
                .QuickCheckThrowOnFailure();
        }

        [Fact]
        public void ANullNumber_IsReadAsZero()
        {
            Assert.Equal(0f, JsonSerializer.Deserialize<float>("null", NumberOptions));
        }

        [Fact]
        public void WhatIsNotANumberOrNull_IsRefusedWithAJsonException()
        {
            var notANumber = Gen.OneOf(
                Gen.Elements("true", "false"),
                FrameGenerators.Text.Select(FrameGenerators.Quote),
                FrameGenerators.Array(1),
                FrameGenerators.Object(1));

            Prop.ForAll(Arb.From(notANumber), json => ThrowsJsonException(() => JsonSerializer.Deserialize<float>(json, NumberOptions)))
                .QuickCheckThrowOnFailure();
        }

        [Fact]
        public void ANumberThatAFloatCannotHold_IsNotTheEndOfTheMessage()
        {
            // JSON allows numbers of any size: whatever a float makes of one, it is a number or a JsonException, nothing else.
            Prop.ForAll(
                Arb.From(Gen.Elements(FrameGenerators.Numbers)),
                json =>
                {
                    try
                    {
                        _ = JsonSerializer.Deserialize<float>(json, NumberOptions);
                        return true;
                    }
                    catch (JsonException)
                    {
                        return true;
                    }
                })
                .QuickCheckThrowOnFailure();
        }

        [Fact]
        public void EveryEventType_IsWrittenAndReadAsItself()
        {
            foreach (var eventType in Enum.GetValues<EventType>())
            {
                Assert.Equal(eventType, JsonSerializer.Deserialize<EventType>(JsonSerializer.Serialize(eventType, EventTypeOptions), EventTypeOptions));
            }
        }

        [Fact]
        public void AnyText_IsAnEventTypeThatItNames_OrUnknown()
        {
            var names = new HashSet<string>(Enum.GetNames<EventType>(), StringComparer.Ordinal);

            Prop.ForAll(
                Arb.From(Gen.Frequency((3, FrameGenerators.Text), (1, Gen.Elements(Enum.GetNames<EventType>())), (1, Gen.Elements(Enum.GetNames<EventType>()).Select(name => name.ToLowerInvariant())))),
                text =>
                {
                    var eventType = JsonSerializer.Deserialize<EventType>(FrameGenerators.Quote(text), EventTypeOptions);
                    return names.Contains(text) ? eventType.ToString() == text : eventType == EventType.Unknown;
                })
                .QuickCheckThrowOnFailure();
        }

        [Fact]
        public void AnEventTypeOfAnyShape_IsUnknown_AndDoesNotDerailTheMembersThatFollow()
        {
            // A converter that does not read a value to its end leaves the reader in the middle of it: the member after it is lost.
            Prop.ForAll(
                Arb.From(FrameGenerators.Value()),
                value =>
                {
                    var read = JsonSerializer.Deserialize<EventTypeHolder>("{\"type\":" + value + ",\"after\":7}");
                    return read is not null && read.After == 7 && (read.Type == EventType.Unknown || value.StartsWith('"'));
                })
                .QuickCheckThrowOnFailure();
        }

        [Fact]
        public void MulToDb_IsNeverBelowTheFloor_AndIsTheFloorForSilence()
        {
            Prop.ForAll(
                Arb.From(FiniteFloats),
                multiplier =>
                {
                    var decibels = AudioMath.MulToDb(multiplier);
                    return decibels >= -100f && (multiplier > 0f || decibels == -100f);
                })
                .QuickCheckThrowOnFailure();
        }

        [Fact]
        public void MulToDb_GrowsWithTheLevel()
        {
            Prop.ForAll(
                Arb.From(Multipliers),
                Arb.From(Multipliers),
                (first, second) => first > second || AudioMath.MulToDb(first) <= AudioMath.MulToDb(second))
                .QuickCheckThrowOnFailure();
        }

        [Fact]
        public void ADecibelValueAboveTheFloor_IsGivenBackByTheConversionToAMultiplierAndBack()
        {
            Prop.ForAll(
                Arb.From(Gen.Choose(-9500, 3000).Select(hundredths => hundredths / 100f)),
                decibels => Math.Abs(AudioMath.MulToDb(AudioMath.DbToMul(decibels)) - decibels) < 0.01f)
                .QuickCheckThrowOnFailure();
        }

        [Fact]
        public void GetLevelDb_AnswersForAnyChannelOfAnyLevels_WithoutThrowing()
        {
            // What OBS Studio sends for the levels of an input: lists of three numbers, an empty list for some inputs, and, in a message that
            // is not as it should be, a list that is null. Any channel number, also one that is not there, has an answer.
            var channel = Gen.Frequency(
                (8, Gen.ListOf(Multipliers).Select(levels => levels.ToArray())),
                (1, Gen.Constant(Array.Empty<float>())),
                (1, Gen.Constant<float[]>(null!)));
            var levels = Gen.ListOf(channel).Select(channels => channels.ToArray());
            var index = Gen.Frequency((8, Gen.Choose(-2, 6)), (1, Gen.Constant(int.MinValue)), (1, Gen.Constant(int.MaxValue)));

            Prop.ForAll(
                Arb.From(levels),
                Arb.From(index),
                (inputLevels, channelIndex) =>
                {
                    var decibels = new InputVolumeMeterResponse(inputLevels, null, "Microphone", null).GetLevelDb(channelIndex);
                    return float.IsNegativeInfinity(decibels) || decibels >= -100f;
                })
                .QuickCheckThrowOnFailure();
        }

        [Fact]
        public void ASceneRef_IsWhatItWasMadeFrom() =>
            ReferenceProperties<SceneRef>(SceneRef.FromName, SceneRef.FromUuid, reference => reference.Name, reference => reference.Uuid, reference => reference.IsSpecified);

        [Fact]
        public void AnInputRef_IsWhatItWasMadeFrom() =>
            ReferenceProperties<InputRef>(InputRef.FromName, InputRef.FromUuid, reference => reference.Name, reference => reference.Uuid, reference => reference.IsSpecified);

        [Fact]
        public void ASourceRef_IsWhatItWasMadeFrom() =>
            ReferenceProperties<SourceRef>(SourceRef.FromName, SourceRef.FromUuid, reference => reference.Name, reference => reference.Uuid, reference => reference.IsSpecified);

        // The three kinds of reference are written alike, so what is promised of one is promised of each.
        private static void ReferenceProperties<T>(
            Func<string, T> fromName,
            Func<Guid, T> fromUuid,
            Func<T, string?> name,
            Func<T, Guid?> uuid,
            Func<T, bool> isSpecified)
            where T : struct, IEquatable<T>
        {
            Prop.ForAll(
                Arb.From(NonEmptyText),
                text =>
                {
                    var reference = fromName(text);
                    return isSpecified(reference) && name(reference) == text && uuid(reference) is null && reference.ToString() == text;
                })
                .QuickCheckThrowOnFailure();

            Prop.ForAll(
                Arb.From(NonEmptyGuid),
                guid =>
                {
                    var reference = fromUuid(guid);
                    return isSpecified(reference) && name(reference) is null && uuid(reference) == guid && reference.ToString() == guid.ToString();
                })
                .QuickCheckThrowOnFailure();

            // Equal when the name is the same, whatever the string object is, and then the hash code is the same; not equal when it is not.
            Prop.ForAll(
                Arb.From(NonEmptyText),
                Arb.From(NonEmptyText),
                (first, second) =>
                {
                    var same = fromName(first).Equals(fromName(new string(first.AsSpan())))
                        && fromName(first).GetHashCode() == fromName(new string(first.AsSpan())).GetHashCode();
                    var comparison = fromName(first).Equals(fromName(second)) == string.Equals(first, second, StringComparison.Ordinal);
                    return same && comparison;
                })
                .QuickCheckThrowOnFailure();

            // A name is never a UUID, not even a name that reads as the UUID of the same object.
            Prop.ForAll(
                Arb.From(NonEmptyGuid),
                guid => !fromName(guid.ToString()).Equals(fromUuid(guid)) && !fromUuid(guid).Equals(default))
                .QuickCheckThrowOnFailure();

            Assert.False(isSpecified(default));
            Assert.Throws<ArgumentException>(() => fromName(string.Empty));
            Assert.Throws<ArgumentNullException>(() => fromName(null!));
            Assert.Throws<ArgumentException>(() => fromUuid(Guid.Empty));
        }

        // Cases that the properties above found, kept as examples: a property tries a hundred cases that change from run to run, and
        // these are the ones that must never be missed.
        [Theory]
        [InlineData("\"\\ud83d\"")]
        [InlineData("\"\\ude00\"")]
        [InlineData("\"Scene\\ud83dCreated\"")]
        public void AnEventTypeThatIsNotValidText_IsUnknown(string json)
        {
            Assert.Equal(EventType.Unknown, JsonSerializer.Deserialize<EventType>(json, EventTypeOptions));
        }

        [Fact]
        public void TheLevelOfAChannelThatIsNull_IsMinusInfinity()
        {
            var response = new InputVolumeMeterResponse([[0.5f, 0.5f, 0.5f], null!, []], null, "Microphone", null);

            Assert.True(float.IsNegativeInfinity(response.GetLevelDb(1)));
            Assert.True(float.IsNegativeInfinity(response.GetLevelDb(2)));
            Assert.Equal(AudioMath.MulToDb(0.5f), response.GetLevelDb(0));
        }

        private static bool ThrowsJsonException(Action action)
        {
            try
            {
                action();
                return false;
            }
            catch (JsonException)
            {
                return true;
            }
        }

        private sealed class EventTypeHolder
        {
            [JsonPropertyName("type")]
            [JsonConverter(typeof(EventTypeConverter))]
            public EventType Type { get; init; }

            [JsonPropertyName("after")]
            public int After { get; init; }
        }
    }
}
