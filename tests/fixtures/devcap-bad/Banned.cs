// NEGATIVE FIXTURE (never compiled — lives outside unity/Assets). Proves two guards fire:
// criterion 1's clock grep (the tokens below) and criterion 4's rule 1 (this file is unwrapped).
public static class Banned
{
    public static double Now()
    {
        return UnityEngine.Time.realtimeSinceStartupAsDouble; // a second clock: forbidden
    }
}
