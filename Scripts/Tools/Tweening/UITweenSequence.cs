#if !UIPANEL_DOTWEEN
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UIPanelSystem.Tweening
{
    /// <summary>
    /// One interpolating float inside a <see cref="UITweenSequence"/>.
    /// </summary>
    internal sealed class UITweenStep : IUITweener
    {
        private readonly float _from;
        private readonly float _to;
        private readonly float _duration;
        private readonly Action<float> _onUpdate;

        private UIEase _ease = UIEase.Linear;
        private AnimationCurve _curve;
        private float _delay;

        public UITweenStep(float from, float to, float duration, Action<float> onUpdate)
        {
            _from = from;
            _to = to;
            _duration = Mathf.Max(0f, duration);
            _onUpdate = onUpdate;
        }

        public float Delay => _delay;
        public float Duration => _duration;
        public float EndTime => _delay + _duration;

        public IUITweener SetEase(UIEase ease)
        {
            _ease = ease;
            _curve = null;
            return this;
        }

        public IUITweener SetEase(AnimationCurve curve)
        {
            _curve = curve;
            return this;
        }

        public IUITweener SetDelay(float delay)
        {
            _delay = Mathf.Max(0f, delay);
            return this;
        }

        /// <summary>Applies the step at <paramref name="sequenceTime"/> seconds from the sequence start.</summary>
        public void Sample(float sequenceTime)
        {
            float local = sequenceTime - _delay;
            if (local < 0f)
                return;

            float normalized = _duration <= 0f ? 1f : Mathf.Clamp01(local / _duration);
            float eased = _curve != null
                ? _curve.Evaluate(normalized)
                : UIEaseEvaluator.Evaluate(_ease, normalized);

            _onUpdate(Mathf.LerpUnclamped(_from, _to, eased));
        }
    }

    /// <summary>
    /// Built-in replacement for DOTween's Sequence, used when DOTween is not installed.
    ///
    /// Every step is joined at the sequence's zero point and offset by its own delay, which is what
    /// the handlers rely on: a timeline of 0.5..1 becomes "delay half the duration, then run".
    /// A single coroutine on a shared runner drives all of them, so a sequence keeps playing even
    /// when the animated object is being disabled at the end of a hide.
    /// </summary>
    internal sealed class UITweenSequence : IUISequence
    {
        private readonly List<UITweenStep> _steps = new List<UITweenStep>();

        private Action _onComplete;
        private bool _useUnscaledTime;
        private bool _isActive = true;
        private bool _isPlaying;
        private Coroutine _routine;

        public IUISequence Join(IUITweener tweener)
        {
            if (tweener is UITweenStep step)
                _steps.Add(step);

            return this;
        }

        public IUISequence SetUpdate(bool isIndependentUpdate)
        {
            _useUnscaledTime = isIndependentUpdate;
            return this;
        }

        public IUISequence OnComplete(Action callback)
        {
            _onComplete = callback;
            return this;
        }

        public void Play()
        {
            if (!_isActive || _isPlaying)
                return;

            _isPlaying = true;

            float total = TotalDuration();
            if (total <= 0f)
            {
                SampleAll(0f);
                Finish();
                return;
            }

            _routine = UITweenRunner.Instance.Run(Run(total));
        }

        public void Kill()
        {
            if (!_isActive)
                return;

            _isActive = false;
            _isPlaying = false;

            if (_routine != null)
            {
                UITweenRunner.StopIfAlive(_routine);
                _routine = null;
            }
        }

        public bool IsActive() => _isActive;

        public bool IsPlaying() => _isPlaying;

        private float TotalDuration()
        {
            float total = 0f;
            for (int i = 0; i < _steps.Count; i++)
            {
                float end = _steps[i].EndTime;
                if (end > total)
                    total = end;
            }

            return total;
        }

        private IEnumerator Run(float total)
        {
            float elapsed = 0f;

            // Frame zero so the start values are on screen before the first delta is applied.
            SampleAll(0f);
            yield return null;

            while (elapsed < total)
            {
                if (!_isActive)
                    yield break;

                elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                SampleAll(Mathf.Min(elapsed, total));
                yield return null;
            }

            if (!_isActive)
                yield break;

            SampleAll(total);
            Finish();
        }

        private void SampleAll(float sequenceTime)
        {
            for (int i = 0; i < _steps.Count; i++)
                _steps[i].Sample(sequenceTime);
        }

        private void Finish()
        {
            _isPlaying = false;
            _isActive = false;
            _routine = null;

            Action callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }
    }

    /// <summary>
    /// Shared coroutine host for <see cref="UITweenSequence"/>. Lives on a hidden, persistent object
    /// so sequences survive the panel object being deactivated mid-animation.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class UITweenRunner : MonoBehaviour
    {
        private static UITweenRunner _instance;
        private static bool _isQuitting;

        public static UITweenRunner Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                var host = new GameObject("[UIPanel Tween Runner]")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                _instance = host.AddComponent<UITweenRunner>();
                DontDestroyOnLoad(host);
                return _instance;
            }
        }

        public Coroutine Run(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }

        public static void StopIfAlive(Coroutine routine)
        {
            if (_isQuitting || _instance == null || routine == null)
                return;

            _instance.StopCoroutine(routine);
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
#endif
