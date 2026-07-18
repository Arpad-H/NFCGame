using System.Collections.Generic;
using UnityEngine;

namespace Riftborn.Tutorial
{
    // The inspector-authored teaching script: a reorderable list of TutorialStep
    // the director plays in order. Create one via Assets ▸ Create ▸ Riftborn ▸
    // Tutorial Sequence, or seed a fully-populated copy of the built-in sequence
    // with Riftborn ▸ Tutorial ▸ Create Sequence Asset From Code.
    //
    // The director loads it from a wired reference, else from a Resources folder
    // by name, and falls back to the built-in TutorialSequence.Build() when the
    // asset is missing or empty — so the tutorial always has content.
    [CreateAssetMenu(menuName = "Riftborn/Tutorial Sequence", fileName = "TutorialSequence")]
    public class TutorialSequenceAsset : ScriptableObject
    {
        [SerializeField] private List<TutorialStep> steps = new();

        public List<TutorialStep> Steps => steps;
    }
}
