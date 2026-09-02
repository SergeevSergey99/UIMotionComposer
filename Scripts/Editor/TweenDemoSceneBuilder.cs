using System.Collections.Generic;
using System.Linq;
using UIMotionComposer.Tweening;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UIMotionComposer.Editor
{
    public static class TweenDemoSceneBuilder
    {
        public const string ScenePath = "Assets/Plugins/UIMotionComposer/Examples/Showcase/UIMotionComposerShowcase.unity";

        private static readonly Color Background = Hex("111624");
        private static readonly Color Surface = Hex("20283A");
        private static readonly Color TextPrimary = Hex("F5F7FF");
        private static readonly Color TextSecondary = Hex("AAB4CD");
        private static readonly Color Blue = Hex("5B8CFF");
        private static readonly Color Purple = Hex("AE72FF");
        private static readonly Color Coral = Hex("FF6B7A");
        private static readonly Color Green = Hex("46D6A0");
        private static Font _font;
        private static Sprite _uiSprite;

        [MenuItem("Tools/UI Motion Composer/Rebuild showcase scene")]
        public static void Build()
        {
            EnsureFolders();
            TweenPresetLibrary.Build();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "UIMotionComposerShowcase";

            GameObject canvasObject = new GameObject("Showcase Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image background = CreateImage("Background", canvasObject.transform, Background);
            Stretch(background.rectTransform);

            Text title = CreateText("Title", canvasObject.transform, "UI MOTION COMPOSER  •  SHOWCASE",
                38, TextPrimary, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1500f, 54f), FontStyle.Bold);
            SetTopStretch(title.rectTransform, 120f, 120f, 24f, 54f);
            Text subtitle = CreateText("Subtitle", canvasObject.transform,
                "Panels, reusable SO presets and stateful buttons. Hover the lower buttons to start infinite child motion.",
                20, TextSecondary, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1650f, 38f));
            SetTopStretch(subtitle.rectTransform, 100f, 100f, 76f, 38f);

            RectTransform scrollRoot = CreateRect("Showcase Scroll", canvasObject.transform, Vector2.zero, Vector2.zero);
            SetStretch(scrollRoot, 46f, 46f, 28f, 134f);
            scrollRoot.gameObject.AddComponent<CanvasRenderer>();
            Image scrollSurface = scrollRoot.gameObject.AddComponent<Image>();
            scrollSurface.color = new Color(0f, 0f, 0f, 0.001f);
            scrollSurface.raycastTarget = true;
            ScrollRect scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 45f;

            RectTransform viewport = CreateRect("Viewport", scrollRoot, Vector2.zero, Vector2.zero);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateRect("Responsive Content", viewport, Vector2.zero, Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(12, 12, 12, 24);
            contentLayout.spacing = 34f;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = viewport;
            scrollRect.content = content;

            RectTransform topRow = CreateGridRow(content, "Panel Row 1");
            CreateSlidePanel(topRow, Vector2.zero);
            CreatePopPanel(topRow, Vector2.zero);
            CreateAlertPanel(topRow, Vector2.zero);

            RectTransform lowerRow = CreateGridRow(content, "Panel Row 2");
            CreateButtonLabPanel(lowerRow, Vector2.zero);
            CreatePresetGalleryPanel(lowerRow, Vector2.zero);
            CreateUtilityPanel(lowerRow, Vector2.zero);
            CreateHudPanel(content, Vector2.zero);

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.transform.SetAsLastSibling();

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            foreach (TweenPlayer player in canvasObject.GetComponentsInChildren<TweenPlayer>(true))
            {
                player.CaptureInitialValues();
                EditorUtility.SetDirty(player);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.InvalidOperationException("Could not save showcase scene at " + ScenePath);

            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("[UI Motion Composer] Built showcase scene: " + ScenePath);
        }

        public static void BuildFromCli()
        {
            Build();
        }

        public static void BuildAndValidateFromCli()
        {
            Build();
            ValidateShowcase();
            TweenValidation.Run();
        }

        [MenuItem("Tools/UI Motion Composer/Validate showcase scene")]
        public static void ValidateShowcase()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject[] all = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(item => item.gameObject)
                .ToArray();

            int missingScripts = all.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
            Require(missingScripts == 0, $"Scene contains {missingScripts} missing script reference(s).");

            TweenPlayer[] players = all.SelectMany(item => item.GetComponents<TweenPlayer>()).ToArray();
            TweenUIPanel[] panels = all.SelectMany(item => item.GetComponents<TweenUIPanel>()).ToArray();
            TweenUIClickable[] clickables = all.SelectMany(item => item.GetComponents<TweenUIClickable>()).ToArray();
            Button[] buttons = all.SelectMany(item => item.GetComponents<Button>()).ToArray();
            CanvasScaler canvasScaler = all.SelectMany(item => item.GetComponents<CanvasScaler>()).SingleOrDefault();
            Require(canvasScaler != null && canvasScaler.referenceResolution == new Vector2(1920f, 1080f),
                "Showcase Canvas must use the normal 1920x1080 reference resolution.");
            ScrollRect showcaseScroll = all.SelectMany(item => item.GetComponents<ScrollRect>())
                .FirstOrDefault(item => item.name == "Showcase Scroll");
            Require(showcaseScroll?.content != null && showcaseScroll.vertical && !showcaseScroll.horizontal,
                "Showcase must use a vertical responsive ScrollRect.");
            Require(showcaseScroll.content.GetComponent<VerticalLayoutGroup>() != null &&
                    showcaseScroll.content.GetComponent<ContentSizeFitter>() != null,
                "Showcase content is missing its responsive vertical layout.");
            Require(all.Count(item => item.GetComponent<HorizontalLayoutGroup>() != null &&
                                      item.name.StartsWith("Panel Row")) == 2,
                "Showcase must contain two responsive panel rows.");
            Require(players.Length == 17, $"Expected 17 TweenPlayers (7 panels + 10 buttons), got {players.Length}.");
            Require(players.All(player => player.HasCapturedInitialValues && player.CapturedInitialValueCount > 0),
                "Every TweenPlayer must have a serialized Initial Values snapshot.");
            Require(panels.Length == 7, $"Expected 7 TweenUIPanel wrappers, got {panels.Length}.");
            Require(clickables.Length == 10, $"Expected 10 stateful TweenUIClickables, got {clickables.Length}.");
            Require(buttons.Length == 10, $"Expected 10 buttons, got {buttons.Length}.");
            Require(buttons.Where(button => button.name == "Replay Animation")
                    .All(button => button.onClick.GetPersistentEventCount() == 1),
                "Every replay button must have exactly one persistent listener.");

            foreach (string panelName in new[]
                     {
                         "01  SLIDE PANEL", "02  MODAL PANEL", "03  ALERT PANEL",
                         "05  INTERACTION LAB", "06  SHARED PRESETS", "07  UTILITY MIX"
                     })
            {
                GameObject panelObject = all.First(item => item.name == panelName);
                Require(panelObject.transform.parent != null && panelObject.transform.parent.name.EndsWith(" Cell"),
                    panelName + " must animate inside a layout-owned cell.");
                Transform body = panelObject.transform.Find("Content");
                Require(body != null && body.GetComponent<VerticalLayoutGroup>() != null,
                    panelName + " content must use VerticalLayoutGroup.");
            }

            CheckPanel(all, "01  SLIDE PANEL", typeof(AnchorPositionTweenClip), typeof(FadeTweenClip), typeof(ColorTweenClip));
            CheckPanel(all, "02  MODAL PANEL", typeof(ScaleTweenClip), typeof(FadeTweenClip), typeof(PunchScaleTweenClip));
            CheckPanel(all, "03  ALERT PANEL", typeof(AnchorPositionTweenClip), typeof(FadeTweenClip), typeof(ShakeTweenClip));
            CheckPanel(all, "04 HUD Progress Panel", typeof(FadeTweenClip), typeof(AnchorPositionTweenClip),
                typeof(TextCounterTweenClip), typeof(FillAmountTweenClip));
            CheckPanel(all, "05  INTERACTION LAB", typeof(ScaleTweenClip), typeof(FadeTweenClip));
            CheckPanel(all, "06  SHARED PRESETS", typeof(AnchorPositionTweenClip), typeof(FadeTweenClip));
            CheckPanel(all, "07  UTILITY MIX", typeof(ScaleTweenClip), typeof(FadeTweenClip), typeof(PunchScaleTweenClip));

            ValidatePresetLibrary();
            ValidateComplexButtons(clickables);

            Debug.Log($"[UI Motion Composer] Showcase validation passed: 7 panels, 10 stateful buttons, {TweenPresetLibrary.AllPresetNames.Length} shared presets, no missing scripts.");
        }

        public static void ValidateFromCli()
        {
            ValidateShowcase();
        }

        private static void CheckPanel(IEnumerable<GameObject> all, string name, params System.Type[] expectedClips)
        {
            GameObject gameObject = all.FirstOrDefault(item => item.name == name);
            Require(gameObject != null, "Missing panel: " + name);
            TweenPlayer player = gameObject.GetComponent<TweenPlayer>();
            Require(player != null, name + " has no TweenPlayer.");
            TweenUIPanel panel = gameObject.GetComponent<TweenUIPanel>();
            Require(panel != null, name + " has no TweenUIPanel wrapper.");
            TweenAnimation animation = player.FindAnimation(TweenIds.Show);
            Require(animation != null, name + " has no Show animation.");
            Require(player.FindAnimation(TweenIds.Hide) != null, name + " has no Hide animation.");
            Require(player.PlayOnEnableAnimations.Contains(TweenIds.Show), name + " is not configured to play Show on enable.");

            foreach (System.Type expected in expectedClips)
                Require(animation.EffectiveClips.Any(clip => clip != null && clip.GetType() == expected),
                    $"{name} is missing {expected.Name}.");
        }

        private static void ValidatePresetLibrary()
        {
            foreach (string presetName in TweenPresetLibrary.AllPresetNames)
            {
                TweenAnimationAsset asset = TweenPresetLibrary.Load(presetName);
                Require(asset != null, "Missing shared preset asset: " + presetName);
                Require(asset.Clips.Count > 0, presetName + " contains no clips.");
                Require(asset.Clips.All(clip => clip != null), presetName + " contains a missing clip type.");
                Require(asset.Clips.OfType<TargetedTweenClip>().All(clip => clip.Target == null),
                    presetName + " contains a scene reference instead of a Target Slot.");
            }
        }

        private static void ValidateComplexButtons(IEnumerable<TweenUIClickable> clickables)
        {
            TweenUIClickable[] motionButtons = clickables
                .Where(clickable => clickable.name.StartsWith("Motion ", System.StringComparison.Ordinal))
                .ToArray();
            Require(motionButtons.Length == 6, $"Expected 6 complex motion buttons, got {motionButtons.Length}.");

            foreach (TweenUIClickable clickable in motionButtons)
            {
                TweenPlayer player = clickable.Player ?? clickable.GetComponent<TweenPlayer>();
                TweenAnimation hover = player.FindAnimation(TweenIds.Hover);
                Require(hover?.Asset != null, clickable.name + " does not use a shared Hover preset.");
                Require(hover.Playback.LoopMode == TweenLoopMode.None,
                    clickable.name + " still repeats the whole Hover animation.");
                Require(hover.EffectiveClips.Any(clip => clip is DurationTweenClip { IsInfinite: true }),
                    clickable.name + " has no infinite child clip.");
                string[] bindings = player.TargetOverrides.Select(entry => entry.Key).ToArray();
                foreach (string key in new[] { "Glow", "Ring", "Spark", "Label" })
                    Require(bindings.Contains(key), $"{clickable.name} is missing target binding '{key}'.");
                Require(player.FindAnimation(TweenIds.Unhover)?.Asset != null,
                    clickable.name + " cannot restore its children after Hover.");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new System.InvalidOperationException("Showcase validation: " + message);
        }

        private static void CreateSlidePanel(Transform parent, Vector2 position)
        {
            PanelParts panel = CreatePanel(parent, "01  SLIDE PANEL", "Move + Fade + Color", Blue, position);
            CreateBadge(panel.Body, "FROM LEFT", Blue);
            CreateText("Headline", panel.Body, "Smooth entrance",
                31, TextPrimary, TextAnchor.MiddleLeft, new Vector2(0f, 92f), new Vector2(390f, 48f), FontStyle.Bold);
            CreateText("Description", panel.Body,
                "Anchor Position starts from an offset while Fade and Color overlap on the same timeline.",
                19, TextSecondary, TextAnchor.UpperLeft, new Vector2(0f, 7f), new Vector2(390f, 112f));

            TweenPlayer player = panel.Root.gameObject.AddComponent<TweenPlayer>();
            var show = NewShowAnimation();
            show.Clips.Add(new AnchorPositionTweenClip
            {
                Label = "Slide from left",
                FromMode = TweenEndpointMode.OffsetFromInitial,
                FromOffset = new Vector2(-520f, 0f),
                ToMode = TweenEndpointMode.Initial,
                Delay = 0.05f,
                Duration = 0.78f,
                Ease = UIEase.OutBack
            });
            show.Clips.Add(Fade(panel.CanvasGroup, 0f, 1f, 0.05f, 0.42f));
            show.Clips.Add(new ColorTweenClip
            {
                Label = "Surface tint",
                Target = panel.Image,
                FromMode = TweenEndpointMode.Custom,
                FromValue = new Color(0.08f, 0.16f, 0.34f, 1f),
                ToMode = TweenEndpointMode.Custom,
                ToValue = Surface,
                Delay = 0.12f,
                Duration = 0.62f,
                Ease = UIEase.OutCubic
            });
            player.AnimationDefinitions.Add(show);
            player.AnimationDefinitions.Add(NewHideAnimation(panel.CanvasGroup));
            player.PlayOnEnableAnimations.Add(TweenIds.Show);
            TweenUIPanel wrapper = AddPanelWrapper(panel.Root.gameObject);
            CreateReplayButton(panel.Body, wrapper, new Vector2(0f, -174f), Blue);
        }

        private static void CreatePopPanel(Transform parent, Vector2 position)
        {
            PanelParts panel = CreatePanel(parent, "02  MODAL PANEL", "Scale + Fade + Punch", Purple, position);
            CreateBadge(panel.Body, "POP IN", Purple);

            Image icon = CreateImage("Modal Icon", panel.Body, new Color(Purple.r, Purple.g, Purple.b, 0.17f));
            SetRect(icon.rectTransform, new Vector2(0f, 78f), new Vector2(88f, 88f));
            CreateText("Icon Glyph", icon.transform, "✓", 46, Purple, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(88f, 88f), FontStyle.Bold);
            CreateText("Headline", panel.Body, "Quest completed",
                31, TextPrimary, TextAnchor.MiddleCenter, new Vector2(0f, -2f), new Vector2(400f, 45f), FontStyle.Bold);
            CreateText("Description", panel.Body, "The modal settles with a small punch after its main scale tween.",
                18, TextSecondary, TextAnchor.UpperCenter, new Vector2(0f, -58f), new Vector2(390f, 72f));

            TweenPlayer player = panel.Root.gameObject.AddComponent<TweenPlayer>();
            var show = NewShowAnimation();
            show.Clips.Add(new ScaleTweenClip
            {
                Label = "Pop scale",
                FromMode = TweenEndpointMode.Custom,
                FromValue = new Vector3(0.48f, 0.48f, 1f),
                ToMode = TweenEndpointMode.Initial,
                Delay = 0.06f,
                Duration = 0.62f,
                Ease = UIEase.OutBack
            });
            show.Clips.Add(Fade(panel.CanvasGroup, 0f, 1f, 0.05f, 0.34f));
            show.Clips.Add(new PunchScaleTweenClip
            {
                Label = "Settle punch",
                Target = panel.Root,
                Delay = 0.68f,
                Duration = 0.42f,
                Strength = new Vector3(0.055f, 0.055f, 0f),
                Vibrato = 5,
                Elasticity = 0.7f
            });
            player.AnimationDefinitions.Add(show);
            player.AnimationDefinitions.Add(NewHideAnimation(panel.CanvasGroup));
            player.PlayOnEnableAnimations.Add(TweenIds.Show);
            TweenUIPanel wrapper = AddPanelWrapper(panel.Root.gameObject);
            CreateReplayButton(panel.Body, wrapper, new Vector2(0f, -174f), Purple);
        }

        private static void CreateAlertPanel(Transform parent, Vector2 position)
        {
            PanelParts panel = CreatePanel(parent, "03  ALERT PANEL", "Move + Fade + Shake", Coral, position);
            CreateBadge(panel.Body, "ATTENTION", Coral);
            CreateText("Alert Symbol", panel.Body, "!", 76, Coral, TextAnchor.MiddleCenter,
                new Vector2(0f, 78f), new Vector2(100f, 100f), FontStyle.Bold);
            CreateText("Headline", panel.Body, "Inventory full",
                31, TextPrimary, TextAnchor.MiddleCenter, new Vector2(0f, -4f), new Vector2(390f, 46f), FontStyle.Bold);
            CreateText("Description", panel.Body, "A deterministic shake starts after the panel lands, without an Animator.",
                18, TextSecondary, TextAnchor.UpperCenter, new Vector2(0f, -60f), new Vector2(390f, 70f));

            TweenPlayer player = panel.Root.gameObject.AddComponent<TweenPlayer>();
            var show = NewShowAnimation();
            show.Clips.Add(new AnchorPositionTweenClip
            {
                Label = "Slide from right",
                FromMode = TweenEndpointMode.OffsetFromInitial,
                FromOffset = new Vector2(520f, 0f),
                ToMode = TweenEndpointMode.Initial,
                Delay = 0.05f,
                Duration = 0.68f,
                Ease = UIEase.OutCubic
            });
            show.Clips.Add(Fade(panel.CanvasGroup, 0f, 1f, 0.06f, 0.38f));
            show.Clips.Add(new ShakeTweenClip
            {
                Label = "Attention shake",
                Target = panel.Root,
                Delay = 0.74f,
                Duration = 0.62f,
                Strength = new Vector3(14f, 5f, 0f),
                Vibrato = 13,
                Randomness = 65f,
                FadeOut = true,
                Seed = 2026
            });
            player.AnimationDefinitions.Add(show);
            player.AnimationDefinitions.Add(NewHideAnimation(panel.CanvasGroup));
            player.PlayOnEnableAnimations.Add(TweenIds.Show);
            TweenUIPanel wrapper = AddPanelWrapper(panel.Root.gameObject);
            CreateReplayButton(panel.Body, wrapper, new Vector2(0f, -174f), Coral);
        }

        private static void CreateButtonLabPanel(Transform parent, Vector2 position)
        {
            PanelParts panel = CreatePanel(parent, "05  INTERACTION LAB",
                "Infinite child loops stop on exit", Blue, position);
            CreateText("Lab Hint", panel.Body,
                "Hover each button. Ring, spark and label are separate Target Slots in shared SO presets.",
                16, TextSecondary, TextAnchor.UpperLeft, new Vector2(0f, 145f), new Vector2(390f, 58f));

            CreateMotionButton(panel.Body, "Motion Orbit", "ORBIT + PULSE",
                new Vector2(0f, 70f), Blue, TweenPresetLibrary.ButtonOrbitHover);
            CreateMotionButton(panel.Body, "Motion Wave", "WAVE + BOUNCE",
                new Vector2(0f, -8f), Purple, TweenPresetLibrary.ButtonWaveHover);
            CreateMotionButton(panel.Body, "Motion Spectrum", "SPIN + COLOR",
                new Vector2(0f, -86f), Green, TweenPresetLibrary.ButtonSpectrumHover);

            TweenPlayer player = panel.Root.gameObject.AddComponent<TweenPlayer>();
            player.AnimationDefinitions.Add(SharedAnimation(TweenIds.Show, TweenPresetLibrary.PanelPopShow));
            player.AnimationDefinitions.Add(SharedAnimation(TweenIds.Hide, TweenPresetLibrary.PanelHide));
            player.PlayOnEnableAnimations.Add(TweenIds.Show);
            AddPanelWrapper(panel.Root.gameObject);
        }

        private static void CreatePresetGalleryPanel(Transform parent, Vector2 position)
        {
            PanelParts panel = CreatePanel(parent, "06  SHARED PRESETS",
                "One asset, different bindings", Purple, position);
            CreateText("Gallery Hint", panel.Body,
                "These buttons reuse the same preset assets with different colors and child objects.",
                16, TextSecondary, TextAnchor.UpperLeft, new Vector2(0f, 145f), new Vector2(390f, 58f));

            CreateMotionButton(panel.Body, "Motion Nebula", "NEBULA ORBIT",
                new Vector2(0f, 70f), Purple, TweenPresetLibrary.ButtonOrbitHover);
            CreateMotionButton(panel.Body, "Motion Signal", "SIGNAL WAVE",
                new Vector2(0f, -8f), Coral, TweenPresetLibrary.ButtonWaveHover);
            CreateMotionButton(panel.Body, "Motion Mint", "MINT SPECTRUM",
                new Vector2(0f, -86f), Green, TweenPresetLibrary.ButtonSpectrumHover);

            TweenPlayer player = panel.Root.gameObject.AddComponent<TweenPlayer>();
            player.AnimationDefinitions.Add(SharedAnimation(TweenIds.Show, TweenPresetLibrary.PanelSlideShow));
            player.AnimationDefinitions.Add(SharedAnimation(TweenIds.Hide, TweenPresetLibrary.PanelHide));
            player.PlayOnEnableAnimations.Add(TweenIds.Show);
            AddPanelWrapper(panel.Root.gameObject);
        }

        private static void CreateUtilityPanel(Transform parent, Vector2 position)
        {
            PanelParts panel = CreatePanel(parent, "07  UTILITY MIX",
                "Text + Fill + shared panel motion", Green, position);
            CreateBadge(panel.Body, "SO PRESET", Green);

            Text message = CreateText("Utility Message", panel.Body, "Reusable animation assets",
                28, TextPrimary, TextAnchor.MiddleCenter, new Vector2(0f, 55f), new Vector2(390f, 42f), FontStyle.Bold);
            CreateText("Utility Description", panel.Body,
                "The panel entrance is a shared asset; text reveal and progress are local clips layered on top.",
                17, TextSecondary, TextAnchor.UpperCenter, new Vector2(0f, -5f), new Vector2(390f, 70f));

            Image track = CreateImage("Utility Track", panel.Body, Hex("303B50"));
            SetRect(track.rectTransform, new Vector2(0f, -89f), new Vector2(340f, 14f));
            Image fill = CreateImage("Utility Fill", track.transform, Green);
            Stretch(fill.rectTransform);
            fill.sprite = _uiSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;

            TweenPlayer player = panel.Root.gameObject.AddComponent<TweenPlayer>();
            TweenAnimation show = SharedAnimation(TweenIds.Show, TweenPresetLibrary.PanelPopShow);
            // This panel intentionally stays inline: it demonstrates combining a reusable panel
            // preset with a second named local animation that owns content-specific targets.
            player.AnimationDefinitions.Add(show);
            player.AnimationDefinitions.Add(new TweenAnimation
            {
                Id = "Content",
                Clips = new List<BaseTweenClip>
                {
                    new TextRevealTweenClip
                    {
                        Label = "Reveal heading",
                        Target = message,
                        Delay = 0.1f,
                        Duration = 0.85f,
                        Ease = UIEase.OutQuad
                    },
                    new FillAmountTweenClip
                    {
                        Label = "Fill utility bar",
                        Target = fill,
                        FromMode = TweenEndpointMode.Custom,
                        FromValue = 0f,
                        ToMode = TweenEndpointMode.Custom,
                        ToValue = 0.76f,
                        Delay = 0.16f,
                        Duration = 1.1f,
                        Ease = UIEase.OutCubic
                    }
                }
            });
            player.AnimationDefinitions.Add(SharedAnimation(TweenIds.Hide, TweenPresetLibrary.PanelHide));
            player.PlayOnEnableAnimations.Add(TweenIds.Show);
            player.PlayOnEnableAnimations.Add("Content");
            AddPanelWrapper(panel.Root.gameObject);
        }

        private static void CreateMotionButton(Transform parent, string name, string label,
            Vector2 position, Color accent, string hoverPreset)
        {
            Image root = CreateImage(name, parent, new Color(accent.r, accent.g, accent.b, 0.82f));
            SetRect(root.rectTransform, position, new Vector2(370f, 62f));
            root.raycastTarget = true;

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = root;
            button.transition = Selectable.Transition.None;

            RectTransform ring = CreateRect("Ring", root.transform, new Vector2(-148f, 0f), new Vector2(42f, 42f));
            SetLeftCenter(ring, 12f, 42f, 42f);
            Image ringLine = CreateImage("Ring Line", ring, new Color(1f, 1f, 1f, 0.2f));
            SetRect(ringLine.rectTransform, Vector2.zero, new Vector2(32f, 5f));
            Image ringTip = CreateImage("Ring Tip", ring, Color.white);
            SetRect(ringTip.rectTransform, new Vector2(15f, 0f), new Vector2(7f, 7f));

            Image spark = CreateImage("Spark", ring, Color.white);
            SetRect(spark.rectTransform, new Vector2(0f, 15f), new Vector2(9f, 9f));

            Text labelText = CreateText("Label", root.transform, label, 14, Color.white,
                TextAnchor.MiddleCenter, new Vector2(24f, 0f), new Vector2(270f, 48f), FontStyle.Bold);
            SetStretch(labelText.rectTransform, 62f, 12f, 7f, 7f);

            TweenPlayer player = root.gameObject.AddComponent<TweenPlayer>();
            player.TargetOverrideDefinitions.Add(new TweenTargetOverride { Key = "Glow", Target = root });
            player.TargetOverrideDefinitions.Add(new TweenTargetOverride { Key = "Ring", Target = ring });
            player.TargetOverrideDefinitions.Add(new TweenTargetOverride { Key = "Spark", Target = spark });
            player.TargetOverrideDefinitions.Add(new TweenTargetOverride { Key = "Label", Target = labelText });
            AddButtonStateAnimations(player, hoverPreset, true);
            root.gameObject.AddComponent<TweenUIClickable>();
        }

        private static void CreateHudPanel(Transform parent, Vector2 position)
        {
            RectTransform cell = CreateLayoutSlot("04 HUD Progress Panel Cell", parent, 1320f, 142f);
            Image root = CreateImage("04 HUD Progress Panel", cell, Hex("182131"));
            Stretch(root.rectTransform);
            AddShadow(root.gameObject, new Color(0f, 0f, 0f, 0.38f), new Vector2(0f, -10f));
            CanvasGroup canvasGroup = root.gameObject.AddComponent<CanvasGroup>();

            Image accent = CreateImage("Accent", root.transform, Green);
            SetAnchorsAndOffsets(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(8f, 0f));

            Text hudLabel = CreateText("HUD Label", root.transform, "MISSION PROGRESS",
                16, Green, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(250f, 28f), FontStyle.Bold);
            SetAnchorsAndOffsets(hudLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.28f, 0.5f),
                new Vector2(36f, 7f), new Vector2(-12f, 35f));
            Text score = CreateText("Score Counter", root.transform, "0 / 2500 XP",
                30, TextPrimary, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(380f, 48f), FontStyle.Bold);
            SetAnchorsAndOffsets(score.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.3f, 0.5f),
                new Vector2(36f, -43f), new Vector2(-12f, 5f));

            Image track = CreateImage("Progress Track", root.transform, Hex("2C374A"));
            SetAnchorsAndOffsets(track.rectTransform, new Vector2(0.31f, 0.5f), new Vector2(0.7f, 0.5f),
                new Vector2(0f, -10f), new Vector2(0f, 10f));
            Image fill = CreateImage("Progress Fill", track.transform, Green);
            Stretch(fill.rectTransform);
            fill.sprite = _uiSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            Text hudHint = CreateText("HUD Hint", root.transform, "Counter + Image Fill Amount",
                16, TextSecondary, TextAnchor.MiddleRight, Vector2.zero, new Vector2(260f, 28f));
            SetAnchorsAndOffsets(hudHint.rectTransform, new Vector2(0.72f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(12f, 7f), new Vector2(-205f, 35f));

            TweenPlayer player = root.gameObject.AddComponent<TweenPlayer>();
            var show = NewShowAnimation();
            show.Clips.Add(Fade(canvasGroup, 0f, 1f, 0.12f, 0.35f));
            show.Clips.Add(new AnchorPositionTweenClip
            {
                Label = "HUD rise",
                FromMode = TweenEndpointMode.OffsetFromInitial,
                FromOffset = new Vector2(0f, -80f),
                ToMode = TweenEndpointMode.Initial,
                Delay = 0.08f,
                Duration = 0.58f,
                Ease = UIEase.OutBack
            });
            show.Clips.Add(new TextCounterTweenClip
            {
                Label = "XP counter",
                Target = score,
                FromValue = 0f,
                ToValue = 2500f,
                WholeNumbers = true,
                Format = "{0} / 2500 XP",
                Delay = 0.38f,
                Duration = 1.05f,
                Ease = UIEase.OutCubic
            });
            show.Clips.Add(new FillAmountTweenClip
            {
                Label = "Progress fill",
                Target = fill,
                FromMode = TweenEndpointMode.Custom,
                FromValue = 0f,
                ToMode = TweenEndpointMode.Custom,
                ToValue = 0.82f,
                Delay = 0.34f,
                Duration = 1.1f,
                Ease = UIEase.OutCubic
            });
            player.AnimationDefinitions.Add(show);
            player.AnimationDefinitions.Add(NewHideAnimation(canvasGroup));
            player.PlayOnEnableAnimations.Add(TweenIds.Show);
            TweenUIPanel wrapper = AddPanelWrapper(root.gameObject);
            CreateReplayButton(root.transform, wrapper, Vector2.zero, Green, new Vector2(170f, 42f));
            RectTransform replay = root.transform.Find("Replay Animation").GetComponent<RectTransform>();
            replay.anchorMin = replay.anchorMax = new Vector2(1f, 0.5f);
            replay.pivot = new Vector2(1f, 0.5f);
            replay.anchoredPosition = new Vector2(-24f, -26f);
            replay.sizeDelta = new Vector2(170f, 42f);
        }

        private static TweenAnimation NewShowAnimation()
        {
            return new TweenAnimation
            {
                Id = TweenIds.Show,
                Playback = new TweenPlaybackSettings
                {
                    UnscaledTime = true,
                    BlendMode = TweenBlendMode.Override,
                    KillBehavior = TweenKillBehavior.Cancel,
                    AllowSelfOverride = true,
                    LoopMode = TweenLoopMode.None,
                    LoopCount = 1
                },
                Clips = new List<BaseTweenClip>()
            };
        }

        private static TweenAnimation SharedAnimation(string id, string presetName)
        {
            TweenAnimationAsset asset = TweenPresetLibrary.Load(presetName);
            if (asset == null)
                throw new System.InvalidOperationException("Missing preset asset: " + presetName);

            return new TweenAnimation
            {
                Id = id,
                Asset = asset,
                Playback = new TweenPlaybackSettings
                {
                    UnscaledTime = true,
                    BlendMode = TweenBlendMode.Override,
                    KillBehavior = TweenKillBehavior.Cancel,
                    AllowSelfOverride = true,
                    LoopMode = TweenLoopMode.None,
                    LoopCount = 1
                }
            };
        }

        private static void AddButtonStateAnimations(TweenPlayer player, string hoverPreset,
            bool complexHover)
        {
            player.AnimationDefinitions.Add(SharedAnimation(TweenIds.Hover, hoverPreset));
            player.AnimationDefinitions.Add(SharedAnimation(TweenIds.Unhover, complexHover
                ? TweenPresetLibrary.ButtonReturn
                : TweenPresetLibrary.ButtonSoftReturn));
            player.AnimationDefinitions.Add(SharedAnimation(TweenIds.Click, complexHover
                ? TweenPresetLibrary.ButtonPress
                : TweenPresetLibrary.ButtonSoftPress));
            player.AnimationDefinitions.Add(SharedAnimation(TweenIds.Disabled, TweenPresetLibrary.ButtonDisabled));
            player.AnimationDefinitions.Add(SharedAnimation(TweenIds.Interactable, TweenPresetLibrary.ButtonInteractable));
        }

        private static TweenAnimation NewHideAnimation(CanvasGroup canvasGroup)
        {
            return new TweenAnimation
            {
                Id = TweenIds.Hide,
                Playback = new TweenPlaybackSettings
                {
                    UnscaledTime = true,
                    BlendMode = TweenBlendMode.Override,
                    KillBehavior = TweenKillBehavior.Cancel,
                    AllowSelfOverride = true,
                    LoopMode = TweenLoopMode.None,
                    LoopCount = 1
                },
                Clips = new List<BaseTweenClip>
                {
                    Fade(canvasGroup, 1f, 0f, 0f, 0.24f)
                }
            };
        }

        private static TweenUIPanel AddPanelWrapper(GameObject gameObject)
        {
            TweenUIPanel panel = gameObject.AddComponent<TweenUIPanel>();
            panel.HideOnAwake = false;
            panel.DeactivateWhenHidden = true;
            panel.ShowAnimationId = TweenIds.Show;
            panel.HideAnimationId = TweenIds.Hide;
            return panel;
        }

        private static FadeTweenClip Fade(CanvasGroup target, float from, float to, float delay, float duration)
        {
            return new FadeTweenClip
            {
                Label = "Panel fade",
                Target = target,
                FadeTarget = TweenFadeTarget.CanvasGroup,
                FromMode = TweenEndpointMode.Custom,
                FromValue = from,
                ToMode = TweenEndpointMode.Custom,
                ToValue = to,
                Delay = delay,
                Duration = duration,
                Ease = UIEase.OutQuad
            };
        }

        private static PanelParts CreatePanel(Transform parent, string title, string subtitle, Color accentColor, Vector2 position)
        {
            RectTransform cell = CreateLayoutSlot(title + " Cell", parent, 500f, 610f);
            Image root = CreateImage(title, cell, Surface);
            Stretch(root.rectTransform);
            AddShadow(root.gameObject, new Color(0f, 0f, 0f, 0.45f), new Vector2(0f, -14f));
            CanvasGroup canvasGroup = root.gameObject.AddComponent<CanvasGroup>();

            Image accent = CreateImage("Top Accent", root.transform, accentColor);
            SetTopStretch(accent.rectTransform, 0f, 0f, 0f, 8f);

            Text titleText = CreateText("Panel Title", root.transform, title,
                18, accentColor, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(410f, 34f), FontStyle.Bold);
            SetTopStretch(titleText.rectTransform, 45f, 45f, 30f, 34f);
            Text summaryText = CreateText("Clip Summary", root.transform, subtitle,
                17, TextSecondary, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(410f, 30f));
            SetTopStretch(summaryText.rectTransform, 45f, 45f, 66f, 30f);

            Image divider = CreateImage("Divider", root.transform, new Color(1f, 1f, 1f, 0.08f));
            SetTopStretch(divider.rectTransform, 45f, 45f, 108f, 2f);

            RectTransform body = CreateRect("Content", root.transform, Vector2.zero, Vector2.zero);
            SetStretch(body, 45f, 45f, 32f, 132f);
            var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return new PanelParts(root.rectTransform, body, root, canvasGroup);
        }

        private static void CreateBadge(Transform parent, string text, Color color)
        {
            RectTransform row = CreateRect("Badge Row", parent, Vector2.zero, new Vector2(420f, 34f));
            Image badge = CreateImage("Badge", row, new Color(color.r, color.g, color.b, 0.15f));
            SetLeftCenter(badge.rectTransform, 0f, 125f, 34f);
            CreateText("Badge Text", badge.transform, text, 14, color, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(125f, 34f), FontStyle.Bold);
        }

        private static void CreateReplayButton(Transform parent, TweenUIPanel targetPanel, Vector2 position,
            Color accent, Vector2? size = null)
        {
            Vector2 buttonSize = size ?? new Vector2(250f, 52f);
            Image image = CreateImage("Replay Animation", parent, new Color(accent.r, accent.g, accent.b, 0.92f));
            SetRect(image.rectTransform, position, buttonSize);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;

            CreateText("Label", image.transform, "REPLAY ANIMATION", 15, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, buttonSize, FontStyle.Bold);
            Stretch(image.transform.Find("Label").GetComponent<RectTransform>());
            UnityEventTools.AddPersistentListener(button.onClick, new UnityEngine.Events.UnityAction(targetPanel.Show));

            TweenPlayer buttonPlayer = image.gameObject.AddComponent<TweenPlayer>();
            buttonPlayer.TargetOverrideDefinitions.Add(new TweenTargetOverride { Key = "Glow", Target = image });
            AddButtonStateAnimations(buttonPlayer, TweenPresetLibrary.ButtonSoftHover, false);
            image.gameObject.AddComponent<TweenUIClickable>();
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize,
            Color color, TextAnchor alignment, Vector2 position, Vector2 size, FontStyle style = FontStyle.Normal)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = _font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            SetRect(text.rectTransform, position, size);
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            SetRect(rect, position, size);
            return rect;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            if (rect.parent != null && rect.parent.GetComponent<LayoutGroup>() != null)
            {
                LayoutElement element = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = size.x;
                element.preferredHeight = size.y;
                element.flexibleWidth = 1f;
                element.flexibleHeight = 0f;
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetStretch(RectTransform rect, float left, float right, float bottom, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetTopStretch(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetLeftCenter(RectTransform rect, float left, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(left, 0f);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetAnchorsAndOffsets(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static RectTransform CreateGridRow(Transform parent, string name)
        {
            RectTransform row = CreateRect(name, parent, Vector2.zero, new Vector2(1700f, 610f));
            LayoutElement element = row.GetComponent<LayoutElement>();
            element.preferredHeight = 610f;
            element.flexibleHeight = 0f;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 32f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return row;
        }

        private static RectTransform CreateLayoutSlot(string name, Transform parent, float width, float height)
        {
            RectTransform slot = CreateRect(name, parent, Vector2.zero, new Vector2(width, height));
            LayoutElement element = slot.GetComponent<LayoutElement>() ?? slot.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
            element.flexibleWidth = 1f;
            element.flexibleHeight = 0f;
            return slot;
        }

        private static void AddShadow(GameObject gameObject, Color color, Vector2 distance)
        {
            Shadow shadow = gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Plugins/UIMotionComposer", "Examples");
            EnsureFolder("Assets/Plugins/UIMotionComposer/Examples", "Showcase");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }

        private readonly struct PanelParts
        {
            public readonly RectTransform Root;
            public readonly RectTransform Body;
            public readonly Image Image;
            public readonly CanvasGroup CanvasGroup;

            public PanelParts(RectTransform root, RectTransform body, Image image, CanvasGroup canvasGroup)
            {
                Root = root;
                Body = body;
                Image = image;
                CanvasGroup = canvasGroup;
            }
        }
    }
}
