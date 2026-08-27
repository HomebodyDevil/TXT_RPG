# AGENTS.md

These instructions apply to all AI agents working in this Unity repository.

## Product Targets

Treat this project as a single shared codebase that may ship to Steam on desktop and to mobile devices with different capabilities, resolutions, aspect ratios, and input methods.

- Keep gameplay rules, domain models, content definitions, and other platform-independent logic shared whenever practical.
- Isolate platform-specific behavior behind clear interfaces or adapters. Do not scatter platform preprocessor directives throughout gameplay code.
- Do not assume that the current editor platform, screen size, orientation, input device, graphics tier, locale, or store service is the only supported environment.
- Prefer designs that allow another platform, storefront, or device class to be added without rewriting core systems.
- Keep platform-specific packages and SDK calls out of shared assemblies when assembly boundaries can enforce the separation.

## Architecture and Extensibility

- Separate core game logic, presentation, input, persistence, and platform services. Core logic should not depend directly on Unity UI objects, device APIs, Steam APIs, or mobile store APIs unless there is a strong reason.
- Represent platform capabilities explicitly. Code must handle unavailable capabilities gracefully instead of assuming that every device supports achievements, cloud saves, vibration, a mouse, touch, or a gamepad.
- Prefer dependency injection, serialized configuration, ScriptableObjects, or focused factories over global platform checks and hard-coded device rules.
- Add extension points only where a real platform or product variation is expected. Avoid speculative abstractions that make ordinary changes harder.
- Preserve backward compatibility for saved data, settings, input bindings, and serialized Unity assets. Introduce migrations or versioning when formats change.
- Avoid hidden coupling through scene searches, object names, tags, static mutable state, or initialization order. Make dependencies and lifetimes clear.

## Input and Interaction

- Use Unity's Input System and semantic actions such as `Navigate`, `Submit`, `Cancel`, `Point`, and `OpenInventory`. Gameplay and UI code should consume actions, not individual keys or buttons.
- Support keyboard and mouse, common gamepads, and touch where the feature is available. Do not make one input method emulate another at the gameplay layer.
- Allow the active control scheme to change at runtime, including switching between mouse, keyboard, controller, and touch.
- Keep prompts and glyphs synchronized with the active control scheme. Do not hard-code keyboard labels in user-facing UI.
- Make controller navigation deterministic. Every interactive screen must have a sensible initial selection, navigation order, focus restoration behavior, and cancel path.
- Make touch targets comfortably sized and spaced. Do not rely on hover, right-click, precise pointing, or multi-touch gestures without an accessible alternative.
- Avoid gameplay logic that depends directly on frame rate or input polling frequency. Use the appropriate update loop and time source.
- Support rebinding where applicable, and avoid reserving essential actions for a single physical control.

## Responsive UI and Resolution Support

- Build layouts with anchors, layout groups, flexible sizing, and content-driven constraints. Do not position essential UI with fixed pixel offsets alone.
- Use an intentional `Canvas Scaler` policy and document the reference resolution and width/height matching behavior for each major UI surface.
- Support common desktop, phone, tablet, ultrawide, and narrow aspect ratios. Do not assume 16:9.
- Respect mobile safe areas, display cutouts, rounded corners, system gesture regions, and orientation changes.
- Define whether gameplay content expands, crops, or uses letterboxing when aspect ratios differ. Do not let this behavior emerge accidentally from camera settings.
- Keep critical controls, text, and status information visible at the smallest supported viewport and at enlarged accessibility text sizes.
- Ensure text can expand for localization. Avoid fixed-size containers that only fit the current language, and do not encode meaningful text into raster images.
- Validate scrolling, modal dialogs, tooltips, virtual keyboards, and focus behavior at constrained resolutions.

## Device Capability and Performance

- Establish scalable quality tiers instead of maintaining unrelated scenes or gameplay implementations for each device class.
- Treat mobile memory, thermal limits, battery consumption, storage, and sustained frame rate as design constraints. A fast editor or desktop run is not sufficient evidence of mobile performance.
- Avoid avoidable per-frame allocations, repeated object searches, unbounded collections, synchronous asset loading during interaction, and excessive UI rebuilds.
- Use pooling, asynchronous loading, asset lifetime management, batching, and appropriate texture or audio compression when measurements justify them.
- Choose frame-rate targets intentionally per platform and make time-dependent systems behave correctly at different frame rates.
- Provide graceful degradation for optional visual effects. Reducing quality must not remove gameplay information or make the UI unusable.
- Do not make definitive performance claims from static inspection. Use profiling or measurements on representative target hardware.

## Platform Services and Persistence

- Wrap Steam, mobile store, achievement, cloud-save, authentication, purchase, notification, and vibration integrations behind platform service interfaces.
- Treat platform SDK initialization and network availability as fallible. Define timeout, cancellation, retry, offline, and partial-failure behavior.
- Never let an optional platform service prevent the core game from starting unless that service is an explicit product requirement.
- Keep local saves functional independently of cloud synchronization. Resolve conflicts deterministically and protect against partial writes or corrupted data.
- Store secrets outside the repository. Do not log access tokens, purchase receipts, personal data, or sensitive platform identifiers.
- Keep store policies and platform entitlements authoritative. Validate purchases and ownership using the appropriate trusted service where required.

## Build and Configuration

- Keep platform differences in build profiles, configuration assets, assembly definitions, and platform adapters rather than manual scene edits.
- Do not depend on uncommitted local editor settings for a successful build. Required settings must be reproducible from version-controlled configuration or documented setup.
- Ensure platform-specific scenes, addressable content, scripting symbols, permissions, orientations, and quality settings are selected deliberately.
- Avoid modifying generated Unity folders such as `Library`, `Temp`, `Logs`, or `obj` as part of implementation work.
- Consider IL2CPP, code stripping, AOT restrictions, filesystem differences, path casing, and platform API availability when adding reflection, serialization, native plugins, or dynamic code.

## Accessibility and Usability

- Do not communicate essential state through color alone. Maintain sufficient contrast and provide text, shape, icon, or audio reinforcement where appropriate.
- Support readable text scaling and avoid UI motion, flashes, or vibration that cannot be reduced when they may affect accessibility.
- Provide equivalent access to essential functionality across supported input methods.
- Preserve player settings such as volume, text speed, control preferences, quality tier, and accessibility options across sessions.

## Implementation Workflow

Before changing a cross-platform feature:

1. Identify the shared behavior, platform-specific behavior, supported input schemes, affected screens, and persistence implications.
2. Trace relevant callers, scenes, prefabs, ScriptableObjects, assemblies, packages, and tests before editing.
3. Decide how unsupported capabilities and runtime device changes will behave.
4. Keep the change focused and consistent with existing project conventions.

After changing a cross-platform feature:

1. Review the diff and inspect Unity serialization changes for unintended asset modifications.
2. Run the most focused applicable Edit Mode and Play Mode tests.
3. Verify compilation for every affected build target when tooling is available.
4. Exercise keyboard and mouse, controller, and touch paths that the feature supports.
5. Check representative narrow, wide, phone, tablet, and desktop layouts, including safe areas and orientation changes where applicable.
6. Test failure and recovery paths for saves, platform services, asynchronous loading, and device disconnection when relevant.
7. Report which checks were actually performed and clearly state any target devices or builds that remain unverified.

## Decision Standard

Prefer the simplest implementation that remains maintainable across supported platforms. A feature is not complete merely because it works in the Unity Editor at one resolution with one input device.
