namespace OBSStudioClient.Enums
{
    public enum OpCode
    {
        Hello = 0,
        Identify = 1,
        Identified = 2,
        Reidentify = 3,
        Event = 5,
        Request = 6,
        RequestResponse = 7,
        RequestBatch = 8,
        RequestBatchResponse = 9,
    }
}
