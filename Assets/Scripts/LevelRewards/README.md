# Level Rewards System - Architecture Summary

## What Was Built

A complete, scalable level rewards system that replaces your old `LevelRewardGiver` components.

### The Problem (Before)
- 5 separate components on one GameObject
- Hard to add new rewards
- Difficult to manage 40+ levels
- No way to easily track which rewards have been granted

### The Solution (After)
- **One database asset** that holds all levels 1-99
- **Polymorphic reward system** where each reward type knows how to grant itself
- **Simple inspector editing** — no code changes needed to add rewards
- **Automatic tracking** — rewards granted once per level

---

## Architecture Overview

```
XpManager (fires OnLevelUp event)
    ↓
LevelRewardManager (listens & orchestrates)
    ↓ queries
LevelRewardDatabase (Scriptable Object with Level→Rewards mapping)
    ↓ calls Grant() on each
Reward subclasses:
    ├── PowerupUnlockReward → PowerupInventory.Add()
    ├── ActionUnlockReward → ActionDetector.UnlockAction()
    └── CosmeticReward → PlayerInventory.Grant()
```

---

## File Structure

```
Assets/Scripts/LevelRewards/
├── Reward.cs (abstract base)
├── PowerupUnlockReward.cs
├── ActionUnlockReward.cs
├── CosmeticReward.cs
├── LevelRewardEntry.cs (serializable mapping)
├── LevelRewardDatabase.cs (Scriptable Object)
├── LevelRewardManager.cs (MonoBehaviour)
└── SETUP_GUIDE.md (detailed instructions)
```

---

## Key Design Patterns

### 1. Polymorphism
Each reward type is a subclass of `Reward` and implements `Grant()`:
```csharp
public abstract class Reward : ScriptableObject
{
    public abstract void Grant();
}
```

This means new reward types can be added without changing LevelRewardManager.

### 2. Scriptable Objects
Rewards are data assets (not components), making them:
- Reusable across multiple levels
- Easy to edit in the Inspector
- Serializable and persistent

### 3. Event-Driven
- XpManager fires `OnLevelUp` event
- LevelRewardManager subscribes and queries the database
- No tight coupling between systems

### 4. Lifecycle Tracking
- `processedLevels` HashSet prevents duplicate grants
- Works even if player reconnects mid-session
- Survives app restart (events only fire on level-up)

---

## Supported Reward Types (v1)

| Type | Used For | Method |
|------|----------|--------|
| **PowerupUnlockReward** | Unlock powerups (Sticky Ball, Hot Spot, etc.) | `PowerupInventory.Add()` |
| **ActionUnlockReward** | Unlock actions (Wall Frenzy, Quick Catch, etc.) | `ActionDetector.UnlockAction()` |
| **CosmeticReward** | Grant cosmetics (trails, ball skins) | `PlayerInventory.Grant()` |

---

## Future Extensions (Easy to Add)

**Suggested new reward types:**
- `ShopCurrencyReward` — Grant coins/gems when shop is added
- `UnlockBallTypeReward` — Unlock a new gameplay mode
- `BoostReward` — Grant a temporary buff (XP multiplier, etc.)
- `StoryUnlockReward` — Unlock lore/dialogue

Adding these is just:
1. Create new `YourReward : Reward`
2. Implement `Grant()`
3. Done! Inspector will auto-support it.

---

## Integration Checklist

- [x] Code created in `Assets/Scripts/LevelRewards/`
- [x] `ActionDetector.cs` updated with `UnlockAction()` method
- [ ] Delete old `LevelRewardGiver` components from scene
- [ ] Create `LevelRewardDatabase` asset
- [ ] Add `LevelRewardManager` to your manager GameObject
- [ ] Configure rewards for levels 1-40 (or higher)
- [ ] Test by leveling up in-game

---

## Console Output Example

When a player levels up with debug logs enabled:
```
[LevelRewards] Level 5 - Granted: Powerup: sticky_ball
[LevelRewards] Level 5 - Granted: Cosmetic: trail_blue
[LevelRewards] === Level 5 Complete (2 rewards) ===
```

---

## Inspector Configuration Time Estimate

- Levels 1-10: ~5 min (learn the pattern)
- Levels 11-40: ~2-3 min (familiar workflow)
- **Total for 40 levels**: ~15-20 min

Since rewards are assets, you can also:
- Duplicate existing reward assets and just change the ID
- Reuse the same reward across multiple levels
- Batch-create assets before configuring the database

---

## Notes for Future UI

When you build the level-up popup screen, subscribe to this event:

```csharp
levelRewardManager.OnLevelUpRewardsGranted += (level, rewards) => {
    // Show popup with rewards list
    // rewards is List<Reward> with GetDebugLabel() for display
};
```

The system fires automatically when levels are reached!

---

**Questions?** Check `SETUP_GUIDE.md` for detailed step-by-step instructions.
