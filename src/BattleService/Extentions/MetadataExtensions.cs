using Grpc.Core;

namespace BattleService.Extentions
{
    public static class MetadataExtensions
    {
        public static bool TryGetValue(this Metadata headers, string key, out string value)
        {
            var entry = headers.FirstOrDefault(h => h.Key == key);
            if (entry != null)
            {
                value = entry.Value;
                return true;
            }

            value = null;
            return false;
        }
    }
}
