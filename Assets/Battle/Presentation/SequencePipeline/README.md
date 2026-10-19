# Documentation

This folder contains all battle presentation related assets, grouped into respective subfolders based on their roles:

Actions - Defines presentation actions and handles the assembly of related facts into structured action packets.

Compilation — Handles the conversion of presentation actions into executable plans using the appropriate recipes and presentation definitions.

Execution — Defines the execution plan models and runtime references consumed by presentation executors.

Facts — Defines and processes the presentation facts received from the battle simulation, including their associated metadata and payloads.

Presenters — Contains components responsible for displaying individual visual outcomes, such as damage numbers, health changes, and other presentation effects.

Timelining — Manages timeline presentation functionality, including animation tracks, runtime bindings, impact markers, and playback.

VisualScheduling — Coordinates the ordering, timing, and execution readiness of presentation plans. Determines when visual sequences may execute, independently of the simulation's ECS system scheduling.