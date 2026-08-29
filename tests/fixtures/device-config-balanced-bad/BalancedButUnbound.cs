// NEGATIVE FIXTURE (never compiled). Global counts are deliberately balanced: two renderer
// creations and two assignments. One renderer is nevertheless unbound, proving a total-count
// gate can be canceled out by binding another renderer twice.
public static class BalancedButUnbound
{
    public static void Build()
    {
        var bound = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
        bound.GetComponent<UnityEngine.Renderer>().sharedMaterial = GreyboxMaterial.Shared;
        bound.GetComponent<UnityEngine.Renderer>().sharedMaterial = GreyboxMaterial.Shared;

        var unbound = UnityEngine.GameObject.CreatePrimitive(
            UnityEngine.PrimitiveType.Cylinder);
        unbound.name = "still-unbound";
    }
}
