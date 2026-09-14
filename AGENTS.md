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

- The generated-Prefab registration requirements below apply only when a reusable builder is justified. They do not require creating builders or permanent Tools menus for ordinary asset edits or one-off integration work.
- Every new generated-Prefab builder must declare a stable task ID, exact input and output paths, dependencies, category, default-selection policy, validation, and either an `IPrefabRebuildTaskProvider` registration or an explicit exclusion reason. Individual menus and registered tasks must call the same generation core. Scene, content, Addressables, Build Settings, manual Prefabs, and user Variants must not be added to the production Prefab batch unless a side-effect-free Prefab-only core is first separated and tested.

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

### Clarify Product and Design Direction Before Proceeding

- Write all agent-authored clarification and choice prompts for this user in Korean, including question titles, headers, question bodies, option labels, option descriptions, recommendation markers, and custom-answer guidance. This applies to both interactive question tools and plain-text questions. Preserve proper nouns, code identifiers, exact file paths, official product names, and technical terms where their original form is appropriate; do not write the surrounding question in Japanese, English, or another language unless the user explicitly requests that language. Before submitting a question tool call, check every user-visible text field for unintended language mixing. App-generated labels outside the agent's control are not covered by this rule.
- This rule applies to both instruction-document writing and implementation. When an unresolved ambiguity could materially affect the result, inspect the relevant context and ask the user to confirm a choice before finalizing the affected specification or implementing the affected change. Do not silently select an interpretation or present an unconfirmed recommendation as a settled requirement.
- Ask about the direction and intended result of the work, not permission to perform routine implementation steps. Relevant choices include calculation semantics, meaningful defaults, user-visible behavior, whether a change should affect one screen or shared Prefabs, data ownership and persistence behavior, compatibility expectations, and significant product scope or design trade-offs. Describe options in terms of their consequences for the user and the product.
- Within the authorized scope, independently choose appropriate tools, commands, code organization, editing methods, and verification steps. Do not present choices such as "May I inspect files?", "May I edit this script?", or "Which tool may I run?" merely to obtain permission for ordinary task execution. Inspect available context before asking a direction question. Ask about a technical choice only when its consequences materially change the agreed result or constraints, and explain those consequences rather than asking the user to select a tool.
- Minimize discretionary permission questions, but do not bypass mandatory environment approvals, security controls, or authorization boundaries. A request to write an instruction document does not authorize implementing it. If a step requires additional authority, destructive changes outside the agreed scope, or an external side effect not covered by the request, obtain the required approval separately from product-direction questions. Do not disguise an approval request as a design choice or treat the absence of a reply as consent.
- Ask concise, self-contained questions with a small set of relevant, clearly differentiated options. Put the recommended option first and briefly explain each option's impact and trade-offs. Always provide a final way to enter a custom answer: use the question UI's built-in free-text option when available; otherwise end the numbered list with an explicit custom-answer option in the response language. Do not duplicate free-text controls already provided by the UI.
- Wait for an explicit answer before proceeding with work that depends on that decision. Do not skip a decision, select a default, or continue dependent work merely because time has elapsed. Silence, preselected UI options, countdown expiry, and automatic skip or dismissal are not approval. Keep the decision pending until the user answers or explicitly changes or withdraws the request. Unaffected read-only investigation and clearly scoped independent work may continue while awaiting the answer.
- Do not impose an agent-selected response deadline on clarification questions. Disable automatic timeouts when the available question tool supports doing so. If an app-controlled timer cannot be disabled, do not claim otherwise: preserve the unanswered decision and wait for an explicit reply, including a chat reply if the question UI closes. Do not add recurring reminders or alarms unless the user separately requests them.
- Preserve the lifetime of unanswered interactive questions. Prefer a blocking question tool when available in the current mode. When using a non-blocking question tool, a successful submission only confirms acceptance of the request, not that the user can see or answer it. Do not immediately send a final response after submitting the question; keep the turn active using the supported input-aware wait mechanism while awaiting the reply. Put any necessary explanation before the question and avoid duplicate requests while one is pending.
- If interactive choices disappear or cannot be answered, do not repeatedly resend them and immediately end the turn. Report the limitation and use an accessible plain-text clarification consistent with the current environment's instructions. Never infer a selected answer from a disappearing prompt. Treat this workflow as a mitigation, not proof that an application defect has been fixed; verify visibility and answer receipt before claiming success.
- Do not ask again about choices already specified by the user or established by applicable project requirements. Routine details that do not materially change the result may follow existing conventions. Record confirmed choices in the resulting instruction document or relevant implementation documentation.

### Direct Editor Application and Tooling Policy

- Temporary Tools menus for applying one-off changes are permitted, including menus intended for the user to run manually after an implementation task. Keep them narrowly scoped, identify their purpose and temporary status, and document whether they can be safely rerun. Reserve permanent tools for recurring authoring, validation, or build needs. This permission supersedes older instruction documents that prohibit temporary application menus; it does not authorize unrelated or destructive changes.
- For implementation requests, include the necessary Prefab, Scene, and configuration application work in the plan. Use available Unity Editor automation when appropriate, or provide a temporary application menu for explicit manual application. Clearly distinguish implemented code and tools from changes already applied and verified in the Editor. Analysis-only and documentation-only requests do not authorize Editor mutations.
- Treat stored assets as the source of truth for developer-authored Prefabs. Modify only the necessary parts; do not use full regeneration as the default application method. For genuinely code-generated assets, keep the generator as the source of truth and explicitly separate generated content from developer-owned customization.
- Connect runtime-selected services through explicit initialization and binding paths. Store mandatory internal Prefab components and references in a complete, valid state in the asset. Do not hide missing configuration through unrestricted scene searches or duplicate service creation.
- Do not attach destructive automatic regeneration to compilation, domain reload, or OnValidate callbacks.
- Before applying changes, check Editor connectivity, permissions, compilation state, and unsaved changes. Wait for required compilation and domain reload to finish, resolve relevant compilation errors, and execute only the application operations needed for the current task. Preserve user edits, Prefab overrides, scene layout, and player save data; do not bypass permissions or silently save unrelated changes.
- Verify saved assets and Scene connections after application, and perform relevant Edit Mode or Play Mode checks in proportion to risk. A successful menu invocation or code compilation alone does not prove that the requested feature is applied and working.
- If Editor automation is unavailable or blocked, report the concrete cause, checks attempted, completed work, and remaining application or verification steps. Do not report the task as complete merely because a new menu or manual procedure was provided. Request only the user action or authority actually needed to proceed.
- This policy does not authorize deleting existing tools, converting all generated assets, or rebuilding unrelated assets. Handle broader tooling cleanup and source-of-truth migrations as separate, explicitly scoped work.

### Required Handoff for Manual Tools Steps

- After implementing an instruction document, if any Tools menu must still be run by the user, include a separate, clearly labeled section in the final response (for Korean responses, use `사용자가 수행할 적용 절차`). Do not bury required steps in a general summary or present them as optional suggestions.
- Put the complete ordered procedure in a fenced `text` code block so it can be copied. Use numbered steps and exact, verified menu paths. Explain prerequisites such as waiting for compilation, leaving Play Mode, opening the correct Scene or Prefab, selecting a target object, and handling unsaved changes without discarding user work.
- For each menu, state what it changes, how to run it (including required selections, options, and confirmation dialogs), what successful completion looks like, and whether it saves automatically or requires an explicit save. State which step must finish before the next one starts; stop instructions must be clear if an error occurs.
- State whether each operation is one-time or safely repeatable, and warn about overwrites or other material side effects before the execution step. Explain any required backup or recovery procedure. Do not remove a temporary menu before the user has had an opportunity to apply the change; document when it may be retired.
- End the procedure with the exact validation path, including the starting Scene, Play Mode actions, and expected visible or functional result where applicable. Record the same procedure in the relevant development documentation so it remains discoverable after the conversation.
- Report separately: implementation completed, application already performed, application still required, and verification performed or pending. Never claim the feature is fully applied or runtime-verified while required manual steps remain. If no manual Tools steps remain, say so explicitly instead of listing menus unnecessarily.

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

## Project Documentation

Treat `DOCS/README.md` as the entry point for repository architecture and development documentation. Read the relevant documents under `DOCS/` before changing an area they describe.

- Keep documentation in the same change as the code or asset structure it describes.
- Update `DOCS/architecture/project-structure.md` when adding, removing, moving, or materially changing a major directory, assembly, subsystem, runtime entry point, or dependency boundary.
- Update the relevant feature architecture document when class responsibilities, object or prefab hierarchies, serialized references, data flow, lifecycle behavior, extension points, or failure behavior change.
- Update `DOCS/development/workflows.md` when setup steps, Editor menu paths, generation commands, test locations, validation procedures, or required tooling change.
- Add a focused document under `DOCS/architecture/` for a new subsystem that cannot be explained clearly in the project structure document. Add a focused document under `DOCS/development/` for a substantial new authoring, build, deployment, migration, or operational workflow.
- When the user asks for a design document that is also intended to guide future implementation, an implementation guide, a work specification, or instructions for another agent, create the instruction document under `DOCS/instructions/`. Use `DOCS/architecture/` only for verified, currently implemented architecture, and keep planned work clearly separated from current behavior.
- Link every new instruction document from the `작업 지침서` section of `DOCS/README.md`. Give the file a focused kebab-case name and include its objective, prerequisites, in-scope and out-of-scope work, affected paths and dependencies, implementation requirements, compatibility and failure behavior, verification criteria, documentation updates, and expected final report.
- After creating an instruction document, explain in the final response how the user should invoke it. Assume the user will attach the instruction file when requesting implementation. Provide a clickable link to the exact document path outside the copyable prompt, and write the ready-to-use prompt in terms of the attached instruction document rather than requiring the user to type or replace a repository path. For example: "첨부한 작업 지침서를 읽고 지침에 따라 구현해줘. 적용과 검증까지 수행하고, 완료한 내용과 남은 절차를 보고해줘." Tailor the prompt to the document's actual scope; do not add unrelated work or implicit commit-and-push authorization.
- If several instruction files are involved, identify each by its attachment filename and state its role or execution order where needed. Do not imply that a file is already attached or has been read when merely providing an example request. If the executing agent cannot access a required attachment, it should report the missing file and request it rather than guessing its contents. Mention optional scope, verification, or commit-and-push clauses separately only when relevant.
- Present ready-to-use task instructions and prompts intended for the user to copy into another agent or task in fenced code blocks delimited by three backticks, optionally labeled `text`. Do not use Markdown blockquotes (`>`) for these instructions. Keep explanations outside the code block and include the complete copyable request inside it. This applies both to instruction-document invocation examples and to standalone task prompts.
- Link every new document from `DOCS/README.md`. Avoid creating isolated documentation that cannot be discovered from the documentation index.
- Use exact repository paths, class names, assembly names, prefab names, ScriptableObject names, and Editor menu paths. Verify them against the current worktree before finalizing documentation.
- Describe verified current behavior separately from planned or recommended behavior. Do not document an unimplemented design as if it already exists.
- Prefer concise responsibility tables and small Mermaid diagrams when they make dependencies, ownership, hierarchy, or execution order materially easier to understand.
- Do not treat generated directories such as `Library`, `Temp`, `Logs`, or `obj` as authoritative architecture. Document version-controlled source, assets, configuration, and reproducible generation workflows.
- When changing a documented area, confirm that existing links remain valid and that the documentation index still reflects the available documents.

Documentation-only changes still require verification of referenced paths and symbols. Code or asset changes are not complete when their associated documentation is materially inaccurate.
## Decision Standard

Prefer the simplest implementation that remains maintainable across supported platforms. A feature is not complete merely because it works in the Unity Editor at one resolution with one input device.
