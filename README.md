# UI Motion Composer

Compose Unity UI motion from independent clips for position, rotation, scale, size, pivot, alpha,
color, fill, text and utility actions. Each clip owns its timing, easing, target and repeat settings
on one visual timeline. The package does not require Animator clips or DOTween.

Runtime types live in the `UIMotionComposer` namespace. Editor tooling lives in
`UIMotionComposer.Editor`.

## Quick start

1. Add **UI Motion Composer/Tween Player** to a UI object.
2. Press **+ Animation** and enter an ID such as `Show`, `Hide`, `Hover` or `Click`.
3. Press **+ Add clip** and choose a Transform, Rect Transform, Visual, Effect, Text or Utility clip.
4. Arrange clips in **Visual Timeline**, or enter exact Delay and Duration values.
5. Press **Capture Initial Pose** after layout has settled.
6. Scrub or play the edit-mode preview. **Restore** returns every affected object to its captured
   pre-preview pose.

`Initial` and `Offset From Initial` use the serialized pose captured by the player. This makes a
single animation reusable on UI objects with different authored positions and sizes.

The **Initial Pose** section lists every saved object, hierarchy path, property and value. **Restore
Pose** applies the complete saved pose without recapturing it; each property also has its own Restore
button. Missing targets are reported and can be removed. This is separate from preview Restore,
which returns objects to the state they had immediately before preview started.

## Timeline

Timeline blocks can overlap freely. Drag a block to move it and drag either edge to resize it.
Snapping defaults to 0.05 seconds and can be temporarily disabled with Alt.

- Zoom with the slider or Ctrl/Command + mouse wheel.
- Pan with the scrollbar or Shift + mouse wheel.
- Ctrl/Command-click toggles clips in the selection.
- Shift-click selects a range.
- Drag empty lane space to create a marquee selection.
- Selected clips move, align, nudge and change rows together.

Repeated clips are striped and infinite repeats are marked with `∞`.

## State-driven controls

Add **UI Motion Composer/Tween UI Clickable** beside a player for buttons, tabs and other selectable
controls. It resolves states in this priority order:

`Disabled > Pressed > Selected > Hovered > Normal`

Only one state animation owns the control at a time. Entering a state stops the previous state
handle first, including infinite Hover loops. The inspector provides animation dropdowns, missing-ID
warnings, a transition reference, edit-mode previews and live Play Mode diagnostics.

**Conventional IDs** assigns the usual `Unhover`, `Hover`, `Click`, `Disabled` and `Interactable`
mappings. `Selected` initially shares `Hover`; assign a separate animation when selection needs a
different appearance.

`SetInteractable(bool)` updates the CanvasGroup and an attached Selectable. Call
`RefreshInteractableState()` after another system changes either source directly.

Use the lower-level **UI Motion Composer/UI Event Trigger** when pointer and navigation events should
launch unrelated animations without state ownership.

## Panel lifecycle

Add **UI Motion Composer/Tween UI Panel** for a ready-made panel lifecycle. It activates before Show,
disables input while hiding and can deactivate the GameObject when Hide completes.

```csharp
tweenPanel.Show(() => OpenFirstField());
tweenPanel.Hide(() => ReturnToPreviousScreen());
tweenPanel.InstantShow();
tweenPanel.InstantHide();
```

The inspector selects Show and Hide IDs from the attached player and reports missing or infinitely
looping transition animations.

## Shared animation assets and target slots

Create a reusable clip stack with **Assets/Create/UI Motion Composer/Tween Animation**. A
ScriptableObject cannot store references to scene objects, so shared clips use portable **Target
Slot** names such as `Content`, `Icon` or `Glow`.

Assign the asset to an animation and bind its slots on each TweenPlayer. An empty slot targets the
player root. A named but unbound slot is reported and skipped instead of silently animating the wrong
object.

The inspector derives the expected target type and all consuming clips for every slot. Bindings can
use an explicit object, the player itself, a relative child path, a descendant name, or a component
search. **Auto Bind All** uses slot names and only falls back to a type match when that match is
unambiguous. Each row reports Missing, Wrong type or Resolved and provides Find, Ping and Clear
actions.

Enable **Local** on a slot to override its player-wide binding only for the selected animation. This
lets several animations reuse the same shared asset with different children without duplicating the
asset or changing the default binding used by the rest of the player.

The reusable presets are in `ScriptableObjects/Presets`. Rebuild them with
**Tools/UI Motion Composer/Rebuild preset library**.

## Clip and animation loops

Every duration clip has independent **Repeat Mode**, **Repeat Count** and **Repeat Delay**. Use a clip
repeat when only one child or property should cycle. Use animation playback looping when the entire
choreography should restart.

An infinite clip keeps its animation handle active until stopped while one-shot sibling clips retain
their completed values. `GetDuration()` returns one authored timeline cycle and `IsInfinite(id)`
reports the actual lifetime.

## Layering

Animations sample in launch order, oldest first. Each clip writes only its selected components;
the last write wins. Within an animation, clips sample in list order, also during preview.

For example, A animates XY, B animates Z and C animates Y: the result is A.X, C.Y, B.Z. All three
keep running. Once C finishes or is stopped, A's Y becomes visible on the next update if A is still
active. Color followed by Fade works the same way: Fade changes alpha and preserves the current RGB.

Overlapping writes never cancel clips or animations. Handles finish with their timelines or explicit
Stop/Complete calls; infinite animations need an explicit stop. Paused animations keep contributing
their frozen sample. Animations launched by a callback start at time zero after existing writers in
that update. State wrappers such as TweenUIClickable still explicitly stop their previous state.

The old Kill Behavior setting and binding-conflict warnings have been removed. Existing serialized
clips and target bindings are unchanged; legacy KillBehavior YAML fields are simply ignored.

## Nested animations

**Utility/Play Tween Animation** starts an animation on another targeted TweenPlayer:

- **Fire And Forget** leaves the child independent.
- **Wait** pauses the parent marker until the child completes and cancels the child with the parent.
- **Link Lifetime** runs both in parallel and links completion or cancellation.

Waiting on an infinite child is intentionally infinite.

## Runtime API

```csharp
TweenHandle handle = tweenPlayer.Play(TweenIds.Show);
handle.OnCompleted(() => Debug.Log("Shown"));
handle.OnCancelled(() => Debug.Log("Interrupted"));

tweenPlayer.PlayReverse(TweenIds.Show);
tweenPlayer.Stop("Attention");
tweenPlayer.Complete("Attention");
tweenPlayer.StopAll();
```

`PlayAnimation(string)` and `PlayAnimationReverse(string)` are void wrappers for persistent
UnityEvent listeners.

Playback uses the package sampler in both runtime and preview. `DOTween.KillAll()`,
`DOTween.timeScale` and the DOTween inspector do not control these animations.

## Preview safety

Edit-mode preview owns an isolated Unity Animation Mode driver. Animated properties are restored by
Unity; non-animatable values use the player's captured playback snapshot. Utility side effects do
not execute during preview.

If the Animation window, Timeline or another preview driver is active, the composer reports the
conflict instead of taking control from it.

## Showcase and validation

Open `Examples/Showcase/UIMotionComposerShowcase.unity`. It contains seven panels, ten stateful
buttons and complex animations where child elements rotate, scale, jump, recolor and move at
independent repeat periods. The scene uses a 1920x1080 reference Canvas, a vertical ScrollRect,
layout-owned card cells and responsive anchored content, so card dimensions can be changed through
their LayoutElement without manually repositioning their children.

- **Tools/UI Motion Composer/Rebuild showcase scene** regenerates it.
- **Tools/UI Motion Composer/Validate showcase scene** checks scripts, panels, buttons and presets.
- **Tools/UI Motion Composer/Run smoke tests** runs the authoring/runtime invariant suite.

The same checks are exposed as EditMode tests in `Tests/Editor`.

## Assemblies

| Assembly | Purpose |
|---|---|
| `UIMotionComposer.Runtime` | Runtime player, clips, easing and UI wrappers |
| `UIMotionComposer.Editor` | Inspectors, timeline, presets, showcase and validation |
| `UIMotionComposer.Tests.Editor` | EditMode smoke tests |

The runtime assembly is auto-referenced. Project code in its own assembly definition must add a
reference to `UIMotionComposer.Runtime`.

Only `Unity.ugui` is required. TextMeshPro support is reached through reflection and is not a hard
assembly dependency.
