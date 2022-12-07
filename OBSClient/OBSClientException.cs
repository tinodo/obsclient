namespace OBSStudioClient
{
    [Serializable]
    public class ObsClientException : Exception
    {
        public ObsClientException(string message) : base(message) { }
    }
}
