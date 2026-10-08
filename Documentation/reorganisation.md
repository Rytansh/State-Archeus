# State: Archeus — Project Reorganisation

- [ ] Commit the current compiling project
  Create a clean Git checkpoint before changing anything.

- [ ] Create the high-level domains
  Establish Core, Content, Game and Battle, with Contracts, Input, Simulation and Presentation inside Battle.

- [ ] Reorganise Turnflow
  Move turn components, requests, systems, groups and runtime helpers into their domain.

- [ ] Reorganise Simulation Input
  Move routing systems, input handlers and entity resolvers. Keep the external gateway and shared contracts separate.

- [ ] Reorganise Setup and Cards
  Group battle creation, initialisation, spawning, card zones and card operations.

- [ ] Reorganise Actions, Events, Effects, Combat and VM
  Move their components, buffers, systems and supporting logic beneath their owning domains.

- [ ] Reorganise Presentation
  Separate Bridge, State, Synchronisation, Pipeline, Binding, Views, Presenters, Registry and Input.

- [ ] Clean up Core, Content and Game
  Move remaining infrastructure, content pipelines and application bootstrapping into the correct domains.

- [ ] Isolate development and testing assets
  Separate obsolete harnesses, test animations, Timeline assets and diagnostics from production code.

- [ ] Create the documentation structure
  Add Docs inside the State Archeus Game Git repository, alongside Assets, Packages and ProjectSettings.

- [ ] Write initial architecture documentation
  Start with Overview.md, Simulation.md and Presentation.md, describing responsibilities, pipelines and invariants.

- [ ] Verify compilation and Unity references
  Check compilation, ECS system registration, serialized references, .meta files and available tests.

- [ ] Commit the completed reorganisation
  Create a clean Git checkpoint before resuming Drawing UI development.