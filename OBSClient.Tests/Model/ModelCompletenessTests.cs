namespace OBSStudioClient.Tests.Model
{
    using System.Text.RegularExpressions;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Model.Engine;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// That <c>ObsModel</c> covers the whole protocol of OBS Studio: every request is made by the model, and every event is acted on by it, except the ones that this class lists with the reason. A request or an event
    /// that a newer protocol adds, or that somebody takes out of the model, fails here until it is covered or the list says why it is not.
    /// </summary>
    public class ModelCompletenessTests
    {
        // The requests that the model does not make by name, and why: each is either not needed (the model has the answer already) or made another way.
        private static readonly Dictionary<RequestType, string> RequestsThatNeedNothing = new()
        {
            [RequestType.Sleep] = "only useful inside a batch, as a pause between its requests, and the model sends no batch that has to wait",
            [RequestType.GetSceneItemId] = "the model knows the id of every scene item, from the list of the items and from the events",
            [RequestType.GetSceneItemIndex] = "it is ObsSceneItem.Index",
            [RequestType.GetSceneItemLocked] = "it is ObsSceneItem.IsLocked",
            [RequestType.GetSceneItemSource] = "it is ObsSceneItem.Source",
            [RequestType.GetGroupList] = "a group is found through the scene items that show it (ObsScene.IsGroup)",
            [RequestType.GetVersion] = "the model asks it with ObsClient.GetVersion when it connects, and shows it (ObsModel.ObsVersion, WebSocketVersion, Platform, SupportedImageFormats)",
        };

        // The events that the engine of the model does not act on, and why.
        private static readonly Dictionary<EventType, string> EventsThatTakeAnotherWay = new()
        {
            [EventType.Unknown] = "stands for an event type that this version of the client does not know",
            [EventType.InputVolumeMeters] = "the levels of the audio are a stream and no state: the host takes them (ObsHighVolume.AudioLevels, ObsModel.AudioLevels), and not the engine",
        };

        [Fact]
        public void EveryEventOfTheProtocol_IsActedOnByTheModel_ExceptTheOnesThatTakeAnotherWay()
        {
            var notActedOn = Enum.GetValues<EventType>().Where(type => !ModelEngine.Handles(type)).Order().ToArray();

            Assert.Equal(EventsThatTakeAnotherWay.Keys.Order().ToArray(), notActedOn);
        }

        [ModelSourceFact]
        public void EveryRequestOfTheProtocol_IsMadeByTheModel_ExceptTheOnesThatNeedNothing()
        {
            var named = new HashSet<string>(StringComparer.Ordinal);
            var name = new Regex(@"\bRequestType\.(\w+)", RegexOptions.CultureInvariant);
            foreach (var file in Directory.EnumerateFiles(ModelSource.Directory!, "*.cs", SearchOption.AllDirectories))
            {
                foreach (var line in File.ReadLines(file))
                {
                    // A request that a comment names is not made.
                    var comment = line.IndexOf("//", StringComparison.Ordinal);
                    foreach (Match match in name.Matches(comment < 0 ? line : line[..comment]))
                    {
                        named.Add(match.Groups[1].Value);
                    }
                }
            }

            var notMade = Enum.GetValues<RequestType>().Where(type => !named.Contains(type.ToString())).Order().ToArray();

            Assert.Equal(RequestsThatNeedNothing.Keys.Order().ToArray(), notMade);
        }
    }
}
