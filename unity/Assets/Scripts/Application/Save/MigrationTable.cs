using Newtonsoft.Json.Linq;
namespace CatMetro.Application.Save
{
    public sealed class MigrationTable
    {
        public MigrationTable Register(int from, int to, System.Func<JObject, JObject> apply) => throw new System.NotImplementedException();
        public JObject Migrate(JObject payload, int fileVersion, int targetVersion) => throw new System.NotImplementedException();
    }
}
