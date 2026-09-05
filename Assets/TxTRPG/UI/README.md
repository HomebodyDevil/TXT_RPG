# StoryTextPanel

`StoryTextPanel` displays localized, presentation-ready story messages with the newest message at the bottom.

## Setup

Place `Assets/TxTRPG/UI/Prefabs/StoryTextPanel.prefab` under a Canvas. The prefab has a default size of 760 by 520 reference pixels and should be anchored by its parent layout.

If the prefab needs to be regenerated, use `Tools > TxT RPG > Rebuild Story Text Panel Prefabs`.

The prefab uses `Assets/TxTRPG/UI/Styles/StoryTextPanelDefaultBackgroundStyle.asset` through `PanelBackgroundRenderer`. Edit that style for the shared default, or use `ApplyBackground`, `ChangeBackground`, `ClearBackground`, and `ResetBackgroundToDefault` for a specific panel.

## Runtime API

```csharp
[SerializeField] private StoryTextPanel storyTextPanel;

storyTextPanel.AddMessage("The road disappears into the fog.");
storyTextPanel.AddMessage("Stay close.", "Guide");
storyTextPanel.ScrollToOldest();
storyTextPanel.ScrollToLatest();
storyTextPanel.Clear();
```

Resolve localization keys before calling `AddMessage`. This keeps localization and presentation responsibilities separate.

## Inspector options

- `Allow User Scrolling`: Enables wheel, drag, touch, controller-driven ScrollRect input, and the scrollbar.
- `Scrollbar Side`: Places the scrollbar on the left or right.
- `Show Scrollbar Background`: Shows or hides the scrollbar track while keeping the handle usable.
- `Scrollbar Handle Size`: Sets a fixed handle length that does not shrink as messages accumulate.
- `Oldest Visible Opacity`: Sets the minimum opacity at the top of the viewport.
- `Fade Start From Bottom`: Controls where upward fading begins.
- `Fade Exponent`: Controls the fade curve.
- `Follow Latest Message`: Scrolls to the newest message after insertion.
- `Maximum Retained Messages`: Limits retained UI objects. Zero means unlimited and should only be used for bounded histories.

The standalone scrollbar maps its top position to the oldest content aligned at the viewport top and its bottom position to the newest content aligned at the viewport bottom.
