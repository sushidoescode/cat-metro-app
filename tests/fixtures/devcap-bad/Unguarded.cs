// NEGATIVE FIXTURE (never compiled — lives outside unity/Assets). Proves criterion 4 fires on
// every rule: unwrapped file (rule 1), else-arm smuggling (rule 1, L3), and unguarded plus
// inverted-guard references (rule 2, L4).
public sealed class UnguardedShim
{
    public object Make()
    {
        return new DevFrameCapture(); // unguarded reference: forbidden outside the dev guard
    }
}
#if DEVELOPMENT_BUILD || UNITY_EDITOR
#else
public sealed class ShipsInRelease { } // an else-arm ships in release builds: forbidden
#endif
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
public sealed class InvertedShim
{
    public object Also() { return new DevFrameCapture(); } // inverted guard is NOT the dev guard
}
#endif
