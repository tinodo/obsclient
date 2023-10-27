namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Interfaces;
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
            this.D = d;
            this.Op = op;
        }

        internal ObsMessage(IMessage data, OpCode op)
        {
            this.Data = data;
            this.Op = op;
        }

        public ObsMessage(IdentifyMessage identifyMessage)
        {
            this.Data = identifyMessage ?? throw new ArgumentNullException(nameof(identifyMessage));
            this.Op = OpCode.Identify;
        }

        public ObsMessage(ReidentifyMessage reidentifyMessage)
        {
            this.Data = reidentifyMessage ?? throw new ArgumentNullException(nameof(reidentifyMessage));
            this.Op = OpCode.Reidentify;
        }

        public ObsMessage(RequestMessage requestMessage)
        {
            this.Data = requestMessage ?? throw new ArgumentNullException(nameof(requestMessage));
            this.Op = OpCode.Request;
        }

        public ObsMessage(RequestBatchMessage requestBatchMessage)
        {
            this.Data = requestBatchMessage ?? throw new ArgumentNullException(nameof(requestBatchMessage));
            this.Op = OpCode.RequestBatch;
        }

        public void OnDeserialized()
        {
            this.Data = this.Op switch
            {
                OpCode.Hello => this.D.Deserialize<HelloMessage>(),
                OpCode.Identified => this.D.Deserialize<IdentifiedMessage>(),
                OpCode.Event => this.D.Deserialize<EventMessage>(),
                OpCode.RequestResponse => this.D.Deserialize<RequestResponseMessage>(),
                OpCode.RequestBatchResponse => this.D.Deserialize<RequestBatchResponseMessage>(),
                _ => throw new ObsClientException($"The OpCode {this.Op} is unexpected."),
            };
        }

        public void OnSerializing()
        {
            if (this.Data != null)
            {
                this.D = JsonSerializer.SerializeToElement(this.Data, this.Data.GetType());
            }
        }
    }
}
