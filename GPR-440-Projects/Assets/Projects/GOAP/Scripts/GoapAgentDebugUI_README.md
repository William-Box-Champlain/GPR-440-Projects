# GOAP Agent Debug UI

This package provides a UI element that displays a GOAP agent's current action, beliefs, and goals in real-time. The UI is positioned at a fixed location on the screen and connects to the agent with a line renderer for easy tracking.

## Features

- **Real-time display** of agent's current action, goal, and beliefs
- **Fixed position UI** with consistent readability
- **Line renderer** that connects the UI to the agent in the game world
- **Color-coded beliefs** (green for true, red for false by default)
- **Customizable appearance** including colors, position, and number of beliefs to show
- **Easy to implement** with extension methods and example scripts
- **Automatic creation** of UI elements if not manually assigned

## How to Use

### Method 1: Add Component in Inspector

1. Select your GOAP agent GameObject in the hierarchy
2. Click "Add Component" in the Inspector
3. Search for "GOAP Agent Debug UI" and add it
4. The UI will be automatically created and configured

### Method 2: Use Extension Method in Code

```csharp
// Get reference to your GOAP agent
GoapAgent myAgent = GetComponent<GoapAgent>();

// Add debug UI with extension method
myAgent.AddDebugUI();
```

### Method 3: Use Example Script

1. Add the `GoapAgentDebugUIExample` component to any GameObject
2. Assign your GOAP agent to the "Target Agent" field
3. Configure any custom settings if desired
4. The UI will be added automatically on Start

## Customization

The UI appearance can be customized through the Inspector:

- **UI Position**: Fixed position of the UI panel on the screen
- **Max Beliefs To Show**: Maximum number of beliefs to display
- **True Belief Color**: Color for beliefs that evaluate to true
- **False Belief Color**: Color for beliefs that evaluate to false
- **Line Color**: Color of the line connecting the UI to the agent
- **Line Width**: Width of the connecting line

## Implementation Details

The system consists of three main components:

1. **GoapAgentDebugUI.cs**: The main component that creates and updates the UI and line renderer
2. **GoapAgentDebugUIExtension.cs**: Extension methods for easy implementation
3. **GoapAgentDebugUIExample.cs**: Example usage and helper methods

### UI Structure

The UI displays:
- Current goal name
- Current action name
- List of beliefs with their current state (true/false)

Beliefs are sorted with true beliefs displayed first, and limited to the maximum number specified.

## Tips

- For better visibility, adjust the UI position to a corner of the screen where it doesn't obstruct gameplay elements
- Customize the line color to make it stand out against your game's environment
- If you have multiple agents, you can use `GoapAgentDebugUIExample.AddDebugUIToAllAgents()` to add UI to all agents at once
- The line renderer helps you track which agent the UI belongs to, especially useful in scenes with multiple agents

## Requirements

- Unity 2019.4 or newer
- GOAP system with GoapAgent component
