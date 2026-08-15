# UIPanel

UI show/hide and hover/click animations driven by serialized presets.

The folder is self-contained: drop it into any Unity project and it compiles. Odin Inspector and
DOTween are **optional** — when they are present the package uses them, when they are not it falls
back to its own implementations, and the serialized data is identical either way.

## How the optional dependencies are wired

| | Plugin installed | Plugin missing |
|---|---|---|
| Inspector | `UIPanelOdinBridge` maps the package attributes onto their Sirenix equivalents; Odin draws everything | `UIPanelInspectorGUI` draws the same layout with IMGUI (boxes, tabs, foldouts, conditional fields, buttons) |
| Tweening | `DoTweenSequence` / `DoTweenTweener` forward to `DOTween.Sequence()` and `DOVirtual.Float` | `UITweenSequence` runs the same timeline from one coroutine on a hidden runner object |

Detection:

* **Odin** publishes `ODIN_INSPECTOR` itself, so the editor code just keys off that symbol.
* **DOTween** publishes nothing, so `UIPanelDefineSymbols` looks for `DG.Tweening.DOTween` on every
  domain reload and adds or removes `UIPANEL_DOTWEEN` for the active build target. Force a re-check
  from **Tools ▸ UI Panel ▸ Refresh Plugin Detection**.

Neither symbol needs to be set by hand, and nothing under `Scripts/` outside `Scripts/Tools/`
references either plugin.

## Presets stay valid across the switch

`AnimationProccesData.Ease` is `UIEase`, whose numeric values mirror `DG.Tweening.Ease` exactly.
Unity serializes an enum as its int, so presets authored with DOTween installed keep their easing
after DOTween is removed, and vice versa. Do not renumber `UIEase`.

Every animation is expressed as a single 0..1 float driving an unclamped lerp, so both backends
produce the same motion — including the overshoot of Back, Elastic and Bounce. The built-in
evaluator uses the same Penner equations and the same defaults (overshoot 1.70158, period 0.3)
DOTween does. The one place the two are only approximately equal is the `Flash` easing family.

## Layout attributes

`UIPanelSystem.Inspector` provides `BoxGroup`, `TabGroup`, `FoldoutGroup`, `LabelText`, `HideLabel`,
`InlineProperty`, `ShowIf`, `HideIf`, `MinMaxSlider` and `Button`.

Conditions take a member name (`ShowIf(nameof(IsEnabled))`) or a member plus an expected value
(`ShowIf(nameof(Mode), AnimationMode.Unified))`), not Odin's `"@expression"` strings — a plain
member name is the form both inspectors can evaluate. Where a condition needs to combine terms,
expose a private bool property and point `ShowIf` at that.

`TabGroup.TextColor` still accepts Odin's expression syntax; the fallback inspector understands the
`"@this.Member.Member"` shape and ignores anything more elaborate.
