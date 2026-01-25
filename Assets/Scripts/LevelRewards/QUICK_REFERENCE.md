# Quick Reference: Adding Rewards to Levels

## 1️⃣ Fastest Way to Add a Single Reward

**Goal:** Level 10 grants "Hot Spot" powerup

### Step A: Create the Reward Asset
1. Right-click in Assets → **Create > Game > Level Rewards > Powerup Unlock**
2. Name: `Reward_HotSpot_Level10`
3. Set **Powerup Id**: `hot_spot`

### Step B: Add to Database
1. Open **LevelRewardDatabase** asset
2. Find Level 10 in the list
3. Drag `Reward_HotSpot_Level10` into the Rewards list for Level 10

**Done!** Player gets Hot Spot at level 10. ✅

---

## 2️⃣ Adding Multiple Rewards to One Level

**Goal:** Level 15 grants 2 things: Overtime powerup + Cool Trail cosmetic

### Create Both Assets
1. `Reward_Overtime_Level15` (PowerupUnlockReward)
   - Powerup Id: `overtime`

2. `Reward_CoolTrail_Level15` (CosmeticReward)
   - Item Id: `trail_cool` (or whatever your trail ID is)

### Add to Level 15
1. Open LevelRewardDatabase
2. Find Level 15
3. Add both rewards to the Rewards list
4. Result:
   ```
   Level 15
   └── Rewards
       ├── [0] Reward_Overtime_Level15
       └── [1] Reward_CoolTrail_Level15
   ```

**Console output when player reaches level 15:**
```
[LevelRewards] Level 15 - Granted: Powerup: overtime
[LevelRewards] Level 15 - Granted: Cosmetic: trail_cool
[LevelRewards] === Level 15 Complete (2 rewards) ===
```

---

## 3️⃣ Bulk-Creating Rewards (Faster for Many Levels)

If you're configuring all 40 levels at once:

### 1. Create All Reward Assets First
- Open a folder for rewards: `Assets/LevelRewards/Rewards/`
- Create all your reward assets (20 min for 40 levels)
  - Name them clearly: `Reward_NAME_LevelX`
  - Organize by type if you want

### 2. Then Populate Database
- Open LevelRewardDatabase
- Drag-and-drop assets into the level entries

**Tip:** You can reuse the same reward on multiple levels if needed.

---

## 4️⃣ All Available IDs

### Powerup IDs
```
landing_amplifier
insurance
sticky_ball
hot_spot
overtime
```

### Action IDs
```
WallFrenzy
QuickCatch
Greed
Desperation
EdgeCase
```

### Cosmetic IDs
Use your ItemDef IDs from ItemDatabase:
```
ball_skin_gold
ball_skin_silver
trail_blue
trail_fire
(... whatever you have defined ...)
```

---

## 5️⃣ Troubleshooting

| Issue | Fix |
|-------|-----|
| "Level X has no rewards" log | That level has no rewards in the database (it's empty, which is fine) |
| Reward doesn't appear to grant | Check that reward asset has correct ID and manager is assigned in scene |
| Console shows errors about missing inventories | Make sure XpManager, PowerupInventory, PlayerInventory, ActionDetector are in the scene |
| "Already owns item" log | Player already had that cosmetic before level-up (harmless) |

---

## 6️⃣ Example: Configuring Levels 1-5 from Scratch

### Create 6 Reward Assets
1. `Reward_WallFrenzy_Level1` (ActionUnlockReward, Action: "WallFrenzy")
2. `Reward_StickyBall_Level2` (PowerupUnlockReward, Powerup: "sticky_ball")
3. `Reward_QuickCatch_Level3` (ActionUnlockReward, Action: "QuickCatch")
4. `Reward_BlueTrail_Level3` (CosmeticReward, Item: "trail_blue")
5. `Reward_Insurance_Level4` (PowerupUnlockReward, Powerup: "insurance")
6. `Reward_HotSpot_Level5` (PowerupUnlockReward, Powerup: "hot_spot")

### Database Configuration
```
LevelRewardDatabase
├── [0] Level 1
│   └── Rewards: [Reward_WallFrenzy_Level1]
├── [1] Level 2
│   └── Rewards: [Reward_StickyBall_Level2]
├── [2] Level 3
│   └── Rewards: [Reward_QuickCatch_Level3, Reward_BlueTrail_Level3]
├── [3] Level 4
│   └── Rewards: [Reward_Insurance_Level4]
└── [4] Level 5
    └── Rewards: [Reward_HotSpot_Level5]
```

### Console Output
```
[LevelRewards] Level 1 - Granted: Action: WallFrenzy
[LevelRewards] === Level 1 Complete (1 rewards) ===

[LevelRewards] Level 2 - Granted: Powerup: sticky_ball
[LevelRewards] === Level 2 Complete (1 rewards) ===

[LevelRewards] Level 3 - Granted: Action: QuickCatch
[LevelRewards] Level 3 - Granted: Cosmetic: trail_blue
[LevelRewards] === Level 3 Complete (2 rewards) ===

[LevelRewards] Level 4 - Granted: Powerup: insurance
[LevelRewards] === Level 4 Complete (1 rewards) ===

[LevelRewards] Level 5 - Granted: Powerup: hot_spot
[LevelRewards] === Level 5 Complete (1 rewards) ===
```

---

## 7️⃣ Time-Saving Tips

- **Reuse assets:** If you unlock the same powerup on level 5 and level 20, create it once and reference it twice
- **Batch-create:** Create all 40 reward assets at once, then assign them to levels
- **Template names:** Always use `Reward_NAME_LevelX` pattern for quick visual scanning
- **Folder organization:** Group rewards by type or by level range
- **Use context menu:** Right-click LevelRewardDatabase asset and use **Generate Empty Entries** to pre-populate

---

## 8️⃣ Adding a New Reward Type Later

Want to add a new kind of reward (e.g., shop currency)?

1. Create new script: `ShopCurrencyReward : Reward`
2. Implement `Grant()` method
3. Add `[CreateAssetMenu(...)]` attribute
4. **That's it!** Inspector will auto-detect it

No changes needed to LevelRewardManager or database!

---

**Need the full setup? See `SETUP_GUIDE.md`**
