# Documentation

This folder contains all assets related to the state presentation pipeline, responsible for showing the player authoritative battle state:

Binding - Connects the underlying system updates for different components to the battle UI view that presents the information to the player.

Recovery - Handles recovery of all updated states in order to converge towards current simulation states in case of asynchronisation.

States - Contains all states and updates that define metadata exported from the simulation.

Views - Represents the top level views (MonoBehaviours) that define all of the components that players will see.
