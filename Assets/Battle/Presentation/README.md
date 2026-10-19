# Documentation

This folder contains all battle presentation related assets, grouped into respective subfolders based on their roles:

Assets - Contains all battle presentation assets and asset models, such as generic battle animations and timelines.

Bridge - Contains a reference to the presentation bridge, which provides a platform for other worlds and code areas to communicate with the battle presentation.

Input - Handles all battle presentation inputs. 

Registry - Provides references to the battle presentation registries, containing entries for presentation recipes and assets.

Scheduling - Manages system ordering for battle presentation systems.

SequencePipeline - Provides all of the assets related to the Sequence/Command pipeline, that presents exported simulation facts with different methods.

StatePipeline - Provides all of the assets related to the State pipeline, that presents an authoritative copy of the current simulated battle state, with recovery systems.

WorldImports - Handles all systems and classes that deal with importing resources from other worlds specifically, like the BattleSimulationWorld.