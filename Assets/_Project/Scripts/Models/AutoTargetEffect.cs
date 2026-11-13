using SerializeReferenceEditor;
using UnityEngine;

namespace PlushieOdyssey.Combat.Models
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "AutoTargetEffect")]
    [System.Serializable]
    public class AutoTargetEffect
    {
        [field: SerializeReference, SR] public TargetMode TargetMode { get; private set; }
        [field: SerializeReference, SR] public Effect Effect { get; private set; }
    }
}