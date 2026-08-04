using Newtonsoft.Json.Linq;
namespace CatMetro.Application.Save
{
    public static class SaveDefaults
    {
        public const ushort FORMAT_VERSION = 1;
        public const ushort SAVE_VERSION = 1;
        public const string MAGIC = "CMSV";
        public static JObject FreshPayload() => throw new System.NotImplementedException();
    }
}
