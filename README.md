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
4. Move and resize the colored blocks in **Visual Timeline**, or enter exact Delay and Duration
   values in the clip fields. Blocks overlap naturally; snapping defaults to 0.05 seconds and can
   be disabled temporarily with Alt.
5. Press **Capture Initial Pose** once the object and its layout look right. `Initial` and
   `Offset From Initial` keep using this serialized authoring snapshot until it is recaptured.
6. Scrub **Edit-mode preview**, rewind with **|<**, or press **Play preview**. **Loop** repeats the
   preview while editing and **Restore** returns the object to the pose captured when preview began.

Edit-mode preview owns an isolated Unity Animation Mode driver. Registered animated properties are
restored by Unity without adding preview entries to the normal Undo history; non-animatable values
use TweenPlayer's exact snapshot restoration. If the Animation window, Timeline or another preview
driver is already active, TweenPlayer waits until that mode is closed rather than taking it over.

Add **Tween UI Clickable** beside the player for normal UI controls. It owns the
`Normal / Hovered / Pressed / Disabled` state machine and stops the previous animation before
entering the next state, so an infinitely looping Hover cannot leak after pointer exit. Its compact
inspector only shows state animation IDs and optional events. `SetInteractable(bool)` updates both
its CanvasGroup and an attached Selectable. The lower-level **UI Event Trigger** remains available
when raw pointer/navigation events should map independently without a state machine.

The clip stack supports move, local/world rotation, scale, anchor position 2D/3D, size, pivot,
fade, color, image fill, punch, shake, jump, events, GameObject toggles, nested animation playback,
text reveal and numeric text counters. An inline clip can target the player object or a direct
object. A shared asset cannot serialize scene references, so its clips use a **Target Slot** such as
`Content` or `Icon`. Assign the asset to a player and its inspector creates a **Target bindings**
table for those slots; each player or prefab supplies its own objects. Empty slots target the player
root, while named but unbound slots are warned about and skipped.

Playback is available from code:

```csharp
TweenHandle handle = tweenPlayer.Play(TweenIds.Show);
handle.OnCompleted(() => Debug.Log("Shown"));
handle.OnCancelled(() => Debug.Log("Interrupted"));
handle.Stop();

tweenPlayer.Play("Attention");
tweenPlayer.Stop("Attention", complete: true);
```

Per-animation settings select scaled/unscaled time, override/additive blending, interruption
behaviour, restart/ping-pong loops and finite or infinite loop counts. Utility clips do not execute
their side effects in edit-mode preview.

Every duration clip also has independent **Repeat Mode**, **Repeat Count** and **Repeat Delay**.
Use clip Repeat when only a ring, glow or child element should cycle; use animation Loop when the
entire choreography, including its relative timing, should start again. An infinite clip keeps its
animation handle active until it is stopped, while one-shot sibling clips remain at their completed
values. The timeline draws repeated ranges with stripes and marks infinite clips with `∞`.

**Utility ▸ Play Tween Animation** starts an animation on another targeted `TweenPlayer`. Its
**Playback Mode** controls ownership: **Fire And Forget** leaves the child independent; **Wait**
holds the parent timeline at the trigger marker until the child finishes and cancels the child if
the parent is cancelled; **Link Lifetime** runs both in parallel, completing the child with a
completed parent and cancelling it with a cancelled parent. Waiting on an infinite child loop is
intentionally infinite. `GetDuration()` reports the authored parent timeline only; runtime spent in
**Wait** is dynamic and is not added to that value.

For an animation containing an infinitely repeated clip, `GetDuration()` reports one authored cycle
for timeline/preview scaling, while `IsInfinite(id)` reports the actual lifetime.

V2 playback is intentionally independent of DOTween. `DOTween.KillAll()`, `DOTween.timeScale` and
the DOTween inspector do not control V2 animations; use `TweenPlayer.Stop`, `StopAll`, `Complete`
and the returned `TweenHandle`. This keeps runtime sampling identical to the inspector preview.

To reuse a clip stack, create **Assets ▸ Create ▸ UI Motion Composer V2 ▸ Tween Animation**, enter
portable **Target Slot** names only where a clip must animate a child or external object, then assign
the asset to an animation's **Shared clip asset** field. Bind the resulting slots below its timeline;
**Find** resolves a child by hierarchy path first and then by GameObject name.

The reusable V2 preset library lives in `ScriptableObjects/V2`. Rebuild it from
**Tools ▸ UI Motion Composer V2 ▸ Rebuild V2 preset library**. Panel entrances, soft button states,
three complex clip-repeated hover variants, disabled/re-enabled states and return animations are regular
`TweenAnimationAsset` files: duplicate and edit them exactly like the old V1 preset assets.

### V2 panel lifecycle

Add **UI Motion Composer V2/Tween UI Panel** beside a player to get a ready-made panel lifecycle.
`TweenUIPanel` activates before Show, disables input while hiding, optionally deactivates after
Hide, and exposes both UnityEvents and C# callbacks:

```csharp
tweenPanel.Show(() => OpenFirstField());
tweenPanel.Hide(() => ReturnToPreviousScreen());
tweenPanel.InstantShow();
tweenPanel.InstantHide();
```

Its public method names match the legacy panel workflow, which keeps caller migration mechanical.
The custom inspector selects Show/Hide IDs from the attached player and warns about missing or
infinitely looping transition animations.

### Migrating legacy content

The migration commands intentionally keep legacy data and components in place:

* **Tools ▸ UI Motion Composer V2 ▸ Migrate selected legacy preset assets** creates new `_V2`
  `TweenAnimationAsset` files beside selected legacy presets.
* **Tools ▸ UI Motion Composer V2 ▸ Migrate selected legacy components** adds a `TweenPlayer`,
  converts Show/Hide/Hover/Click/Disable/Return animation data inline, imports the controller's
  serialized `TempValues` as the V2 Initial Pose and adds a `TweenUIPanel` to panel objects.

Position migration uses Anchor Position 3D, so old Z values and separate-axis timelines are not
lost. Inspect and preview the result, then remove the old controller only after its callers have
been switched to `TweenPlayer`.

### V2 showcase scene

Open `Examples/V2/UIMotionComposerV2Showcase.unity` and enter Play Mode. The scene contains seven
panels and ten buttons: slide, pop/modal, shake alert, counter/fill HUD, utility composition and two
shared-preset button galleries. Hover the lower motion buttons to see one child rotate forever while
other children independently pulse, jump, recolor or move at different periods. The Hover animation
itself no longer restarts; only the configured clips repeat. Leaving the button stops it and its
shared Return preset restores every bound child. Every panel uses `TweenUIPanel`; replay buttons use
the stateful `TweenUIClickable` wrapper.

The scene can be regenerated from **Tools ▸ UI Motion Composer V2 ▸ Rebuild V2 showcase scene** and
validated with the adjacent **Validate V2 showcase scene** command.

Run **Tools ▸ UI Motion Composer V2 ▸ Run V2 smoke tests** after changing runtime semantics. The
suite covers preview restore/refresh, serialized Initial values, target slots, nested playback,
finite Restart/Ping Pong and infinite clip repeats, reversed playback, overlapping-binding
diagnostics and the clickable state machine (including stopping an infinite Hover when the pointer
exits).

The same checks run as EditMode tests from `Tests/Editor`, so the Test Runner and `-runTests` in
batch mode report them case by case and keep going after a failure. The assertions live in
`TweenV2Validation` and both entry points call them, so there is one source of truth: the menu item
is the quick authoring-time pass, the tests are the reportable one.

### Playing an animation backwards

Because the sampler is a pure function of time, an animation can run in reverse without authoring a
second one — it starts at each clip's To value and walks back to its From value. The player keeps
the concrete endpoints resolved by the latest forward launch, so the default **From ▸ Current**
still returns to the value captured before that launch rather than resolving Current again at To:

```csharp
tweenPlayer.PlayReverse(TweenIds.Show);   // the Show, un-played
tweenPlayer.Play("Attention", reversed: true);
```

`PlayAnimationReverse(string)` is the void wrapper for UnityEvent listeners. Triggers only fire on
the way back when their **Fire On Reverse** is set. An infinitely repeating clip does not keep a
reversed play alive: reaching zero ends it, because a reversed play is bounded by definition. With
**Loop ▸ Restart** a reversed play loops backwards rather than flipping to forward on its second
pass.

## Assemblies

The package compiles into its own assemblies rather than `Assembly-CSharp`:

| Assembly | Location |
|---|---|
| `UIMotionComposer.Runtime` | `Scripts/` |
| `UIMotionComposer.Tools.Editor` | `Scripts/Tools/Editor/` |
| `UIMotionComposer.Inspector.Editor` | `Scripts/Tools/Inspector/Editor/` |
| `UIMotionComposer.V2.Editor` | `Scripts/V2/Editor/` |
| `UIMotionComposer.Tests.Editor` | `Tests/Editor/` |

The runtime assembly is auto-referenced, so game code in `Assembly-CSharp` keeps compiling
unchanged. Project code that lives in *its own* assembly definition must add a reference to
`UIMotionComposer.Runtime`.

Only `Unity.ugui` is referenced. DOTween, Odin and TextMeshPro stay optional exactly as before:
Odin and DOTween are auto-referenced precompiled assemblies, and TextMeshPro is reached by
reflection, so none of them is a hard compile-time dependency.

**Custom clip types are not supported from outside the package.** `BaseTweenClip` is public, but
`Capture`, `Evaluate` and `Restore` take internal state types, so the class cannot be subclassed
from another assembly. This is deliberate: closing the contract now keeps `TweenClipState` and
`TweenSampleInfo` free to change, and opening it later is not a breaking change. Add new clip types
inside `Scripts/V2/Runtime/`.

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
