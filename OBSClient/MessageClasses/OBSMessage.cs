namespace OBSStudioClient.MessageClasses
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using OBSStudioClient.Messages;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal class ObsMessage : IJsonOnDeserialized, IJsonOnSerializing
    {
        [JsonPropertyName("d")]
        public JsonElement D { get; private set; }

        [JsonPropertyName("op")]
        public OpCode Op { get; }

        [JsonIgnore]
        public IMessage? Data { get; private set; }

        [JsonConstructor]
        public ObsMessage(JsonElement d, OpCode op)
        {
            D = d;
            Op = op;
        }

        internal ObsMessage(IMessage data, OpCode op)
        {
            Data = data;
            Op = op;
        }

        public ObsMessage(IdentifyMessage identifyMessage)
        {
            Data = identifyMessage ?? throw new ArgumentNullException(nameof(identifyMessage));
            Op = OpCode.Identify;
        }

        public ObsMessage(ReidentifyMessage reidentifyMessage)
        {
            Data = reidentifyMessage ?? throw new ArgumentNullException(nameof(reidentifyMessage));
            Op = OpCode.Reidentify;
        }

        public ObsMessage(RequestMessage requestMessage)
        {
            Data = requestMessage ?? throw new ArgumentNullException(nameof(requestMessage));
            Op = OpCode.Request;
        }

        public ObsMessage(RequestBatchMessage requestBatchMessage)
        {
            Data = requestBatchMessage ?? throw new ArgumentNullException(nameof(requestBatchMessage));
            Op = OpCode.RequestBatch;
        }

        public void OnDeserialized()
        {
            Data = Op switch
            {
                OpCode.Hello => D.Deserialize<HelloMessage>(),
                OpCode.Identified => D.Deserialize<IdentifiedMessage>(),
                OpCode.Event => D.Deserialize<EventMessage>(),
                OpCode.RequestResponse => D.Deserialize<RequestResponseMessage>(),
                OpCode.RequestBatchResponse => D.Deserialize<RequestBatchResponseMessage>(),
                _ => throw new ObsClientException($"The OpCode {Op} is unexpected."),
            };
        }

        public void OnSerializing()
        {
            if (Data != null)
            {
                D = JsonSerializer.SerializeToElement(Data, Data.GetType());
            }
        }
    }
}
