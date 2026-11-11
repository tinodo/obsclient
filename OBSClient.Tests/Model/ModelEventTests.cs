namespace OBSStudioClient.Tests.Model
{
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The events of the model that say that something happened (a custom event, a vendor event, a screenshot, OBS Studio exiting), what it knows of the version of OBS Studio that it is
    /// connected to, and the events that it subscribes to for them.
    /// </summary>
    public class ModelEventTests
    {
        private const int GeneralEvents = 1;
        private const int VendorEvents = 512;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static int Told<T>(List<T> list)
        {
            lock (list)
            {
                return list.Count;
            }
        }

        [Fact]
        public async Task TheModel_SubscribesToTheGeneralEvents_WhateverTheApplicationAsksFor()
        {
            await using var rig = new ModelSimRig(options: new ObsModelOptions { AdditionalEvents = EventSubscriptions.None });

            await rig.ConnectAsync().WithTimeoutAsync(Timeout);

            Assert.NotNull(rig.Server.EventSubscriptions);
            Assert.Equal(EventSubscriptions.General, (EventSubscriptions)rig.Server.EventSubscriptions!.Value & EventSubscriptions.General);
            Assert.True(ModelHost.RequiredEvents.HasFlag(EventSubscriptions.General));
        }

        [Fact]
        public async Task TheHighVolumeEvents_AreNotSubscribedTo_UnlessTheApplicationAsksForThem()
        {
            const EventSubscriptions HighVolume = EventSubscriptions.InputVolumeMeters | EventSubscriptions.InputActiveStateChanged | EventSubscriptions.InputShowStateChanged | EventSubscriptions.SceneItemTransformChanged;
            await using var without = new ModelSimRig(options: new ObsModelOptions());
            await without.ConnectAsync().WithTimeoutAsync(Timeout);
            await using var with = new ModelSimRig(options: new ObsModelOptions { AdditionalEvents = EventSubscriptions.InputVolumeMeters });
            await with.ConnectAsync().WithTimeoutAsync(Timeout);
            await using var following = new ModelSimRig(options: new ObsModelOptions { HighVolume = ObsHighVolume.Transforms });
            await following.ConnectAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(EventSubscriptions.None, (EventSubscriptions)without.Server.EventSubscriptions!.Value & HighVolume);
            Assert.Equal(EventSubscriptions.InputVolumeMeters, (EventSubscriptions)with.Server.EventSubscriptions!.Value & HighVolume);
            Assert.Equal(EventSubscriptions.SceneItemTransformChanged, (EventSubscriptions)following.Server.EventSubscriptions!.Value & HighVolume);
            Assert.False(ModelHost.RequiredEvents.HasFlag(EventSubscriptions.Vendors));
        }

        [Fact]
        public async Task ACustomEvent_IsRaisedWithItsData()
        {
            await using var rig = await ModelRig.ConnectedAsync();
            var received = new List<(object? Sender, CustomEventEventArgs Args)>();
            rig.Model.CustomEvent += (sender, e) =>
            {
                lock (received)
                {
                    received.Add((sender, e));
                }
            };

            await rig.Responder.SendEventAsync("CustomEvent", new { source = "an app", number = 7 }, GeneralEvents);
            await ModelRig.EventuallyAsync(() => Told(received) == 1, "the custom event to be raised");

            var (sender, args) = Assert.Single(received);
            Assert.Same(rig.Model, sender);
            Assert.Equal("an app", args.EventData!.Value.GetProperty("source").GetString());
            Assert.Equal(7, args.EventData!.Value.GetProperty("number").GetInt32());
        }

        [Fact]
        public async Task AVendorEvent_AScreenshot_AndTheExitOfOBSStudio_AreRaisedWithTheirData()
        {
            await using var rig = await ModelRig.ConnectedAsync();
            var vendors = new List<VendorEventEventArgs>();
            var screenshots = new List<string>();
            var exits = new List<object?>();
            rig.Model.VendorEvent += (_, e) =>
            {
                lock (vendors)
                {
                    vendors.Add(e);
                }
            };
            rig.Model.ScreenshotSaved += (_, e) =>
            {
                lock (screenshots)
                {
                    screenshots.Add(e.SavedScreenshotPath);
                }
            };
            rig.Model.ExitStarted += (sender, _) =>
            {
                lock (exits)
                {
                    exits.Add(sender);
                }
            };

            await rig.Responder.SendEventAsync("VendorEvent", new { vendorName = "a-plugin", eventType = "Something", eventData = new { value = 1 } }, VendorEvents);
            await rig.Responder.SendEventAsync("ScreenshotSaved", new { savedScreenshotPath = "C:\\Pictures\\shot.png" }, GeneralEvents);
            await rig.Responder.SendEventAsync("ExitStarted", new { }, GeneralEvents);
            await ModelRig.EventuallyAsync(() => Told(vendors) == 1 && Told(screenshots) == 1 && Told(exits) == 1, "the three events to be raised");

            Assert.Equal("a-plugin", Assert.Single(vendors).VendorName);
            Assert.Equal("Something", vendors[0].EventType);
            Assert.Equal("C:\\Pictures\\shot.png", Assert.Single(screenshots));
            Assert.Same(rig.Model, Assert.Single(exits));
        }

        [Fact]
        public async Task TheEventsThatSayWhatHappened_AreRaisedInTheOrderInWhichTheyArrived_OneAtATime()
        {
            await using var rig = await ModelRig.ConnectedAsync();
            var order = new List<string>();
            var running = 0;
            var overlapped = false;
            void Enter()
            {
                if (Interlocked.Increment(ref running) > 1)
                {
                    overlapped = true;
                }
            }

            void Leave()
            {
                Interlocked.Decrement(ref running);
            }

            rig.Model.CustomEvent += (_, e) =>
            {
                Enter();
                lock (order)
                {
                    order.Add($"custom:{e.EventData!.Value.GetProperty("n").GetInt32()}");
                }

                Leave();
            };
            rig.Model.ScreenshotSaved += (_, e) =>
            {
                Enter();
                lock (order)
                {
                    order.Add($"screenshot:{e.SavedScreenshotPath}");
                }

                Leave();
            };
            rig.Model.Outputs.Record.FileChanged += (_, e) =>
            {
                Enter();
                lock (order)
                {
                    order.Add($"file:{e.NewOutputPath}");
                }

                Leave();
            };

            await rig.Responder.SendEventAsync("CustomEvent", new { n = 1 }, GeneralEvents);
            await rig.Responder.SendEventAsync("RecordFileChanged", new { newOutputPath = "b" }, 64);
            await rig.Responder.SendEventAsync("CustomEvent", new { n = 2 }, GeneralEvents);
            await rig.Responder.SendEventAsync("ScreenshotSaved", new { savedScreenshotPath = "s" }, GeneralEvents);
            await rig.Responder.SendEventAsync("CustomEvent", new { n = 3 }, GeneralEvents);
            await ModelRig.EventuallyAsync(() => Told(order) == 5, "the five events to be raised");

            Assert.Equal(new[] { "custom:1", "file:b", "custom:2", "screenshot:s", "custom:3" }, order);
            Assert.False(overlapped);
        }

        [Fact]
        public async Task AHandlerThatThrows_DoesNotKeepTheOthersFromRunning_AndIsLogged()
        {
            var logger = new ListLogger();
            await using var rig = await ModelRig.ConnectedAsync(loggerFactory: new SingleLoggerFactory(logger));
            var second = new List<int>();
            var after = new List<int>();
            rig.Model.CustomEvent += (_, _) => throw new InvalidOperationException("a handler that fails");
            rig.Model.CustomEvent += (_, e) =>
            {
                lock (second)
                {
                    second.Add(e.EventData!.Value.GetProperty("n").GetInt32());
                }
            };

            await rig.Responder.SendEventAsync("CustomEvent", new { n = 1 }, GeneralEvents);
            await rig.Responder.SendEventAsync("CustomEvent", new { n = 2 }, GeneralEvents);
            await ModelRig.EventuallyAsync(() => Told(second) == 2, "both custom events to reach the second handler");
            rig.Model.ScreenshotSaved += (_, _) =>
            {
                lock (after)
                {
                    after.Add(1);
                }
            };
            await rig.Responder.SendEventAsync("ScreenshotSaved", new { savedScreenshotPath = "s" }, GeneralEvents);
            await ModelRig.EventuallyAsync(() => Told(after) == 1, "the notifications to go on");

            Assert.Equal(new[] { 1, 2 }, second);
            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("CustomEvent", StringComparison.Ordinal));
        }

        [Fact]
        public async Task TheEventsThatSayWhatHappened_AreRaisedWhileTheModelIsStillBeingRead()
        {
            await using var rig = ModelRig.Create();
            var raised = new ManualResetEventSlim();
            var stateWhenRaised = (ObsModelState?)null;
            var heldTheRead = false;
            rig.Model.CustomEvent += (_, _) =>
            {
                stateWhenRaised = rig.Model.State;
                raised.Set();
            };
            var once = 0;
            rig.Responder.OnRequest = (type, data) =>
            {
                // The first read of the model is held back until the event that is sent now has been raised: the model is not complete, and the application is told all the same.
                if (Interlocked.Exchange(ref once, 1) == 0)
                {
                    rig.Responder.SendEventAsync("CustomEvent", new { n = 1 }, GeneralEvents).GetAwaiter().GetResult();
                    heldTheRead = raised.Wait(TimeSpan.FromSeconds(10));
                }

                return null;
            };

            await rig.ConnectAsync().WithTimeoutAsync(TimeSpan.FromSeconds(30));

            Assert.True(heldTheRead, "The event was not raised while the first read was held back.");
            Assert.Equal(ObsModelState.Synchronizing, stateWhenRaised);
        }

        [Fact]
        public async Task TheExitOfOBSStudio_MakesTheModelExiting_BeforeTheEventIsRaised_UntilItIsConnectedToOneThatWasStartedAgain()
        {
            await using var rig = await ModelRig.ConnectedAsync(options: new ObsModelOptions { AutoReconnect = true });
            Assert.False(rig.Model.IsExiting);
            var told = new List<string>();
            rig.Model.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ObsModel.IsExiting))
                {
                    lock (told)
                    {
                        told.Add($"IsExiting:{rig.Model.IsExiting}");
                    }
                }
            };
            rig.Model.ExitStarted += (_, _) =>
            {
                lock (told)
                {
                    told.Add($"ExitStarted:{rig.Model.IsExiting}");
                }
            };

            await rig.Responder.SendEventAsync("ExitStarted", new { }, GeneralEvents);
            await ModelRig.EventuallyAsync(() => Told(told) == 2, "the exit to be told");

            Assert.True(rig.Model.IsExiting);
            lock (told)
            {
                Assert.Equal(new[] { "IsExiting:True", "ExitStarted:True" }, told);
            }

            Assert.Contains(rig.Changes, change => change.Entity is null && change.PropertyName == "IsExiting" && Equals(change.OldValue, false) && Equals(change.NewValue, true));

            // The connection ends, as it does when OBS Studio is closed, and the model connects to the OBS Studio that is started again.
            var handshakes = rig.Responder.Handshakes;
            rig.Responder.AbortConnection();
            await ModelRig.EventuallyAsync(() => rig.Responder.Handshakes > handshakes && rig.Model.State == ObsModelState.Synchronized && !rig.Model.IsExiting, "the model to be connected again and not exiting");

            await ModelRig.EventuallyAsync(() => Told(told) == 3, "the end of the exit to be told");
            lock (told)
            {
                Assert.Equal("IsExiting:False", told[2]);
            }
        }

        [Fact]
        public async Task TheExitOfOBSStudio_IsToldWhileTheModelIsBeingRead_WithTheModelExitingBeforeTheEvent()
        {
            await using var rig = await ModelRig.ConnectedAsync();
            var told = new List<string>();
            rig.Model.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ObsModel.IsExiting))
                {
                    lock (told)
                    {
                        told.Add($"IsExiting:{rig.Model.IsExiting}");
                    }
                }
            };
            rig.Model.ExitStarted += (_, _) =>
            {
                lock (told)
                {
                    told.Add("ExitStarted");
                }
            };

            // OBS Studio is changing its scene collection when it is closed: the connection ends, and what is held until the model is complete would never be told.
            await rig.Responder.SendEventAsync("CurrentSceneCollectionChanging", new { sceneCollectionName = "Other" }, 2);
            await ModelRig.EventuallyAsync(() => rig.Model.State == ObsModelState.Synchronizing, "the model to be reading");
            await rig.Responder.SendEventAsync("ExitStarted", new { }, GeneralEvents);
            await ModelRig.EventuallyAsync(() => Told(told) == 2, "the exit to be told while the model is read");

            lock (told)
            {
                Assert.Equal(new[] { "IsExiting:True", "ExitStarted" }, told);
            }

            Assert.Equal(ObsModelState.Synchronizing, rig.Model.State);
        }

        [Fact]
        public async Task WhatTheConnectionSaysAboutOBSStudio_IsKnownAfterTheConnect_AndToldWhenItChanges()
        {
            await using var rig = ModelRig.Create();
            rig.Responder.SupportedImageFormats = ["png", "jpg"];
            rig.Responder.ObsVersion = "32.1.2";
            rig.Responder.WebSocketVersion = "5.6.3";
            Assert.Null(rig.Model.ObsVersion);
            Assert.Null(rig.Model.WebSocketVersion);
            Assert.Null(rig.Model.Platform);
            Assert.Empty(rig.Model.SupportedImageFormats);

            await rig.ConnectAsync().WithTimeoutAsync(TimeSpan.FromSeconds(15));

            Assert.Equal(new Version(32, 1, 2), rig.Model.ObsVersion);
            Assert.Equal(new Version(5, 6, 3), rig.Model.WebSocketVersion);
            Assert.Equal("windows", rig.Model.Platform);
            Assert.Equal(new[] { "png", "jpg" }, rig.Model.SupportedImageFormats);
            await ModelRig.EventuallyAsync(() => rig.Properties.Contains("SupportedImageFormats"), "the facts to be told");
            Assert.Equal(1, rig.Properties.Count(property => property == "ObsVersion"));
            Assert.Equal(1, rig.Properties.Count(property => property == "WebSocketVersion"));
            Assert.Equal(1, rig.Properties.Count(property => property == "Platform"));
            var record = Assert.Single(rig.Changes, change => change.PropertyName == "ObsVersion");
            Assert.Null(record.Entity);
            Assert.Null(record.OldValue);
            Assert.Equal(new Version(32, 1, 2), record.NewValue);
            Assert.Equal("the model: ObsVersion (nothing) -> 32.1.2", record.ToString());
        }

        [Fact]
        public async Task WhatTheConnectionSaysAboutOBSStudio_IsKept_WhenTheConnectionIsLost_AndReplacedByWhatTheNextOneSays()
        {
            await using var rig = await ModelRig.ConnectedAsync(options: new ObsModelOptions { AutoReconnect = true });
            Assert.Equal(new Version(33, 0, 0), rig.Model.ObsVersion);
            var handshakes = rig.Responder.Handshakes;

            rig.Responder.ObsVersion = "31.0.4";
            rig.Responder.SupportedImageFormats = ["png"];
            rig.Responder.AbortConnection();
            await ModelRig.EventuallyAsync(() => rig.Responder.Handshakes > handshakes && rig.Model.State == ObsModelState.Synchronized, "the model to be connected again");

            Assert.Equal(new Version(31, 0, 4), rig.Model.ObsVersion);
            Assert.Equal(new[] { "png" }, rig.Model.SupportedImageFormats);
            await ModelRig.EventuallyAsync(() => rig.Properties.Count(property => property == "ObsVersion") == 2, "the new version to be told");
            var records = rig.Changes.Where(change => change.PropertyName == "ObsVersion").ToArray();
            Assert.Equal(new Version(33, 0, 0), records[1].OldValue);
            Assert.Equal(new Version(31, 0, 4), records[1].NewValue);
            Assert.Equal(1, rig.Properties.Count(property => property == "WebSocketVersion"));
        }

        // A factory that hands out one logger, for the tests that look at what the model logs.
        private sealed class SingleLoggerFactory(ILogger logger) : ILoggerFactory
        {
            public void AddProvider(ILoggerProvider provider)
            {
            }

            public ILogger CreateLogger(string categoryName)
            {
                return logger;
            }

            public void Dispose()
            {
            }
        }
    }
}
