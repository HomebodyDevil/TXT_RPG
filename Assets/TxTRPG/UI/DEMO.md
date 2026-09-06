# StoryTextPanel Demo

Place `Assets/TxTRPG/UI/Demo/StoryTextPanelDemo.prefab` under a Canvas and enter Play Mode.

The demo loads 16 messages with mixed speaker and paragraph lengths. This is enough content to inspect:

- newest-message alignment at the bottom;
- opacity fading toward the top;
- fixed scrollbar handle length;
- oldest-message alignment at the upper scrollbar endpoint;
- newest-message alignment at the lower scrollbar endpoint;
- messages with and without a speaker.

Edit `StoryTextPanelDemoData.asset` to change the sample content without modifying runtime code. Use `Tools > TxT RPG > UI > Demos > Rebuild Story Text Panel Demo` to restore the default sample data and rebuild the demo prefab.
