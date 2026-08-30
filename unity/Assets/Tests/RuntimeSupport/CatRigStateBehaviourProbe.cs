using UnityEngine;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class CatRigStateBehaviourProbe : StateMachineBehaviour
    {
        public static int StateEnterCount;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo,
            int layerIndex) => StateEnterCount++;
    }
}
