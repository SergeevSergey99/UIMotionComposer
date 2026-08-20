# UI Motion Composer

Compose UI motion from independent channels — alpha, position, rotation, scale, size and pivot —
each with its own window on a shared timeline and its own easing or curve. No Animation clips,
no Animator component, no keyframes to re-author per panel.

Every value is expressed relative to the panel's own authored pose, so what you compose is
portable: one asset drives any panel, wherever it happens to sit on screen.

Everything lives in the `UIMotionComposer` namespace (`UIMotionComposer.Inspector`,
`UIMotionComposer.Tweening` for the support layers). The folder is self-contained: drop it into any
Unity project and it compiles. Odin Inspector and DOTween are **optional** — when they are present
the package uses them, when they are not it falls back to its own implementations, and the
serialized data is identical either way.

## How the optional dependencies are wired

| | Plugin installed | Plugin missing |
|---|---|---|
| Inspector | `OdinBridge` maps the package attributes onto their Sirenix equivalents; Odin draws everything | `InspectorGUI` draws the same layout with IMGUI (boxes, tabs, foldouts, conditional fields, buttons) |
| Tweening | `DoTweenSequence` / `DoTweenTweener` forward to `DOTween.Sequence()` and `DOVirtual.Float` | `UITweenSequence` runs the same timeline from one coroutine on a hidden runner object |

Detection:

* **Odin** publishes `ODIN_INSPECTOR` itself, so the editor code just keys off that symbol.
* **DOTween** publishes nothing, so `DefineSymbols` looks for `DG.Tweening.DOTween` on every
  domain reload and adds or removes `UIMOTION_DOTWEEN` for the active build target. Force a re-check
  from **Tools ▸ UI Motion Composer ▸ Refresh Plugin Detection**.

Neither symbol needs to be set by hand, and nothing under `Scripts/` outside `Scripts/Tools/`
references either plugin.

## Presets stay valid across the switch

`AnimationProcessData.Ease` is `UIEase`, whose numeric values mirror `DG.Tweening.Ease` exactly.
Unity serializes an enum as its int, so presets authored with DOTween installed keep their easing
after DOTween is removed, and vice versa. Do not renumber `UIEase`.

Every animation is expressed as a single 0..1 float driving an unclamped lerp, so both backends
produce the same motion — including the overshoot of Back, Elastic and Bounce. The built-in
evaluator uses the same Penner equations and the same defaults (overshoot 1.70158, period 0.3)
DOTween does. The one place the two are only approximately equal is the `Flash` easing family.

## Components

* `UIPanelController` — a panel driven by preset assets or by animation data authored inline.
  Assigning a preset hides the matching inline data and takes precedence over it.
* `UIMultiPanelController` — a panel that shows and hides other panels alongside its own animation.
* `UIClickableController` — hover / click / disable animations, same preset-or-inline rule.
* `UIClickableInteractablePoller` — optional, see *Interactability* below.

## Interactability

Call `SetInteractable(bool)` rather than writing `canvasGroup.interactable` directly. The controller
cannot notice an external write without polling every frame, and a screenful of buttons each running
an `Update` is exactly the cost this package should not impose by default.

If some code really must set the flag itself, follow it with `RefreshInteractableState()`, or add a
`UIClickableInteractablePoller` to that specific button to restore the old polling behaviour.

## Interrupting an animation

Cutting a running animation short and starting another one continues from the pose the panel is
currently in, instead of first snapping to the new animation's configured initial value. That is
what keeps fast hover in/out and a Show interrupting a Hide readable.

Set `AnimationData.RestartFromInitialOnInterrupt` if a particular animation really should always
begin from its initial values, even mid-flight.

## Captured start values

Each controller stores the panel's authored pose once (`TempValues`) so presets can offset from it
and return to it. That is what makes a preset like "slide in from the bottom" reusable on any panel.

**Has Start Values** says whether that pose has been captured, and the pose itself only appears once
it has. Clearing the toggle makes the controller capture again on the next play; the **Save Start
Values** button captures immediately.

The capture forces a layout rebuild first, so panels inside a `LayoutGroup` record the pose the
layout gives them rather than the one authored in the prefab. If a panel is posed by something the
rebuild cannot see, capture manually once the panel looks right.

## Layout attributes

`UIMotionComposer.Inspector` provides `BoxGroup`, `TabGroup`, `FoldoutGroup`, `LabelText`, `HideLabel`,
`InlineProperty`, `ShowIf`, `HideIf`, `MinMaxSlider` and `Button`.

Conditions take a member name (`ShowIf(nameof(IsEnabled))`) or a member plus an expected value
(`ShowIf(nameof(Mode), AnimationMode.Unified))`), not Odin's `"@expression"` strings — a plain
member name is the form both inspectors can evaluate. Where a condition needs to combine terms,
expose a private bool property and point `ShowIf` at that.

`TabGroup.TextColor` still accepts Odin's expression syntax; the fallback inspector understands the
`"@this.Member.Member"` shape and ignores anything more elaborate.

## Writing a handler

`IAnimationHandler` instances are **shared**: a preset asset hands the same instances to every
controller referencing it. A handler must stay stateless during playback — read the config, push
tweens into the sequence, keep nothing. Per-play state belongs in `UIAnimationContext`.

For the rect-driven properties, subclass `TransformAnimationHandler` (Vector3) or
`Transform2DAnimationHandler` (Vector2) and implement `GetCurrentValue`, `GetStartValue` and
`ApplyValue`. Override `ApplyInterpolated` only when component-wise lerping is wrong — rotation does,
because euler angles take the long way round past 180 degrees.
