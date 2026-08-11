namespace CatMetro.Content.Daily
{
    public interface IDailySeedScheme
    {
        string ArtifactLabel { get; }
        uint Derive(string dateKey, int k);
    }
}
