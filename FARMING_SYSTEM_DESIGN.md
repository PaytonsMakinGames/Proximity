# Proximity Farming Game System - Master Design Doc

**Date Created:** January 26, 2026  
**Status:** Design Phase (Ready for Implementation)  
**Last Updated:** January 26, 2026

---

## Executive Summary

Transform Proximity into an **OSRS-style action farming game** where players:
1. Perform repeatable actions (throw patterns) to trigger drop tables
2. Earn **coins** + **powerups** from drops
3. Farm combo actions for 1/1500 ultra-rare cosmetics
4. Level powerups with coins (expensive, exponential)
5. Use shop for optimization/cosmetics (Grand Exchange model)
6. Watch optional ads for convenience multipliers only

---

## CORE CURRENCY & PROGRESSION

### Currency (SEPARATE systems)

| Currency | Source | Sink | Notes |
|----------|--------|------|-------|
| **XP** | Actions + Scoring | Level up (unlocks actions) | Separate from coins entirely |
| **Coins** | Action drops + Selling powerups | Shop purchases + Powerup leveling | Soft currency, farmable only |

### Economy Math

**Action Coin Drops:**
```
QuickCatch (base): 50 coins + powerup
Wall Frenzy (dynamic): 50-150 coins based on difficulty
Combo actions (1/1500 ultra-rare): 200-500 coins + cosmetic
```

**Powerup Buy/Sell:**
```
Sticky Ball (Common):
  - Buy: 300 coins
  - Sell: 150 coins (50%)
  - Rebuy: 300 coins (same price)

Hot Spot (Uncommon):
  - Buy: 600 coins
  - Sell: 300 coins
  - Rebuy: 600 coins

Encore (Rare):
  - Buy: 1000 coins
  - Sell: 500 coins
  - Rebuy: 1000 coins

Insurance (Rare):
  - Buy: 800 coins
  - Sell: 400 coins
  - Rebuy: 800 coins
```

**Bulk Discount:**
```
Formula: baseCost × quantity × (1 - (0.10 × min(quantity-1, 5)))

Example:
  Buy 1x Sticky: 300 coins
  Buy 5x Sticky: 1350 coins (-10% per item, 5 item cap)
  Buy 10x Sticky: 2700 coins (still -10% cap)
```

---

## ACTION FARMING SYSTEM

### Action Detection & Triggering

**Current Actions (Expand to 20+):**
- Wall Frenzy (30+ wall bounces, dynamic difficulty)
- Quick Catch (catch at 8+ u/s speed)
- Greed (catch on 2nd-to-last throw)
- Desperation (catch on last throw before stop)
- Edge Case (catch within 5% of wall)

**New Core Actions (Always Active):**
- Distance Master (long throw; tune per screen to be reachable)
- Duo Drifter (reach target distance while only touching 2 distinct walls)
- Perfect Chain (5+ consecutive catches; chains persist across runs until drop is awarded)
- Speed Runner (finish run in ≤5 seconds)
- Panic Catch (catch within 1.5s of predicted stop)
- Wall Dribble (hit the same wall 3–4 times in quick succession without touching others)

**Advanced Actions (Unlock via levels):**
- Combo Artist (trigger 3+ actions in one run)
- Last Second+ (harder version of Panic Catch: within 0.75s of stop)

**Combo Actions (Prerequisites required):**
- Hot Spot Heap: Hit 2+ Hot Spots in a single run (Hot Spot used)
- Sticky Specialist: Land a ≥3.75x landing while Sticky Ball is active
- Overtime Mastery: 4+ seconds airtime in a run
- Powerup Synergy slots (add more): Landing Amp Double-Tap (back-to-back edge/corner catches with Landing Amp active); Encore Chain (Encore save + finish run); Insurance Clutch (Insurance triggers on a run-ending catch)

### Action State Management

**Per-Throw Reset:**
```csharp
OnThrowStarted()
  - Reset wall bounce count
  - Reset throw distance
  - Reset bounce pattern tracking
```

**Per-Run Reset:**
```csharp
OnRunStarted()
  - Reset combo prerequisites (Hot Spot used this run?)
  - Reset milestone counters (consecutive actions)
```

**Per-Pickup Reset:**
```csharp
OnPickupHappened()
  - Evaluate all action triggers
  - Award drops for triggered actions
  - Reset combo state if needed
```

### Drop Table Structure

**Per-action drop tables:**
```csharp
[System.Serializable]
public struct DropEntry {
    public string id;           // Powerup or cosmetic ID
    public int weight;          // Rarity weight
}

// Example: Wall Frenzy drops
DropEntry[] wallFrenzyDrops = new[] {
    new DropEntry { id = "sticky_ball", weight = 100 },
    new DropEntry { id = "hot_spot", weight = 30 },
    new DropEntry { id = "overtime", weight = 20 },
    new DropEntry { id = "insurance", weight = 10 },
};

// Ultra-rare overlay (1/1500 chance, separate roll)
UltraRareDrop[] wallFrenzyRares = new[] {
    new UltraRareDrop { 
        id = "prismatic_sticky_skin", 
        displayName = "Prismatic Sticky Ball",
        dropRate = 1f/1500f
    }
};
```

---

## POWERUP LEVELING SYSTEM

### Powerup Level Mechanics

**Concept:**
- Players "own" all levels they've unlocked
- Can equip/downgrade any owned level for FREE
- Each upgrade level increases cost exponentially (per-powerup)
- Buy/sell prices based on highest owned level
- Max level is per-powerup (Insurance max 5, Hot Spot max 3, etc.)

**Per-Powerup Configuration:**
```csharp
[System.Serializable]
public class PowerupDef {
    public string id;
    public string displayName;
    
    // Leveling
    public int maxLevel = 3;
    public int[] levelUpCosts;  // [0] = level 1->2 cost, [1] = 2->3, etc
    // Example: Insurance = [100, 200, 400, 800, 1600] (exponential)
    // Example: Hot Spot = [150, 300, 600] (lower cap, exponential)
    
    // Pricing (based on highest owned level)
    public int baseBuyCost = 300;
    public int GetBuyCost(int highestLevel) {
        // Cost scales with level: Level 1 = 300, Level 2 = 450, Level 3 = 675
        return (int)(baseBuyCost * Mathf.Pow(1.5f, highestLevel - 1));
    }
}
```

### Powerup Instance Tracking

**In PowerupInventory:**
```csharp
[System.Serializable]
public class PowerupInventorySave {
    public List<string> ids = new List<string>();           // Powerup IDs
    public List<int> counts = new List<int>();              // How many copies
    public List<int> highestLevelOwned = new List<int>();   // Highest level per ID
}

// Example:
// ids: ["sticky_ball", "insurance", "hot_spot"]
// counts: [5, 3, 1]
// highestLevelOwned: [2, 3, 1]
```

### Level Progression Per Powerup

**Insurance (Rare):**
```
Level 1 → 2: 100 coins
Level 2 → 3: 200 coins
Level 3 → 4: 400 coins
Level 4 → 5: 800 coins
Max: Level 5

Effect per level:
  Lv1: Guarantees 1.0x landing multiplier
  Lv2: Guarantees 1.5x landing multiplier
  Lv3: Guarantees 2.0x landing multiplier
  Lv4: Guarantees 2.5x landing multiplier
  Lv5: Guarantees 3.0x landing multiplier
```

**Hot Spot (Uncommon):**
```
Level 1 → 2: 80 coins
Level 2 → 3: 160 coins
Max: Level 3

Effect per level:
  Lv1: Spawns zone, 50 distance per hit
  Lv2: Zone lasts longer, 75 distance per hit
  Lv3: Zone is larger, 100 distance per hit
```

**Sticky Ball (Common):**
```
Level 1 → 2: 50 coins
Level 2 → 3: 100 coins
Max: Level 3

Effect per level:
  Lv1: Pin to wall, 30% distance bonus
  Lv2: Pin to wall, 50% distance bonus
  Lv3: Pin to wall, 75% distance bonus
```

**Overtime (Rare):**
```
Level 1 → 2: 120 coins
Level 2 → 3: 240 coins
Max: Level 3

Effect per level:
  Lv1: Accumulates air time, up to +25% multiplier at 3s
  Lv2: Accumulates air time, up to +50% multiplier at 4s
  Lv3: Accumulates air time, up to +100% multiplier at 5s
```

**Encore (Rare):**
```
Level 1 → 2: 150 coins
Max: Level 2

Effect per level:
  Lv1: +1 throw OR revive (once per run)
  Lv2: +2 throws OR revive (once per run, higher success)
```

### Buy/Sell Prices Scaled by Level

**Example: Insurance**
```
Player has no Insurance:
  - Can't sell
  - Buy price: 800 coins (base)

Player unlocks Insurance (owns Level 1):
  - Buy price: 800 coins (no markup yet)
  - Sell price: 400 coins

Player upgrades to Level 2:
  - Buy price: 1200 coins (800 × 1.5)
  - Sell price: 600 coins (50% of buy)

Player upgrades to Level 3:
  - Buy price: 1800 coins (1200 × 1.5)
  - Sell price: 900 coins
```

---

## POWERUP LOADOUT SYSTEM

### Pre-Run Loadout Selection

**Concept:**
- Before each run, player equips 6 powerups from their inventory
- Stash = unlimited stored powerups
- Loadout = active 6 for this run
- Powerups consumed from loadout only

### Data Structure

```csharp
[System.Serializable]
public class LoadoutSlot {
    public string powerupId;
    public int loadedCount;      // How many in this slot
    public int currentLevel;     // Which level to use (1-3)
}

[System.Serializable]
public class PowerupLoadout {
    public LoadoutSlot[] slots = new LoadoutSlot[6];
    public DateTime lastModified;
}
```

### Loadout UI Flow

**Screen 1: Loadout Builder**
```
[Loadout Name: "Hot Spot Combo"]

Slot 1: [Sticky Ball] × 10 [Level 1▼] [Remove]
Slot 2: [Hot Spot] × 3 [Level 2▼] [Remove]
Slot 3: [Overtime] × 5 [Level 1▼] [Remove]
Slot 4: [Empty] [+ Add Powerup]
Slot 5: [Empty] [+ Add Powerup]
Slot 6: [Empty] [+ Add Powerup]

Stash Available:
  Sticky Ball (lv3): 50 copies
  Insurance (lv2): 8 copies
  
[SAVE LOADOUT] [CANCEL]
```

**Screen 2: Select Powerup to Add**
```
Your Powerups:
├─ Sticky Ball (you have 50, level 1-3)
│  └─ [Load 5 at Level 1] [Load 10 at Level 2] [Load...custom]
├─ Insurance (you have 8, level 1-2)
│  └─ [Load 5] [Load custom...]
├─ Hot Spot (you have 3, level 1-2)
└─ [Load 3] [Load custom...]
```

### In-Run Integration

```csharp
// Instead of checking full inventory,
// PowerupManager checks active loadout

public bool HasPowerupInLoadout(string id) {
    return Array.Find(activeLoadout.slots, 
        s => s != null && s.powerupId == id) != null;
}

public int GetLoadoutCount(string id) {
    var slot = Array.Find(activeLoadout.slots, 
        s => s != null && s.powerupId == id);
    return slot?.loadedCount ?? 0;
}

// When consuming:
public bool TryConsumePowerup(string id) {
    var slot = Array.Find(activeLoadout.slots, 
        s => s != null && s.powerupId == id);
    if (slot == null || slot.loadedCount <= 0) return false;
    
    slot.loadedCount--;
    return true;
}
```

---

## ULTRA-RARE DROP SYSTEM (1/1500)

### Trigger Points

**Combo Actions Only:**
```
Wall Frenzy Combo (1/1500):
  → Prismatic Sticky Ball (cosmetic skin)
  → Golden Trail FX
  → Bounce Icon Avatar Frame

Hot Spot Combo (1/1500):
  → Legendary Hot Spot (cosmetic upgrade)
  → Star Burst Popup FX

Overtime Combo (1/1500):
  → Temporal Trail (cosmetic)
  → Time Warp Popup Effect

Sticky Specialist (1/1500):
  → Mirror Ball (cosmetic)
  → Reverse Gravity Popup
```

### Ultra-Rare Definition

```csharp
[System.Serializable]
public class UltraRareDrop {
    public string id;                    // "prismatic_sticky_skin"
    public string displayName;           // "Prismatic Sticky Ball"
    public ItemType type;                // CosmeticSkin
    public string description;
    public float dropRate = 1f / 1500f;
    public string triggerComboAction;    // "WallFrenzyCombo"
    public Sprite icon;
    public Sprite largeArtwork;
    public bool isOneTimeOnly = false;
}
```

### Award Trigger

```csharp
// In ActionRewarder.cs

public void AwardAction(string actionId) {
    var drops = GetDropTable(actionId);
    
    // Normal drop
    string normalId = Roll(drops);
    if (!string.IsNullOrEmpty(normalId)) {
        AwardItem(normalId, showPopup: true);
    }
    
    // ULTRA-RARE roll (combo actions only)
    if (IsComboAction(actionId)) {
        if (Random.value < (1f/1500f)) {
            var rareItem = GetUltraRareForAction(actionId);
            if (rareItem != null) {
                AwardItem(rareItem.id, showPopup: true, isUltraRare: true);
                TriggerFanfare(rareItem);
            }
        }
    }
}

void TriggerFanfare(UltraRareDrop rare) {
    // Screen shake (0.3s)
    cameraShake.Shake(1f, 0.3f);
    
    // Gold popup (3-5s duration)
    popups.PopAtWorldWithExtraOffset(
        ballPos,
        "⭐ ULTRA RARE ⭐",
        goldColor,
        offset: Vector2.zero,
        duration: 5f,
        fontSize: 80
    );
    
    // Item name
    popups.PopAtWorldWithExtraOffset(
        ballPos,
        rare.displayName,
        goldColor,
        offset: new Vector2(0, -80f),
        duration: 5f
    );
    
    // Optional: UI confetti effect
    // Optional: Audio sting
}
```

---

## SHOP SYSTEM

### Shop Manager Architecture

**Responsibilities:**
- Manage buy/sell transactions
- Track coin balance
- Handle bulk discounts
- Generate limited-time rotation
- Track cosmetic unlocks

### Shop Inventory

**Tabs:**

1. **Powerups Tab**
   - List all unlocked powerups
   - Show count, current highest level
   - Buy (bulk option), Sell, Level Up buttons

2. **Cosmetics Tab**
   - Organized by type (ball skins, trails, popups, avatars)
   - Filter: owned, locked, limited
   - Preview on hover

3. **Limited Time Tab**
   - Rotating bundles
   - Countdown timer
   - First-purchase only items

4. **Boosters Tab**
   - 2x Coin Multiplier (5 runs, 300 coins or watch ad)
   - XP Booster (10 runs, 200 coins or watch ad)
   - Future: Event-specific boosters

### Buy Transaction

```csharp
public bool TryBuyCoin(string powerupId, int quantity) {
    var def = GetPowerupDef(powerupId);
    if (!def) return false;
    
    // Calculate cost based on highest level owned
    int highestLevelOwned = GetHighestLevelOwned(powerupId);
    int costPerUnit = def.GetBuyCost(highestLevelOwned);
    int totalCost = CalculateBulkPrice(costPerUnit, quantity);
    
    if (coinBalance < totalCost) {
        ShowError("Not enough coins!");
        return false;
    }
    
    // Spend coins
    SpendCoins(totalCost);
    
    // Add to stash
    inventory.Add(powerupId, quantity);
    
    ShowNotification($"Bought {quantity}x {def.displayName}");
    return true;
}
```

### Sell Transaction

```csharp
public bool TrySellCoin(string powerupId, int quantity) {
    int ownCount = inventory.GetCount(powerupId);
    if (ownCount < quantity) {
        ShowError("Don't have enough!");
        return false;
    }
    
    var def = GetPowerupDef(powerupId);
    int highestLevelOwned = GetHighestLevelOwned(powerupId);
    int costPerUnit = def.GetBuyCost(highestLevelOwned);
    int sellValue = (int)(costPerUnit * 0.5f);
    int totalGain = sellValue * quantity;
    
    // Remove from stash
    inventory.TrySpend(powerupId, quantity);
    
    // Give coins
    AddCoins(totalGain);
    
    ShowNotification($"Sold {quantity}x {def.displayName} for {totalGain} coins");
    return true;
}
```

### Level Up Transaction

```csharp
public bool TryLevelUpPowerup(string powerupId) {
    int currentHighest = GetHighestLevelOwned(powerupId);
    var def = GetPowerupDef(powerupId);
    
    if (currentHighest >= def.maxLevel) {
        ShowError("Already at max level!");
        return false;
    }
    
    int cost = def.levelUpCosts[currentHighest - 1];  // Index from 0
    
    if (coinBalance < cost) {
        ShowError($"Need {cost} coins!");
        return false;
    }
    
    // Spend coins
    SpendCoins(cost);
    
    // Update highest level
    SetHighestLevelOwned(powerupId, currentHighest + 1);
    
    ShowNotification($"{def.displayName} upgraded to Level {currentHighest + 1}!");
    return true;
}
```

---

## AD INTEGRATION

### Ad Placement Rules

**Rule 1: Never directly give progression**
- No coins for watching ads
- No powerups for watching ads
- No XP for watching ads

**Rule 2: Ads enable free convenience multipliers**
- Double coins for 5 runs
- 50% XP boost for 10 runs

**Rule 3: Ads are always optional**
- "Pay coins OR watch ad"
- Never forced

### Ad Placement 1: Post-Run Bonus

```
[Run End Screen]

You earned:
  +50 XP
  +120 coins
  +1 Sticky Ball
  
[CONTINUE] [WATCH AD for +50 coins]

-- OR --

[CONTINUE] [WATCH AD for 2x coins next 5 runs]
```

**Cooldown:** 5 per day, 5 min cooldown between ads

### Ad Placement 2: Booster Shop

```
[Shop > Boosters]

2x Coin Multiplier (5 runs)
├─ 300 coins
└─ OR [WATCH AD]

XP Booster (10 runs)
├─ 200 coins
└─ OR [WATCH AD]
```

### Ad Placement 3: Cosmetic Preview

```
New cosmetic drops in shop:
  [Prismatic Sticky Ball] - New!
  
  [BUY for 500 coins]
  [WATCH AD to preview for 30 mins]
  
After preview expires:
  "Did you like it? [BUY NOW]"
```

### Ad Manager Implementation

```csharp
public class AdManager : MonoBehaviour {
    [SerializeField] int adsPerDay = 5;
    [SerializeField] float adCooldownSeconds = 300f;
    
    int adsWatchedToday = 0;
    float lastAdTime = 0f;
    
    public bool CanWatchAd() {
        float timeSinceLastAd = Time.realtimeSinceStartup - lastAdTime;
        return adsWatchedToday < adsPerDay && timeSinceLastAd > adCooldownSeconds;
    }
    
    public void ShowAd(AdType type, System.Action onComplete) {
        if (!CanWatchAd()) return;
        
        // Show ad through platform (Unity Ads, Google Mobile Ads, etc)
        ShowPlatformAd(() => {
            adsWatchedToday++;
            lastAdTime = Time.realtimeSinceStartup;
            onComplete?.Invoke();
        });
    }
}
```

---

## DATA PERSISTENCE

### Save Files Structure

**PowerupInventorySave** (existing, expand):
```csharp
[System.Serializable]
public class PowerupInventorySave {
    public List<string> ids = new List<string>();
    public List<int> counts = new List<int>();
    public List<int> highestLevelOwned = new List<int>();  // NEW
    public List<int> equippedLevels = new List<int>();     // Current selected level per powerup
}
```

**CoinsManager** (new):
```csharp
[System.Serializable]
public class CoinsSave {
    public long totalCoins = 0;
    public long allTimeCoinsEarned = 0;  // For stats
}
```

**PowerupLoadout** (new):
```csharp
[System.Serializable]
public class LoadoutSave {
    public List<LoadoutSlot[]> savedLoadouts = new List<LoadoutSlot[]>();
    public int activeLoadoutIndex = 0;
}
```

**CosmeticsInventory** (new):
```csharp
[System.Serializable]
public class CosmeticsSave {
    public List<string> unlockedCosmetics = new List<string>();
    public List<string> equippedCosmetics = new List<string>();  // One per type
    public long lastPreviewExpire = 0;
    public string previewingCosmeticId = null;
}
```

---

## UI SCREENS TO BUILD

### 1. Loadout Selection Screen (Pre-Run)
- 6 slot grid
- Drag-drop powerups from stash
- Level selector per slot
- Load/Save buttons
- Quick presets dropdown

### 2. Shop Main Hub
- 4 tabs: Powerups, Cosmetics, Limited, Boosters
- Coin balance display (top)
- Search/filter bar

### 3. Powerup Shop Tab
- Scrollable list of unlocked powerups
- Buy/Sell/Level Up buttons
- Count display, level badge
- Bulk purchase modal

### 4. Cosmetics Shop Tab
- Grid view or list
- Filter: owned, locked, limited
- Preview on click
- Buy button with "or watch ad" option

### 5. Limited Tab
- Countdown timers
- Bundles description
- Limited-time badges

### 6. Boosters Tab
- Card layout
- Coin cost + AD alternative
- Active booster display

### 7. Cosmetics Inventory
- Owned cosmetics grid
- Equip/unequip
- Preview full screen

### 8. Run Summary Screen (Post-Run, expand current)
- Display all drops
- Show coins earned
- "Watch ad" prompt for bonus
- "Watch ad for 2x coins next 5 runs" option

---

## TECHNICAL IMPLEMENTATION ORDER

### Phase 1: Core Currency & Coin System (Week 1-2)
- [ ] Create CoinsManager (tracks balance, add/spend)
- [ ] Integrate coins into ActionRewarder drops
- [ ] Create basic coin display UI
- [ ] Save/load coins

### Phase 2: Powerup Leveling (Week 2-3)
- [ ] Expand PowerupInventory to track highest level owned
- [ ] Create PowerupDefinition costs array per powerup
- [ ] Implement buy/sell/levelup logic in shop manager
- [ ] Create LoadoutSystem
- [ ] Build loadout UI screen

### Phase 3: Shop System (Week 3-4)
- [ ] Create ShopManager (buy/sell/levelup transactions)
- [ ] Build shop UI (tabs, grid, buttons)
- [ ] Integrate powerup pricing by level
- [ ] Integrate cosmetics unlocking
- [ ] Implement bulk purchase logic

### Phase 4: Ultra-Rare Drops (Week 4)
- [ ] Define ultra-rare drop tables per combo
- [ ] Implement 1/1500 roll in ActionRewarder
- [ ] Create fanfare effect (shake, popups, glow)
- [ ] Add cosmetic unlock on ultra-rare

### Phase 5: Ad Integration (Week 4-5)
- [ ] Create AdManager with cooldown/daily limits
- [ ] Add ad options to booster shop
- [ ] Add post-run ad prompts
- [ ] Integrate cosmetic preview ads
- [ ] Track ad impressions for analytics

### Phase 6: Cosmetics System (Week 5)
- [ ] Create CosmeticsInventory manager
- [ ] Implement cosmetic equipping (trail, ball skin, popup)
- [ ] Build cosmetics inventory UI
- [ ] Add preview functionality
- [ ] Implement limited-time cosmetics rotation

### Phase 7: Polish & Testing (Week 6)
- [ ] Economy balance pass (coin costs, drop rates)
- [ ] UI polish (animations, feedback)
- [ ] Load/save testing
- [ ] Action farming loop testing
- [ ] Ad flow testing

---

## KEY DESIGN DECISIONS LOCKED IN

✅ **One soft currency (coins):** Farmable only, no real money  
✅ **Powerup leveling:** Expensive, exponential, per-powerup capped  
✅ **Level flexibility:** Downgrade free, all levels owned  
✅ **Buy/sell prices:** Based on highest level owned  
✅ **Combo ultra-rares:** 1/1500, cosmetics only  
✅ **Loadout system:** 6 pre-run slots, equip from stash  
✅ **Ad philosophy:** Multipliers only, never progression, optional  
✅ **Economy sink:** Powerup leveling (expensive)  
✅ **Economy source:** Action drops + selling  
✅ **Shop purpose:** Optimization + cosmetics (Grand Exchange model)  

---

## QUESTIONS RESOLVED

| Question | Answer |
|----------|--------|
| How expensive are upgrades? | Exponential per powerup (100→200→400→800) |
| Can players downgrade? | Yes, free (still own all levels) |
| Buy/sell at what price? | Scaled by highest level owned |
| What triggers ultra-rares? | Combo actions only (1/1500) |
| What do ultra-rares drop? | Cosmetics only (no power) |
| Ads give what? | Multipliers only (2x coins, +XP), never items |
| Loadout or free-form? | Loadout (6 pre-run slots) |
| One currency or two? | One soft (coins), separate XP |
| Shop tabs? | Powerups, Cosmetics, Limited, Boosters |
| Cosmetic preview duration? | 30 minutes |

---

## NEXT STEPS

1. **Confirm all design points** (user sign-off)
2. **Create data structure files** (save/load skeletons)
3. **Begin Phase 1** (CoinsManager implementation)
4. **Weekly progress check-in** (update this doc with completion)

---

## NOTES & CONSIDERATIONS

- **Economy balance:** May need tuning after playtesting (coin drop rates, powerup costs)
- **Ad revenue:** Tied to watch rate, not progression blocking
- **Scalability:** Design allows easy addition of new powerups/actions/cosmetics
- **Mobile-first:** Touch UI priority (not implemented yet, plan for Phase 6)
- **Future expansion:** Battle pass system ready (placeholder slot in booster tab)

