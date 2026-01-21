using UnityEngine;

/// <summary>
/// DEPRECATED - This script has been split into two focused systems:
/// 
/// 1. ActionDetector (Actions/ActionDetector.cs)
///    - Handles action detection logic
///    - Listens to game events
///    - Delegates reward calls
/// 
/// 2. ActionRewarder (Actions/ActionRewarder.cs)
///    - Handles reward distribution
///    - Manages drop tables
///    - Shows popups
///
/// Migration:
/// - Replace [SerializeField] ActionManager actions with ActionDetector
/// - Update component references in scene
/// - See ARCHITECTURE.md for full details
/// 
/// This file is kept for reference only. Delete after migration is complete.
/// </summary>
[System.Obsolete("Use ActionDetector and ActionRewarder instead. See ARCHITECTURE.md", false)]
public class ActionManager_DEPRECATED : MonoBehaviour
{
    // This class is intentionally empty as a deprecation notice.
    // All functionality has been moved to ActionDetector and ActionRewarder.
}
