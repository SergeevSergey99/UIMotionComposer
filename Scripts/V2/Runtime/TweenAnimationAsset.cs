using System.Collections.Generic;
using UnityEngine;

namespace UIMotionComposer.V2
{
    [CreateAssetMenu(fileName = "TweenAnimation", menuName = "UI Motion Composer V2/Tween Animation")]
    public sealed class TweenAnimationAsset : ScriptableObject
    {
        [SerializeReference]
        public List<BaseTweenClip> Clips = new List<BaseTweenClip>();
    }
}
