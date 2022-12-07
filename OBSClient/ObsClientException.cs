using System.Runtime.Serialization;

namespace OBSStudioClient
{
    [Serializable]
    public class ObsClientException : Exception
    {
        public ObsClientException() { }

        public ObsClientException(string message) : base(message) { }

        private ObsClientException(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }
    }
}
