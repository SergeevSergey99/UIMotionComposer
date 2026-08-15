using System;

namespace UIPanelSystem.Tweening
{
    /// <summary>
    /// Easing set used by the animation data.
    ///
    /// The numeric values intentionally mirror DG.Tweening.Ease one-to-one: assets authored while
    /// DOTween was installed store the raw int, so a project that later drops DOTween keeps every
    /// preset it already had. Do not renumber.
    /// </summary>
    public enum UIEase
    {
        Unset = 0,
        Linear = 1,
        InSine = 2,
        OutSine = 3,
        InOutSine = 4,
        InQuad = 5,
        OutQuad = 6,
        InOutQuad = 7,
        InCubic = 8,
        OutCubic = 9,
        InOutCubic = 10,
        InQuart = 11,
        OutQuart = 12,
        InOutQuart = 13,
        InQuint = 14,
        OutQuint = 15,
        InOutQuint = 16,
        InExpo = 17,
        OutExpo = 18,
        InOutExpo = 19,
        InCirc = 20,
        OutCirc = 21,
        InOutCirc = 22,
        InElastic = 23,
        OutElastic = 24,
        InOutElastic = 25,
        InBack = 26,
        OutBack = 27,
        InOutBack = 28,
        InBounce = 29,
        OutBounce = 30,
        InOutBounce = 31,
        Flash = 32,
        InFlash = 33,
        OutFlash = 34,
        InOutFlash = 35
    }

    /// <summary>
    /// Penner easing equations normalised to t in [0..1] -> value in [0..1] (values outside the
    /// range are expected for Back/Elastic/Bounce overshoot).
    ///
    /// Only used by the built-in tween backend; when DOTween is installed its own evaluator runs
    /// instead. The formulas are the same ones DOTween uses, with DOTween's default overshoot
    /// (1.70158) and period (0.3), so switching backends does not change how a preset looks.
    /// </summary>
    public static class UIEaseEvaluator
    {
        private const float TwoPi = 6.283185307179586f;
        private const float HalfPi = 1.5707963267948966f;
        private const float DefaultOvershoot = 1.70158f;
        private const float DefaultPeriod = 0.3f;

        public static float Evaluate(UIEase ease, float t)
        {
            switch (ease)
            {
                case UIEase.Unset:
                case UIEase.Linear:
                    return t;

                case UIEase.InSine: return 1f - (float)Math.Cos(t * HalfPi);
                case UIEase.OutSine: return (float)Math.Sin(t * HalfPi);
                case UIEase.InOutSine: return -0.5f * ((float)Math.Cos(Math.PI * t) - 1f);

                case UIEase.InQuad: return t * t;
                case UIEase.OutQuad: return -t * (t - 2f);
                case UIEase.InOutQuad:
                {
                    float s = t * 2f;
                    if (s < 1f) return 0.5f * s * s;
                    s -= 1f;
                    return -0.5f * (s * (s - 2f) - 1f);
                }

                case UIEase.InCubic: return t * t * t;
                case UIEase.OutCubic:
                {
                    float s = t - 1f;
                    return s * s * s + 1f;
                }
                case UIEase.InOutCubic:
                {
                    float s = t * 2f;
                    if (s < 1f) return 0.5f * s * s * s;
                    s -= 2f;
                    return 0.5f * (s * s * s + 2f);
                }

                case UIEase.InQuart: return t * t * t * t;
                case UIEase.OutQuart:
                {
                    float s = t - 1f;
                    return -(s * s * s * s - 1f);
                }
                case UIEase.InOutQuart:
                {
                    float s = t * 2f;
                    if (s < 1f) return 0.5f * s * s * s * s;
                    s -= 2f;
                    return -0.5f * (s * s * s * s - 2f);
                }

                case UIEase.InQuint: return t * t * t * t * t;
                case UIEase.OutQuint:
                {
                    float s = t - 1f;
                    return s * s * s * s * s + 1f;
                }
                case UIEase.InOutQuint:
                {
                    float s = t * 2f;
                    if (s < 1f) return 0.5f * s * s * s * s * s;
                    s -= 2f;
                    return 0.5f * (s * s * s * s * s + 2f);
                }

                case UIEase.InExpo: return t <= 0f ? 0f : (float)Math.Pow(2f, 10f * (t - 1f));
                case UIEase.OutExpo: return t >= 1f ? 1f : 1f - (float)Math.Pow(2f, -10f * t);
                case UIEase.InOutExpo:
                {
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    float s = t * 2f;
                    if (s < 1f) return 0.5f * (float)Math.Pow(2f, 10f * (s - 1f));
                    s -= 1f;
                    return 0.5f * (2f - (float)Math.Pow(2f, -10f * s));
                }

                case UIEase.InCirc: return -((float)Math.Sqrt(1f - t * t) - 1f);
                case UIEase.OutCirc:
                {
                    float s = t - 1f;
                    return (float)Math.Sqrt(1f - s * s);
                }
                case UIEase.InOutCirc:
                {
                    float s = t * 2f;
                    if (s < 1f) return -0.5f * ((float)Math.Sqrt(1f - s * s) - 1f);
                    s -= 2f;
                    return 0.5f * ((float)Math.Sqrt(1f - s * s) + 1f);
                }

                case UIEase.InElastic: return InElastic(t);
                case UIEase.OutElastic: return OutElastic(t);
                case UIEase.InOutElastic: return InOutElastic(t);

                case UIEase.InBack: return t * t * ((DefaultOvershoot + 1f) * t - DefaultOvershoot);
                case UIEase.OutBack:
                {
                    float s = t - 1f;
                    return s * s * ((DefaultOvershoot + 1f) * s + DefaultOvershoot) + 1f;
                }
                case UIEase.InOutBack:
                {
                    float overshoot = DefaultOvershoot * 1.525f;
                    float s = t * 2f;
                    if (s < 1f) return 0.5f * (s * s * ((overshoot + 1f) * s - overshoot));
                    s -= 2f;
                    return 0.5f * (s * s * ((overshoot + 1f) * s + overshoot) + 2f);
                }

                case UIEase.InBounce: return 1f - OutBounce(1f - t);
                case UIEase.OutBounce: return OutBounce(t);
                case UIEase.InOutBounce:
                    return t < 0.5f
                        ? (1f - OutBounce(1f - t * 2f)) * 0.5f
                        : OutBounce(t * 2f - 1f) * 0.5f + 0.5f;

                case UIEase.Flash: return Flash(t);
                case UIEase.InFlash: return Flash(t) * t;
                case UIEase.OutFlash:
                {
                    float inverse = 1f - t;
                    return Flash(t) * (1f - inverse * inverse);
                }
                case UIEase.InOutFlash:
                {
                    float weight = t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2f) * 0.5f;
                    return Flash(t) * weight;
                }

                default:
                    return t;
            }
        }

        private static float InElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            float s = ElasticPhaseShift();
            float shifted = t - 1f;
            return -((float)Math.Pow(2f, 10f * shifted) * DefaultOvershoot *
                     (float)Math.Sin((shifted - s) * TwoPi / DefaultPeriod));
        }

        private static float OutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            float s = ElasticPhaseShift();
            return DefaultOvershoot * (float)Math.Pow(2f, -10f * t) *
                   (float)Math.Sin((t - s) * TwoPi / DefaultPeriod) + 1f;
        }

        private static float InOutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            const float period = DefaultPeriod * 1.5f;
            float s = period / TwoPi * (float)Math.Asin(1f / DefaultOvershoot);
            float scaled = t * 2f;

            if (scaled < 1f)
            {
                scaled -= 1f;
                return -0.5f * (DefaultOvershoot * (float)Math.Pow(2f, 10f * scaled) *
                                (float)Math.Sin((scaled - s) * TwoPi / period));
            }

            scaled -= 1f;
            return DefaultOvershoot * (float)Math.Pow(2f, -10f * scaled) *
                (float)Math.Sin((scaled - s) * TwoPi / period) * 0.5f + 1f;
        }

        // DOTween's default amplitude (1.70158) is above |change| (1), so the phase shift takes the
        // asin branch rather than the period/4 shortcut.
        private static float ElasticPhaseShift()
        {
            return DefaultPeriod / TwoPi * (float)Math.Asin(1f / DefaultOvershoot);
        }

        private static float OutBounce(float t)
        {
            if (t < 1f / 2.75f)
                return 7.5625f * t * t;

            if (t < 2f / 2.75f)
            {
                t -= 1.5f / 2.75f;
                return 7.5625f * t * t + 0.75f;
            }

            if (t < 2.5f / 2.75f)
            {
                t -= 2.25f / 2.75f;
                return 7.5625f * t * t + 0.9375f;
            }

            t -= 2.625f / 2.75f;
            return 7.5625f * t * t + 0.984375f;
        }

        // Approximation of DOTween's Flash family with its default amplitude: a sawtooth that runs
        // to the end value and back once. Exact parity is not attempted -- when DOTween is present
        // its own Flash implementation is used instead.
        private static float Flash(float t)
        {
            const float amplitude = DefaultOvershoot;
            int stepIndex = (int)Math.Ceiling(t * amplitude);
            if (stepIndex < 1) stepIndex = 1;

            float stepDuration = 1f / amplitude;
            float local = t - stepDuration * (stepIndex - 1);
            float direction = stepIndex % 2 != 0 ? 1f : -1f;
            if (direction < 0f) local -= stepDuration;

            return local * direction / stepDuration;
        }
    }
}
