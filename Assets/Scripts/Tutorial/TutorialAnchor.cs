using UnityEngine;

namespace Riftborn.Tutorial
{
    // A named point a tutorial highlight can aim at. Drop an empty GameObject
    // anywhere in the scene (or add this to an existing object), give it an id,
    // and reference that id from a step's highlight (Kind = Anchor). Because the
    // director resolves the id to this transform at runtime, the highlight follows
    // the marker wherever you move or parent it — the "define the transform"
    // control for targets that aren't lane portals.
    //
    // Assets (the TutorialSequence) can't hold direct scene references, so the
    // string id is the bridge between the authored step and this scene object.
    public class TutorialAnchor : MonoBehaviour
    {
        [Tooltip("Referenced by a step's highlight (Kind = Anchor, matching Anchor Id). Case-insensitive; keep it unique.")]
        public string id;
    }
}
