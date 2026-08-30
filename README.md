# UI Motion Composer

Compose UI motion from independent channels — alpha, position, rotation, scale, size and pivot —
each with its own window on a shared timeline and its own easing or curve. No Animation clips,
no Animator component, no keyframes to re-author per panel.

Every value is expressed relative to the panel's own authored pose, so what you compose is
portable: one asset drives any panel, wherever it happens to sit on screen.

Everything lives in the `UIMotionComposer` namespace (`UIMotionComposer.Inspector`,
`UIMotionComposer.Tweening` for the support layers). The folder is self-contained: drop it into any
Unity project and it compiles. Odin Inspector is optional. DOTween is also optional for the legacy
V1 controllers: they use it when present and fall back to the built-in sequence implementation.
The clip-based V2 player deliberately uses its own sampler in both edit mode and runtime.

## V2: clip-based composer

`TweenPlayer` is the new authoring workflow. It lives next to the legacy controllers, so existing
scenes keep working while screens are migrated one at a time.

1. Add **UI Motion Composer V2/Tween Player** to a UI object.
2. Press **+ Animation**, give it an ID such as `Show`, `Hide`, `Hover` or `Click`.
3. Press **+ Add clip** and choose clips from Transform, Rect Transform, Visual, Effects or Utility.
4. Configure each clip's Delay and Duration. Clips overlap naturally and run on the same timeline.
5. Press **Capture Initial Pose** once the object and its layout look right. `Initial` and
   `Offset From Initial` keep using this serialized authoring snapshot until it is recaptured.
6. Scrub **Edit-mode preview** or press **Play preview**; **Restore** returns the object to the pose
   captured when preview began.

Edit-mode preview owns an isolated Unity Animation Mode driver. Registered animated properties are
restored by Unity without adding preview entries to the normal Undo history; non-animatable values
use TweenPlayer's exact snapshot restoration. If the Animation window, Timeline or another preview
driver is already active, TweenPlayer waits until that mode is closed rather than taking it over.

Add **UI Event Trigger** beside the player for no-code Hover/Unhover/Click/selection wiring. Its
inspector shows the animation IDs currently available on that player and flags missing IDs.

The clip stack supports move, local/world rotation, scale, anchor position 2D/3D, size, pivot,
fade, color, image fill, punch, shake, jump, events, GameObject toggles, nested animation playback,
text reveal and numeric text counters. A clip can target the player object, a direct object, or a
named override in **Targets and automatic playback**. Named overrides make shared animation assets
portable between prefabs.

Playback is available from code:

```csharp
TweenHandle handle = tweenPlayer.Play(TweenIds.Show);
handle.Stop();

tweenPlayer.Play("Attention");
tweenPlayer.Stop("Attention", complete: true);
```

Per-animation settings select scaled/unscaled time, override/additive blending, interruption
behaviour, restart/ping-pong loops and finite or infinite loop counts. Utility clips do not execute
their side effects in edit-mode preview.

V2 playback is intentionally independent of DOTween. `DOTween.KillAll()`, `DOTween.timeScale` and
the DOTween inspector do not control V2 animations; use `TweenPlayer.Stop`, `StopAll`, `Complete`
and the returned `TweenHandle`. This keeps runtime sampling identical to the inspector preview.

To reuse a clip stack, create **Assets ▸ Create ▸ UI Motion Composer V2 ▸ Tween Animation** and
assign it to an animation's **Shared clip asset** field.

### Migrating legacy content

The migration commands intentionally keep legacy data and components in place:

* **Tools ▸ UI Motion Composer V2 ▸ Migrate selected legacy preset assets** creates new `_V2`
  `TweenAnimationAsset` files beside selected legacy presets.
* **Tools ▸ UI Motion Composer V2 ▸ Migrate selected legacy components** adds a `TweenPlayer`,
  converts Show/Hide/Hover/Click/Disable/Return animation data inline and imports the controller's
  serialized `TempValues` as the V2 Initial Pose.

Position migration uses Anchor Position 3D, so old Z values and separate-axis timelines are not
lost. Inspect and preview the result, then remove the old controller only after its callers have
been switched to `TweenPlayer`.

### V2 showcase scene

Open `Examples/V2/UIMotionComposerV2Showcase.unity` and enter Play Mode. The scene contains four
editable examples: a slide panel, a pop/modal panel, a shake alert and a HUD with a numeric counter
and filled progress image. Every panel plays `Show` on enable; each Replay button also demonstrates
the no-code hover/click trigger.

The scene can be regenerated from **Tools ▸ UI Motion Composer V2 ▸ Rebuild V2 showcase scene** and
validated with the adjacent **Validate V2 showcase scene** command.

## How the optional dependencies are wired in V1

| | Plugin installed | Plugin missing |
|---|---|---|
| Inspector | `OdinBridge` maps the package attributes onto their Sirenix equivalents; Odin draws everything | `InspectorGUI` draws the same layout with IMGUI (boxes, tabs, foldouts, conditional fields, buttons) |
| V1 tweening | `DoTweenSequence` / `DoTweenTweener` forward to `DOTween.Sequence()` and `DOVirtual.Float` | `UITweenSequence` runs the same timeline from one coroutine on a hidden runner object |

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
