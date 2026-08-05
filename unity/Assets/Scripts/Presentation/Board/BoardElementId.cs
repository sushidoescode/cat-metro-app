using UnityEngine;

namespace CatMetro.Presentation.Board
{
    // CM-C2b criterion 1: every greybox view object carries its authored id + kind so the
    // enumeration test can compare the scene against the DTO, id for id.
    public sealed class BoardElementId : MonoBehaviour
    {
        public string Id;
        public string Kind; // node | source | station | edge | switch | train
    }
}
