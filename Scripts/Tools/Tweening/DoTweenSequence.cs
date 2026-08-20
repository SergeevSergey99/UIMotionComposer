#if UIMOTION_DOTWEEN
using System;
using DG.Tweening;
using UnityEngine;

namespace UIMotionComposer.Tweening
{
    /// <summary>
    /// DOTween backed tween. Active only when UIMOTION_DOTWEEN is defined, which
    /// DefineSymbols sets as soon as DOTween is present in the project.
    /// </summary>
    internal sealed class DoTweenTweener : IUITweener
    {
        private readonly Tweener _tweener;

        public DoTweenTweener(float from, float to, float duration, Action<float> onUpdate)
        {
            _tweener = DOVirtual.Float(from, to, Mathf.Max(0f, duration), value => onUpdate(value));
        }

        public Tweener Tweener => _tweener;

        public IUITweener SetEase(UIEase ease)
        {
            _tweener.SetEase(ease.ToDoTweenEase());
            return this;
        }

        public IUITweener SetEase(AnimationCurve curve)
        {
            if (curve != null)
                _tweener.SetEase(curve);

            return this;
        }

        public IUITweener SetDelay(float delay)
        {
            _tweener.SetDelay(Mathf.Max(0f, delay));
            return this;
        }
    }

    /// <summary>
    /// DOTween backed sequence. Joining keeps every tween anchored at the sequence start, with the
    /// per tween delay deciding when it actually begins -- the same shape the built-in engine uses.
    /// </summary>
    internal sealed class DoTweenSequence : IUISequence
    {
        private readonly Sequence _sequence = DOTween.Sequence();

        public IUISequence Join(IUITweener tweener)
        {
            if (tweener is DoTweenTweener doTweenTweener)
                _sequence.Join(doTweenTweener.Tweener);

            return this;
        }

        public IUISequence SetUpdate(bool isIndependentUpdate)
        {
            _sequence.SetUpdate(isIndependentUpdate);
            return this;
        }

        public IUISequence OnComplete(Action callback)
        {
            _sequence.OnComplete(() => callback?.Invoke());
            return this;
        }

        public void Play() => _sequence.Play();

        public void Kill() => _sequence.Kill();

        public bool IsActive() => _sequence.IsActive();

        public bool IsPlaying() => _sequence.IsPlaying();
    }

    public static class UIEaseDoTweenExtensions
    {
        /// <summary>
        /// UIEase mirrors DG.Tweening.Ease numerically, so the conversion is a cast. Unset is the
        /// one exception: DOTween treats it as "no ease chosen", which is not what a preset means.
        /// </summary>
        public static Ease ToDoTweenEase(this UIEase ease)
        {
            return ease == UIEase.Unset ? Ease.Linear : (Ease)(int)ease;
        }

        public static UIEase ToUIEase(this Ease ease)
        {
            int value = (int)ease;
            return Enum.IsDefined(typeof(UIEase), value) ? (UIEase)value : UIEase.Linear;
        }
    }
}
#endif
