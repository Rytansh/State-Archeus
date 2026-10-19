# Documentation

This folder contains the top level heirarchy for all battle simulation related assets, containing the following subfolders:

CombatEngine - Contains all assets concerning combat logic, gameplay flow and participation. 

EventEngine - Handles all event processing, including custom VM calls and behavioural handling. 

Identity - Contains assets that identify the overarching battle for reference by other systems.

Input - Handles retriving top level input commands and routing them to the correct systems to request changes to simulated state.

Randomness - Creates randomness via deterministic RNG and exposes a service for other simulation systems to use.

Scheduling - Manages scheduling and timing of battle simulation systems.

Setup - Initialises battle components and manages setup of the battle systems and assets involved with the battle. 

WorldExports - Handles export of information to other worlds specifically.