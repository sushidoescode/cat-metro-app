// NEGATIVE FIXTURE (never compiled — lives outside unity/Assets). Proves criterion 4 fires on
// both rules: this file is unwrapped (rule 1) and holds an unguarded capture reference (rule 2).
public sealed class UnguardedShim
{
    public object Make()
    {
        return new DevFrameCapture(); // unguarded reference: forbidden outside the dev guard
    }
}
