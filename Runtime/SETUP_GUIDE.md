# Graph System - Quick Setup Guide

## Initial Setup

### Step 1: Create GraphConfig
1. In Project window, right-click in Assets/ScriptableObject folder (or any folder)
2. Select: Create → Configuration → Graph
3. Name it "GraphConfig" (or your preferred name)
4. Adjust parameters if needed:
   - **DissolveThreshold**: Controls edge simplification (default: 45°)
   - **VertexBlendDistance**: How close vertices need to be to merge (default: 0.1)
   - **EdgeBlendDistance**: For edge overlap detection (default: 0.05)

### Step 2: Create Graph Manager
1. Create an empty GameObject in your scene
2. Add Component → Graph → GraphSystem
3. Assign your GraphConfig to the "Graph Config" field

### Step 3: Set Up Furniture Waypoints
For each furniture piece or obstacle:

1. Create empty GameObjects at key boundary points
   - Position them around the perimeter of your furniture
   - These will become navigation nodes

2. Create a GameObject to hold the entry point
   - Add Component → Graph → GraphEntry
   - In the inspector, set "Graph Node Points" array size
   - Drag your waypoint GameObjects into the array (in order around the perimeter)
   - Toggle "Draw Debug Visualization" to see the waypoints in yellow

### Step 4: Build the Graph
1. Select the GraphSystem GameObject
2. In Inspector, click "Build Graph" button
3. The system will:
   - Create nodes from all GraphEntry waypoints
   - Blend nearby vertices from different furniture
   - Establish connections following the rules
   - Simplify the graph based on angle threshold
   - Ensure all nodes are connected

### Step 5: Visualize
- Enable "Draw Debug Visualization" on the Graph component
- In Scene view, you should see:
  - **Blue spheres**: Navigation nodes
  - **Green lines**: Direct connections between nodes
  - **Red lines**: Furniture boundaries (negative zones)

---

## Usage in Code

```csharp
using Graph;

// Get all nodes
var allNodes = Graph.Instance.AllNodes;

// Get neighbors of a node
foreach (var neighbor in someNode.Neighbors)
{
    // Use for pathfinding
    float distance = someNode.GetDistance(neighbor);
}

// Check direct connection
if (nodeA.HasEdgeTo(nodeB))
{
    // These nodes are directly connected
}
```

---

## Tips

- **Furniture Boundaries**: Arrange waypoints in counter-clockwise order for best results
- **Overlapping Furniture**: If pieces overlap, the system will automatically blend vertices
- **Multiple Scenes**: Each scene needs its own Graph manager
- **Debugging**: Use yellow visualization on GraphEntry and blue/green/red on Graph to understand connectivity
- **Rebuild**: Call BuildGraph() again if you modify furniture positions or add new entries

---

## Troubleshooting

**No nodes appear?**
- Check that GraphEntry components have waypoints assigned
- Verify waypoints have valid Transform references
- Check console for error messages

**Graph looks disconnected?**
- BuildGraph automatically ensures connectivity
- If still disconnected, increase VertexBlendDistance to merge more vertices
- Check that furniture pieces aren't too far apart

**Too many edges?**
- Reduce DissolveThreshold to remove more edges
- Decrease angle threshold for more aggressive simplification

**Vertices overlap strangely?**
- Adjust VertexBlendDistance to control merging threshold
- Manually adjust furniture positions if too close together

