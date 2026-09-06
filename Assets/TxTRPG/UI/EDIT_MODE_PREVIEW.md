# Edit Mode Preview

Open `StoryTextPanelDemo.prefab` in Prefab Mode, or place it under a Canvas in a scene. Its 16 demo messages are serialized as preview-only children, so the layout is visible without entering Play Mode.

Select the nested `StoryTextPanel` and change its serialized options. Scrollbar placement, background visibility, handle size, and message opacity settings update in Edit Mode. Opacity is recalculated at the latest-message scroll position.

After changing `StoryTextPanelDemoData.asset`, run:

`Tools > TxT RPG > UI > Preview > Refresh Story Text Panel`

The preview-only message objects disable and remove themselves before runtime messages are populated, so they are not counted as gameplay history.
