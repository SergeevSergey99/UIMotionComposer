using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UIMotionComposer.V2.Editor
{
    /// <summary>Creates the reusable V2 preset assets used by the showcase and available to users.</summary>
    public static class TweenV2PresetLibrary
    {
        public const string Folder = "Assets/Plugins/UIMotionComposer/ScriptableObjects/V2";

        public const string PanelSlideShow = "Panel_SlideFade_Show";
        public const string PanelPopShow = "Panel_Pop_Show";
        public const string PanelAlertShow = "Panel_Alert_Show";
        public const string PanelHide = "Panel_Soft_Hide";
        public const string ButtonSoftHover = "Button_Soft_Hover";
        public const string ButtonSoftReturn = "Button_Soft_Return";
        public const string ButtonSoftPress = "Button_Soft_Press";
        public const string ButtonOrbitHover = "Button_Orbit_Hover";
        public const string ButtonWaveHover = "Button_Wave_Hover";
        public const string ButtonSpectrumHover = "Button_Spectrum_Hover";
        public const string ButtonReturn = "Button_Return";
        public const string ButtonPress = "Button_Press";
        public const string ButtonDisabled = "Button_Disabled";
        public const string ButtonInteractable = "Button_Interactable";

        public static readonly string[] AllPresetNames =
        {
            PanelSlideShow, PanelPopShow, PanelAlertShow, PanelHide,
            ButtonSoftHover, ButtonSoftReturn, ButtonSoftPress,
            ButtonOrbitHover, ButtonWaveHover, ButtonSpectrumHover,
            ButtonReturn, ButtonPress, ButtonDisabled, ButtonInteractable
        };

        private static readonly Dictionary<string, TweenAnimationAsset> BuiltAssets =
            new Dictionary<string, TweenAnimationAsset>(StringComparer.Ordinal);

        [MenuItem("Tools/UI Motion Composer V2/Rebuild V2 preset library")]
        public static void Build()
        {
            BuiltAssets.Clear();
            EnsureFolder("Assets/Plugins/UIMotionComposer", "ScriptableObjects");
            EnsureFolder("Assets/Plugins/UIMotionComposer/ScriptableObjects", "V2");

            Upsert(PanelSlideShow,
                new AnchorPositionTweenClip
                {
                    Label = "Slide from left",
                    FromMode = TweenEndpointMode.OffsetFromInitial,
                    FromOffset = new Vector2(-520f, 0f),
                    ToMode = TweenEndpointMode.Initial,
                    Delay = 0.04f,
                    Duration = 0.72f,
                    Ease = UIMotionComposer.Tweening.UIEase.OutBack
                },
                Fade(0f, 1f, 0.04f, 0.4f));

            Upsert(PanelPopShow,
                new ScaleTweenClip
                {
                    Label = "Pop scale",
                    FromMode = TweenEndpointMode.Custom,
                    FromValue = new Vector3(0.55f, 0.55f, 1f),
                    ToMode = TweenEndpointMode.Initial,
                    Delay = 0.03f,
                    Duration = 0.56f,
                    Ease = UIMotionComposer.Tweening.UIEase.OutBack
                },
                Fade(0f, 1f, 0.03f, 0.3f),
                new PunchScaleTweenClip
                {
                    Label = "Settle",
                    Delay = 0.58f,
                    Duration = 0.36f,
                    Strength = new Vector3(0.045f, 0.045f, 0f),
                    Vibrato = 5,
                    Elasticity = 0.72f
                });

            Upsert(PanelAlertShow,
                new AnchorPositionTweenClip
                {
                    Label = "Drop in",
                    FromMode = TweenEndpointMode.OffsetFromInitial,
                    FromOffset = new Vector2(0f, 260f),
                    ToMode = TweenEndpointMode.Initial,
                    Delay = 0.04f,
                    Duration = 0.62f,
                    Ease = UIMotionComposer.Tweening.UIEase.OutBounce
                },
                Fade(0f, 1f, 0.04f, 0.32f),
                new ShakeTweenClip
                {
                    Label = "Attention shake",
                    Delay = 0.67f,
                    Duration = 0.5f,
                    Strength = new Vector3(11f, 4f, 0f),
                    Vibrato = 11,
                    Randomness = 55f,
                    Seed = 371488
                });

            Upsert(PanelHide,
                Fade(1f, 0f, 0f, 0.24f),
                new ScaleTweenClip
                {
                    Label = "Soft shrink",
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.OffsetFromInitial,
                    ToOffset = new Vector3(-0.045f, -0.045f, 0f),
                    Duration = 0.28f,
                    Ease = UIMotionComposer.Tweening.UIEase.InQuad
                });

            Upsert(ButtonSoftHover,
                new ScaleTweenClip
                {
                    Label = "Hover grow",
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.OffsetFromInitial,
                    ToOffset = new Vector3(0.045f, 0.045f, 0f),
                    Duration = 0.14f,
                    Ease = UIMotionComposer.Tweening.UIEase.OutQuad
                },
                new ColorTweenClip
                {
                    Label = "Hover brighten",
                    TargetKey = "Glow",
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.OffsetFromInitial,
                    ToOffset = new Color(0.12f, 0.12f, 0.12f, 0f),
                    Duration = 0.16f,
                    Ease = UIMotionComposer.Tweening.UIEase.OutQuad
                });

            Upsert(ButtonSoftReturn,
                ReturnScale(string.Empty, "Hover return"),
                new ColorTweenClip
                {
                    Label = "Color return",
                    TargetKey = "Glow",
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.Initial,
                    Duration = 0.16f,
                    Ease = UIMotionComposer.Tweening.UIEase.OutQuad
                });

            Upsert(ButtonSoftPress,
                new PunchScaleTweenClip
                {
                    Label = "Click punch",
                    Duration = 0.22f,
                    Strength = new Vector3(-0.075f, -0.075f, 0f),
                    Vibrato = 4,
                    Elasticity = 0.58f
                });

            Upsert(ButtonOrbitHover,
                Spin("Ring", 360f, 1.2f),
                new PunchScaleTweenClip
                {
                    Label = "Pulsing label",
                    TargetKey = "Label",
                    Duration = 1.2f,
                    Strength = new Vector3(0.13f, 0.13f, 0f),
                    Vibrato = 4,
                    Elasticity = 0.85f
                },
                new PunchAnchorPositionTweenClip
                {
                    Label = "Orbiting spark",
                    TargetKey = "Spark",
                    Duration = 1.2f,
                    Strength = new Vector2(15f, 0f),
                    Vibrato = 4,
                    Elasticity = 0.9f
                });

            Upsert(ButtonWaveHover,
                Spin("Ring", -180f, 0.9f),
                new JumpAnchorPositionTweenClip
                {
                    Label = "Bouncing spark",
                    TargetKey = "Spark",
                    FromMode = TweenEndpointMode.Initial,
                    ToMode = TweenEndpointMode.Initial,
                    Duration = 0.9f,
                    JumpPower = 13f,
                    Jumps = 2,
                    Ease = UIMotionComposer.Tweening.UIEase.Linear
                },
                new PunchScaleTweenClip
                {
                    Label = "Breathing label",
                    TargetKey = "Label",
                    Duration = 0.9f,
                    Strength = new Vector3(0.09f, 0.16f, 0f),
                    Vibrato = 2,
                    Elasticity = 1f
                });

            Upsert(ButtonSpectrumHover,
                Spin("Ring", 360f, 1.45f),
                new ColorTweenClip
                {
                    Label = "Spectrum glow",
                    TargetKey = "Glow",
                    FromMode = TweenEndpointMode.Initial,
                    ToMode = TweenEndpointMode.Custom,
                    ToValue = new Color(0.25f, 1f, 0.78f, 1f),
                    Duration = 1.45f,
                    Ease = UIMotionComposer.Tweening.UIEase.InOutSine
                },
                new PunchScaleTweenClip
                {
                    Label = "Spark pulse",
                    TargetKey = "Spark",
                    Duration = 1.45f,
                    Strength = new Vector3(0.32f, 0.32f, 0f),
                    Vibrato = 5,
                    Elasticity = 0.75f
                });

            Upsert(ButtonReturn,
                ReturnScale(string.Empty, "Root scale"),
                ReturnScale("Label", "Label scale"),
                ReturnScale("Spark", "Spark scale"),
                new RotateTweenClip
                {
                    Label = "Ring rotation",
                    TargetKey = "Ring",
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.Initial,
                    Duration = 0.18f,
                    Ease = UIMotionComposer.Tweening.UIEase.OutQuad
                },
                new AnchorPositionTweenClip
                {
                    Label = "Spark position",
                    TargetKey = "Spark",
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.Initial,
                    Duration = 0.18f,
                    Ease = UIMotionComposer.Tweening.UIEase.OutQuad
                },
                new ColorTweenClip
                {
                    Label = "Glow color",
                    TargetKey = "Glow",
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.Initial,
                    Duration = 0.2f,
                    Ease = UIMotionComposer.Tweening.UIEase.OutQuad
                });

            Upsert(ButtonPress,
                new PunchScaleTweenClip
                {
                    Label = "Press impact",
                    Duration = 0.24f,
                    Strength = new Vector3(-0.09f, -0.09f, 0f),
                    Vibrato = 4,
                    Elasticity = 0.62f
                },
                new PunchScaleTweenClip
                {
                    Label = "Spark impact",
                    TargetKey = "Spark",
                    Duration = 0.28f,
                    Strength = new Vector3(0.35f, 0.35f, 0f),
                    Vibrato = 5,
                    Elasticity = 0.72f
                });

            Upsert(ButtonDisabled,
                new FadeTweenClip
                {
                    Label = "Disabled fade",
                    FadeTarget = TweenFadeTarget.CanvasGroup,
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.Custom,
                    ToValue = 0.42f,
                    Duration = 0.22f,
                    Ease = UIMotionComposer.Tweening.UIEase.OutQuad
                },
                new ScaleTweenClip
                {
                    Label = "Disabled scale",
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.OffsetFromInitial,
                    ToOffset = new Vector3(-0.035f, -0.035f, 0f),
                    Duration = 0.22f,
                    Ease = UIMotionComposer.Tweening.UIEase.OutQuad
                });

            Upsert(ButtonInteractable,
                new FadeTweenClip
                {
                    Label = "Restore alpha",
                    FadeTarget = TweenFadeTarget.CanvasGroup,
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.Initial,
                    Duration = 0.2f,
                    Ease = UIMotionComposer.Tweening.UIEase.OutQuad
                },
                ReturnScale(string.Empty, "Restore scale"));

            AssetDatabase.SaveAssets();
            Debug.Log($"[UI Motion Composer] Rebuilt {AllPresetNames.Length} reusable V2 preset assets in {Folder}.");
        }

        public static TweenAnimationAsset Load(string name)
        {
            if (BuiltAssets.TryGetValue(name, out TweenAnimationAsset built) && built != null)
                return built;
            return AssetDatabase.LoadAssetAtPath<TweenAnimationAsset>($"{Folder}/{name}.asset");
        }

        private static void Upsert(string name, params BaseTweenClip[] clips)
        {
            string path = $"{Folder}/{name}.asset";
            TweenAnimationAsset asset = AssetDatabase.LoadAssetAtPath<TweenAnimationAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TweenAnimationAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Clips = new List<BaseTweenClip>(clips);
            EditorUtility.SetDirty(asset);
            BuiltAssets[name] = asset;
        }

        private static FadeTweenClip Fade(float from, float to, float delay, float duration)
        {
            return new FadeTweenClip
            {
                Label = "Panel fade",
                FadeTarget = TweenFadeTarget.CanvasGroup,
                FromMode = TweenEndpointMode.Custom,
                FromValue = from,
                ToMode = TweenEndpointMode.Custom,
                ToValue = to,
                Delay = delay,
                Duration = duration,
                Ease = UIMotionComposer.Tweening.UIEase.OutQuad
            };
        }

        private static RotateTweenClip Spin(string targetKey, float degrees, float duration)
        {
            return new RotateTweenClip
            {
                Label = "Infinite ring spin",
                TargetKey = targetKey,
                FromMode = TweenEndpointMode.Initial,
                ToMode = TweenEndpointMode.OffsetFromInitial,
                ToOffset = new Vector3(0f, 0f, degrees),
                Duration = duration,
                Ease = UIMotionComposer.Tweening.UIEase.Linear
            };
        }

        private static ScaleTweenClip ReturnScale(string targetKey, string label)
        {
            return new ScaleTweenClip
            {
                Label = label,
                TargetKey = targetKey,
                FromMode = TweenEndpointMode.Current,
                ToMode = TweenEndpointMode.Initial,
                Duration = 0.18f,
                Ease = UIMotionComposer.Tweening.UIEase.OutQuad
            };
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
