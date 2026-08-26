// NEGATIVE FIXTURE (never compiled — lives outside unity/Assets). Proves two criterion gates
// fire: a dev-guard token in a shipped policy file, and an unbound runtime primitive.
public static class GuardedPolicy
{
#if UNITY_EDITOR
    public static void Apply() { }
#endif
    public static object Unbound()
    {
        var primitive = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
        primitive.GetComponent<UnityEngine.Renderer>().sharedMaterial = null;
        return primitive;
    }
}
