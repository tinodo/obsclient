namespace OBSStudioClient.Classes
{
    internal static class AudioMath
    {
        // Evita -Infinity quando mul = 0
        public static float MulToDb(float mul, float floorDb = -100f)
        {
            if (mul <= 0f)
                return floorDb;

            float db = 20f * MathF.Log10(mul);
            return MathF.Max(db, floorDb);
        }

        public static float DbToMul(float db)
        {
            return MathF.Pow(10f, db / 20f);
        }
    }
}