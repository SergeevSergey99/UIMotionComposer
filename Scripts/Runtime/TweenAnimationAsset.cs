using System.Collections.Generic;
using UnityEngine;

namespace UIMotionComposer
{
    [CreateAssetMenu(fileName = "TweenAnimation", menuName = "UI Motion Composer/Tween Animation")]
    public sealed class TweenAnimationAsset : ScriptableObject
    {
        [SerializeReference]
        public List<BaseTweenClip> Clips = new List<BaseTweenClip>();
    }
}
