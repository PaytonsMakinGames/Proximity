# Level Rewards System Setup Guide

## ✅ Code Files Created
All scripts have been created in `Assets/Scripts/LevelRewards/`:
- `Reward.cs` - Abstract base class for all rewards
- `PowerupUnlockReward.cs` - Grants powerups
- `ActionUnlockReward.cs` - Unlocks actions
- `CosmeticReward.cs` - Grants cosmetic items
- `LevelRewardEntry.cs` - Serializable level→rewards mapping
- `LevelRewardDatabase.cs` - Scriptable Object asset
- `LevelRewardManager.cs` - The orchestrator MonoBehaviour

**Plus:** Updated `ActionDetector.cs` with unlock tracking methods.

---

## 🎯 Step-by-Step Setup

### Step 1: Create the Reward Database Asset
1. In Unity Editor, right-click in your `Assets/` folder (or preferred location)
2. Select **Create > Game > Level Rewards > Level Reward Database**
3. Name it `LevelRewardDatabase` (or whatever you prefer)
4. Select it to open in the Inspector

### Step 2: Generate Level Entries
1. With the database selected, scroll down to **Context Menu** area in Inspector
2. Right-click on the asset and select **Generate Empty Entries (1-40)** for v1
   - Or **Generate Empty Entries (1-99)** if you want all 99 levels now
3. This populates the database with empty reward lists for each level

### Step 3: Add LevelRewardManager to Your Scene
1. Find your main manager GameObject (where XpManager lives)
2. Add a new script component: **LevelRewardManager**
3. In the Inspector, assign:
   - **Xp**: Drag your XpManager object
   - **Reward Database**: Drag the LevelRewardDatabase asset you created
   - **Debug Log**: Check this box (shows console logs when rewards are granted)

### Step 4: Remove Old LevelRewardGiver Components
1. Delete all those individual `LevelRewardGiver` components you had attached to various objects
2. The new system handles everything with just one manager!

### Step 5: Configure Rewards for Each Level
1. Select your **LevelRewardDatabase** asset
2. In the Inspector, expand the **Level Rewards** list
3. For each level you want to configure:
   - Click the level entry to expand it
   - Click **+** on the **Rewards** list to add items
   - Create new reward assets and drag them in, OR drag existing ones

#### Creating New Reward Assets
For **Powerup Unlocks**:
1. Right-click in Assets → **Create > Game > Level Rewards > Powerup Unlock**
2. Set **Powerup Id** to one of: `landing_amplifier`, `insurance`, `sticky_ball`, `hot_spot`, `overtime`
3. Name it clearly: `Reward_StickyBall_Level5`

For **Action Unlocks**:
1. Right-click in Assets → **Create > Game > Level Rewards > Action Unlock**
2. Set **Action Id** to one of: `WallFrenzy`, `QuickCatch`, `Greed`, `Desperation`, `EdgeCase`
3. Name it: `Reward_WallFrenzy_Level3`

For **Cosmetics**:
1. Right-click in Assets → **Create > Game > Level Rewards > Cosmetic Item**
2. Set **Item Id** to match an ItemDef.id from your ItemDatabase (your ball skins, trails, etc.)
3. Name it: `Reward_CoolTrail_Level10`

### Step 6: Example Configuration for Level 1-5

**Level 1:**
- ActionUnlockReward (Action: "WallFrenzy")

**Level 2:**
- PowerupUnlockReward (Powerup: "sticky_ball")

**Level 3:**
- CosmeticReward (Item: "trail_blue") — adjust ID to match your actual trails
- ActionUnlockReward (Action: "QuickCatch")

**Level 5:**
- PowerupUnlockReward (Powerup: "landing_amplifier")
- CosmeticReward (Item: "ball_skin_gold")

---

## 🔑 Key Powerup IDs (from PowerupManager.cs)
- `landing_amplifier`
- `insurance`
- `sticky_ball`
- `hot_spot`
- `overtime`

## 🔑 Key Action IDs (from ActionDetector.cs)
- `WallFrenzy`
- `QuickCatch`
- `Greed`
- `Desperation`
- `EdgeCase`

---

## 🧪 Testing

### In-Game Testing
1. Play the game
2. Check the console (Debug Log should be enabled)
3. Level up and watch the console for:
   ```
   [LevelRewards] Level 5 - Granted: Powerup: sticky_ball
   [LevelRewards] Level 5 - Granted: Cosmetic: trail_blue
   [LevelRewards] === Level 5 Complete (2 rewards) ===
   ```

### Manual Testing
1. Select your **LevelRewardManager** in the scene
2. In the Inspector, find the **Test Grant Level** method
3. Type a level number and click it to manually grant that level's rewards
4. This is useful for testing without needing to level up

---

## ⚡ Future: Reward Screen UI

The system fires an event when rewards are granted:
```csharp
LevelRewardManager.OnLevelUpRewardsGranted(levelNumber, rewardsList)
```

When you build the level-up popup screen, subscribe to this event and display the rewards!

---

## 📋 Common Questions

**Q: Can I add a reward after the game is released?**
A: Yes! Just create a new reward asset and add it to the database. The system will handle it automatically on next level-up.

**Q: What if I want to add a new powerup type?**
A: Create the powerup in PowerupManager, add it to the PowerupDatabase, then create PowerupUnlockReward assets for it.

**Q: What if a player levels up but the reward is null?**
A: You'll see a warning in the console. Check that your reward asset is properly assigned in the database.

**Q: Can I grant multiple rewards on one level?**
A: Yes! Just add multiple reward assets to that level's list. The console will show all of them.

**Q: What happens if I configure the same reward for multiple levels?**
A: Nothing special—if a player reaches that level, they get it. If they already own it (for cosmetics), it just logs that they already have it.

---

## 🎨 Inspector Workflow Summary

```
LevelRewardDatabase Asset
├── Level Rewards
│   ├── [0] Level: 1
│   │   └── Rewards
│   │       └── [0] Reward_WallFrenzy_Level1
│   ├── [1] Level: 2
│   │   └── Rewards
│   │       └── [0] Reward_StickyBall_Level2
│   └── ... (up to 40 or 99)
```

Done! You now have a clean, scalable system. Happy game-making! 🎮
