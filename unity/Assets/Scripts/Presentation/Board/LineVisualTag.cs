using UnityEngine;

namespace CatMetro.Presentation.Board
{
    public enum LineVisualRole : byte
    {
        Station = 1,
        Commuter = 2,
    }

    public sealed class LineVisualTag : MonoBehaviour
    {
        public byte ColorCode;
        public string SymbolId;
        public string SilhouetteId;
        public Color LineColor;
        public LineVisualRole Role;

        public void Apply(LineIdentity identity, LineVisualRole role)
        {
            ColorCode = identity.ColorCode;
            SymbolId = identity.SymbolId;
            SilhouetteId = identity.SilhouetteId;
            LineColor = identity.Color;
            Role = role;
        }
    }

    public sealed class LineSymbolMesh : MonoBehaviour
    {
        public string SymbolId;
    }
}
