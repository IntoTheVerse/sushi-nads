# Sushi Bonks UI Specifications

## 🎨 Screen Designs for Unity Implementation

---

## 1. Character Minting Screen (First-Time Setup)

### **Purpose**
New player onboarding - mint Chef NFT and set player name

### **Layout Structure**
```
┌─────────────────────────────────────────────────┐
│                                                 │
│              🎨 SUSHI BONKS 🍣                  │
│                                                 │
│         ┌─────────────────────────┐             │
│         │   [Chef Character       │             │
│         │    Preview/Animation]   │             │
│         │                         │             │
│         └─────────────────────────┘             │
│                                                 │
│         CREATE YOUR CHEF                        │
│                                                 │
│         ┌─────────────────────────┐             │
│         │  Enter Chef Name...     │ (Input)    │
│         └─────────────────────────┘             │
│         3-20 characters                         │
│                                                 │
│         ┌─────────────────────────┐             │
│         │    🎯 MINT CHEF NFT     │ (Button)   │
│         │     (FREE - Gas Only)   │             │
│         └─────────────────────────┘             │
│                                                 │
│  💡 This NFT is soulbound (non-transferable)    │
│  ⚡ Start with 0 energy - refill to play!       │
│                                                 │
│         Wallet: 0x1234...5678                   │
│                                                 │
└─────────────────────────────────────────────────┘
```

### **UI Elements**

#### Header
- **Logo/Title**: "SUSHI BONKS" with sushi emoji
- **Subtitle**: "Create Your Chef"

#### Main Content
1. **Chef Preview Area**
   - Animated 3D chef character or static illustration
   - Rotates or shows idle animation
   - Size: ~40% of screen height

2. **Name Input Field**
   - Text Input Box
   - Placeholder: "Enter Chef Name..."
   - Character counter: "X/20"
   - Validation hint below: "3-20 characters"
   - Error state (red) if invalid

3. **Mint Button**
   - Large, prominent button
   - Text: "🎯 MINT CHEF NFT"
   - Subtext: "(FREE - Gas Only)"
   - States:
     - Default: Bright, inviting color
     - Disabled: Grayed out (if name invalid)
     - Loading: Spinner + "Minting..."
     - Success: Green checkmark + "Minted!"

4. **Info Section**
   - Icon + Text rows:
     - 💡 "This NFT is soulbound (non-transferable)"
     - ⚡ "Start with 0 energy - refill to play!"

5. **Wallet Display**
   - Small text at bottom
   - "Wallet: 0x1234...5678"
   - Copy button icon next to it

### **States & Interactions**

| State | UI Behavior |
|-------|-------------|
| Loading | Show loading spinner overlay |
| Name Invalid | Red border on input, disable button |
| Minting | Button shows spinner, disable input |
| Success | Show success animation → Navigate to Profile |
| Error | Show error toast, re-enable button |

---

## 2. Leaderboard Screen

### **Layout Structure**
```
┌─────────────────────────────────────────────────┐
│  🏆 LEADERBOARD          Season 1      [Filter] │
│─────────────────────────────────────────────────│
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │  YOUR RANK                              │   │
│  │  ┌───────────────────────────────────┐  │   │
│  │  │ #42   PlayerName   12,450 pts  ⭐ │  │   │
│  │  └───────────────────────────────────┘  │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │  RANK  PLAYER         SCORE      DATE   │   │
│  ├─────────────────────────────────────────┤   │
│  │  🥇 1   ChefMaster    25,890    2d ago  │   │
│  │  🥈 2   SushiKing     23,450    1d ago  │   │
│  │  🥉 3   RamenLord     21,200    3h ago  │   │
│  │  4      FoodieNinja   18,750    5h ago  │   │
│  │  5      CookWarrior   17,890    1d ago  │   │
│  │  ...                                    │   │
│  │  42     PlayerName    12,450    30m ago │   │ ← Highlighted
│  │  ...                                    │   │
│  │  100    Beginner123    1,200    1w ago  │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  ◄ Prev    Page 1 of 10    Next ►              │
│                                                 │
│  [🔄 Refresh]  [📊 My Stats]  [🎮 Play]        │
└─────────────────────────────────────────────────┘
```

### **UI Elements**

#### Header Bar
1. **Title**: "🏆 LEADERBOARD"
2. **Season Display**: "Season 1" (dynamic)
3. **Filter Button**: Dropdown/modal for filters
   - All Time / This Week / Today
   - Top 10 / Top 50 / Top 100

#### Your Rank Card (Highlighted)
- **Background**: Distinct color (e.g., gold/blue gradient)
- **Layout**: Rank # | Player Name | Score | Star icon
- **Position**: Fixed at top, always visible
- **Data Fields**:
  - `rank`: number
  - `playerName`: string
  - `score`: number (formatted with commas)
  - Special badge if top 10

#### Leaderboard Table
1. **Header Row**
   - Columns: RANK | PLAYER | SCORE | DATE
   - Sticky header (stays on scroll)

2. **Data Rows**
   - **Rank Column**:
     - Top 3: Medal emojis (🥇🥈🥉)
     - 4+: Number only
     - Width: 60px
   
   - **Player Column**:
     - Chef name (truncate if > 15 chars)
     - Wallet address on hover (0x...1234)
     - Width: 40%
   
   - **Score Column**:
     - Formatted number (12,450)
     - Right-aligned
     - Width: 30%
   
   - **Date Column**:
     - Relative time ("2d ago", "3h ago")
     - Width: 20%

3. **Current Player Highlight**
   - Row with your rank has different background
   - Scroll to center on this row on load

4. **Scroll Container**
   - Virtual scrolling for performance (100+ items)
   - Max height: 60% of screen
   - Smooth scroll

#### Pagination Controls
- **Previous Button**: "◄ Prev"
- **Page Indicator**: "Page X of Y"
- **Next Button**: "Next ►"
- Shows 20 players per page

#### Bottom Action Bar
1. **Refresh Button**: 🔄 Refresh
   - Reloads leaderboard data
   - Shows loading spinner
   
2. **My Stats Button**: 📊 My Stats
   - Opens Player Profile screen
   
3. **Play Button**: 🎮 Play
   - Returns to game/main menu

### **States & Interactions**

| State | UI Behavior |
|-------|-------------|
| Loading | Skeleton loaders for rows |
| No Data | "No rankings yet" message |
| Player Not Ranked | "You haven't played yet!" in Your Rank card |
| Refreshing | Spinner on refresh button |
| Error | Error toast + retry button |

### **Data Requirements**

```javascript
// API Call
GET /api/leaderboard/top?limit=100&season=1

// Response
{
  rankings: [
    {
      rank: 1,
      player: "0x1234...",
      playerName: "ChefMaster",
      score: 25890,
      timestamp: "2025-01-20T10:30:00Z",
      sessionId: 42
    },
    // ...
  ],
  currentPlayer: {
    rank: 42,
    score: 12450,
    // ...
  },
  totalPlayers: 1000,
  season: 1
}
```

---

## 3. Player Profile Screen

### **Layout Structure**
```
┌─────────────────────────────────────────────────┐
│  ← Back                        PLAYER PROFILE   │
│─────────────────────────────────────────────────│
│                                                 │
│  ┌──────────────┐                              │
│  │   [Chef      │   ChefMaster                 │
│  │    Avatar]   │   Chef ID: #1234             │
│  │              │   0x1234...5678 [📋 Copy]    │
│  └──────────────┘                              │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │  ⚡ ENERGY STATUS                       │   │
│  │  ┌─────────────────────────────────┐    │   │
│  │  │ ████████░░░░░░░░░░  250/300    │    │   │
│  │  └─────────────────────────────────┘    │   │
│  │  [💰 REFILL ENERGY - 0.1 USDC]         │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │  📊 GLOBAL STATISTICS                   │   │
│  │  ─────────────────────────────────────  │   │
│  │                                         │   │
│  │  🎮 Total Sessions Played:      42      │   │
│  │  📈 Total Moves Made:        1,234      │   │
│  │                                         │   │
│  │  🍱 Recipes Spawned:           156      │   │
│  │  ✅ Recipes Delivered:         89       │   │
│  │  ❌ Recipes Discarded:         23       │   │
│  │  📊 Delivery Rate:            79.5%     │   │
│  │                                         │   │
│  │  🔥 Perfectly Cooked:          234      │   │
│  │  🚫 Overcooked Items:           45      │   │
│  │  📊 Cooking Accuracy:         83.9%     │   │
│  │                                         │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │  🏆 PERSONAL RECORDS                    │   │
│  │  ─────────────────────────────────────  │   │
│  │                                         │   │
│  │  Highest Score:           12,450 pts    │   │
│  │  Best Combo Streak:           15x       │   │
│  │  Fastest Completion:      8:34 min      │   │
│  │  Current Rank:              #42         │   │
│  │                                         │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │  📜 RECENT SESSIONS                     │   │
│  │  ─────────────────────────────────────  │   │
│  │  Session #143  | 12,450 pts | 2h ago   │   │
│  │  Session #142  |  9,800 pts | 1d ago   │   │
│  │  Session #141  | 11,200 pts | 2d ago   │   │
│  │  [View All History]                    │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  [🎮 PLAY NOW]  [🏆 LEADERBOARD]  [⚙️ SETTINGS]│
└─────────────────────────────────────────────────┘
```

### **UI Elements**

#### Header
1. **Back Button**: "← Back"
2. **Title**: "PLAYER PROFILE"

#### Profile Card (Top Section)
1. **Chef Avatar**
   - Square/circular image
   - Size: 120x120px
   - Shows Chef NFT character

2. **Player Info**
   - **Name**: Large, bold text
   - **Chef ID**: "#1234" (NFT token ID)
   - **Wallet Address**: "0x1234...5678"
   - **Copy Button**: 📋 icon (copies full address)

#### Energy Status Card
1. **Energy Bar**
   - Progress bar showing current/max energy
   - Color gradient:
     - Green (80-100%)
     - Yellow (30-79%)
     - Red (0-29%)
   - Text: "250/300"

2. **Refill Button**
   - Text: "💰 REFILL ENERGY"
   - Subtext: "0.1 USDC = +300 Energy"
   - States:
     - Default: Blue/purple gradient
     - Loading: Spinner
     - Success: Green flash

#### Global Statistics Card
**Title**: "📊 GLOBAL STATISTICS"

**Session Stats**
- 🎮 Total Sessions Played: `{count}`
- 📈 Total Moves Made: `{count}` (formatted with commas)

**Recipe Stats** (YOUR REQUESTED SECTION)
- 🍱 Recipes Spawned: `{totalRecipesSpawned}`
- ✅ Recipes Delivered: `{totalRecipesDelivered}`
- ❌ Recipes Discarded: `{totalRecipesDiscarded}`
- 📊 Delivery Rate: `{(delivered/spawned*100).toFixed(1)}%`

**Cooking Stats** (YOUR REQUESTED SECTION)
- 🔥 Perfectly Cooked: `{totalWellCooked}`
- 🚫 Overcooked Items: `{totalOverCooked}`
- 📊 Cooking Accuracy: `{(wellCooked/(wellCooked+overCooked)*100).toFixed(1)}%`

**Visual Design**:
- Grid layout (2 columns on wide screens)
- Icon + Label + Value
- Values aligned right
- Subtle dividers between sections

#### Personal Records Card
**Title**: "🏆 PERSONAL RECORDS"

**Records Display**:
- Highest Score: `{maxScore}` pts
- Best Combo Streak: `{maxCombo}x`
- Fastest Completion: `{MM:SS}` min
- Current Rank: `#{rank}`

**Visual Design**:
- Each record on separate row
- Trophy/medal icons for achievements
- Highlight current rank in gold

#### Recent Sessions Card
**Title**: "📜 RECENT SESSIONS"

**Session List** (Last 3):
```
Session #ID  |  Score pts  |  Time ago
────────────────────────────────────
Session #143 | 12,450 pts | 2h ago
Session #142 |  9,800 pts | 1d ago
Session #141 | 11,200 pts | 2d ago
```

**View All Button**:
- "View All History"
- Opens detailed session history modal/screen

#### Bottom Action Bar
1. **Play Now**: 🎮 PLAY NOW
   - Primary action, large button
   - Green/vibrant color
   
2. **Leaderboard**: 🏆 LEADERBOARD
   - Opens leaderboard screen
   
3. **Settings**: ⚙️ SETTINGS
   - Opens settings (update name, etc.)

### **States & Interactions**

| State | UI Behavior |
|-------|-------------|
| Loading | Skeleton loaders for all cards |
| No Sessions | "Play your first game!" message |
| Low Energy | Red energy bar + pulsing refill button |
| Refilling | Show transaction pending toast |
| Error | Error toast with retry option |

### **Data Requirements**

```javascript
// API Call
GET /api/chef/profile

// Response
{
  player: {
    address: "0x1234...5678",
    name: "ChefMaster",
    tokenId: 1234,
    energy: 250,
    maxEnergy: 300
  },
  globalStats: {
    totalSessionsPlayed: 42,
    totalMoves: 1234,
    totalRecipesSpawned: 156,
    totalRecipesDelivered: 89,
    totalRecipesDiscarded: 23,
    totalWellCooked: 234,
    totalOverCooked: 45
  },
  personalRecords: {
    highestScore: 12450,
    bestCombo: 15,
    fastestTime: 514, // seconds
    currentRank: 42
  },
  recentSessions: [
    {
      sessionId: 143,
      score: 12450,
      timestamp: "2025-01-23T08:00:00Z"
    },
    // ... 2 more
  ]
}
```

---

## 4. Common UI Components

### Toast Notifications
```
┌─────────────────────────────────┐
│  ✅ Success!                    │
│  Chef NFT minted successfully   │
└─────────────────────────────────┘

┌─────────────────────────────────┐
│  ⚠️ Warning                     │
│  Low energy! Refill to play     │
└─────────────────────────────────┘

┌─────────────────────────────────┐
│  ❌ Error                       │
│  Transaction failed. Try again  │
└─────────────────────────────────┘
```

**Specifications**:
- Position: Top-right corner
- Duration: 3-5 seconds
- Auto-dismiss or manual close (X button)
- Stack multiple toasts vertically

### Loading States
1. **Skeleton Loaders**
   - Gray animated pulse
   - Match component dimensions
   
2. **Spinners**
   - Circular spinner for buttons
   - Full-screen for page loads

3. **Progress Bars**
   - For transactions (mint, refill)
   - Show percentage/steps

### Confirmation Modals
```
┌──────────────────────────────────────┐
│  Refill Energy?                      │
│  ────────────────────────────────    │
│                                      │
│  Cost: 0.1 USDC                      │
│  You will receive: +300 Energy       │
│                                      │
│  [Cancel]        [Confirm]           │
└──────────────────────────────────────┘
```

---

## 5. Responsive Design Notes

### Mobile (< 768px)
- Stack cards vertically
- Full-width components
- Larger touch targets (min 44x44px)
- Hide some table columns (show rank + score only)

### Tablet (768px - 1024px)
- 2-column grid for stats
- Side-by-side buttons
- Scrollable tables

### Desktop (> 1024px)
- 3-column layouts where appropriate
- Larger preview areas
- Hover states for all interactive elements

---

## 6. Color Palette Suggestions

### Primary Colors
- **Primary**: `#FF6B35` (Orange - sushi theme)
- **Secondary**: `#004E89` (Deep blue)
- **Accent**: `#FFD23F` (Golden yellow)

### Status Colors
- **Success**: `#10B981` (Green)
- **Warning**: `#F59E0B` (Amber)
- **Error**: `#EF4444` (Red)
- **Info**: `#3B82F6` (Blue)

### UI Colors
- **Background**: `#1F2937` (Dark gray)
- **Card BG**: `#374151` (Medium gray)
- **Text Primary**: `#F9FAFB` (Off-white)
- **Text Secondary**: `#9CA3AF` (Light gray)

---

## 7. Typography

### Font Families
- **Headings**: Bold, sans-serif (e.g., "Poppins", "Montserrat")
- **Body**: Regular, sans-serif (e.g., "Inter", "Roboto")
- **Numbers**: Monospace (e.g., "Roboto Mono") for scores/stats

### Font Sizes
- **H1 (Page Title)**: 32px - 40px
- **H2 (Section Title)**: 24px - 28px
- **H3 (Card Title)**: 18px - 20px
- **Body**: 14px - 16px
- **Small**: 12px - 14px
- **Caption**: 10px - 12px

---

## 8. Implementation Checklist

### For Unity Developer

- [ ] Create Character Minting scene
  - [ ] Input field with validation
  - [ ] Mint button with loading states
  - [ ] Success/error feedback

- [ ] Create Leaderboard scene
  - [ ] Scrollable table/list
  - [ ] Player rank highlight
  - [ ] Pagination controls
  - [ ] Refresh functionality

- [ ] Create Player Profile scene
  - [ ] Energy display and refill button
  - [ ] Global stats display (6 key metrics)
  - [ ] Personal records card
  - [ ] Recent sessions list

- [ ] Common Components
  - [ ] Toast notification system
  - [ ] Loading indicators
  - [ ] Confirmation modals
  - [ ] Copy-to-clipboard functionality

- [ ] API Integration Layer
  - [ ] HTTP client setup
  - [ ] Error handling
  - [ ] Data caching
  - [ ] Retry logic

---

## 9. API Integration Guide

### Backend Endpoints Needed

```javascript
// Chef/Profile
POST   /api/chef/mint              // Mint new chef NFT
GET    /api/chef/profile           // Get player profile + global stats
POST   /api/chef/refill-energy     // Refill energy with USDC
PUT    /api/chef/update-name       // Update chef name

// Leaderboard
GET    /api/leaderboard/top        // Get top N players
GET    /api/leaderboard/player-rank // Get current player rank

// Session
GET    /api/session/history        // Get session history
```

### Data Models

```typescript
// Global Stats (FROM BLOCKCHAIN)
interface PlayerGlobalStats {
  totalSessionsPlayed: number;
  totalMoves: number;
  totalRecipesSpawned: number;      // ← YOUR REQUIREMENT
  totalRecipesDelivered: number;    // ← YOUR REQUIREMENT
  totalRecipesDiscarded: number;    // ← YOUR REQUIREMENT
  totalWellCooked: number;          // ← YOUR REQUIREMENT
  totalOverCooked: number;          // ← YOUR REQUIREMENT
}

// Computed in UI
interface ComputedStats {
  deliveryRate: number;     // (delivered / spawned) * 100
  cookingAccuracy: number;  // (wellCooked / (wellCooked + overCooked)) * 100
}
```

---

## 10. Notes for Implementation

### Priority Order
1. **High**: Player Profile (most data-rich)
2. **Medium**: Leaderboard (competitive feature)
3. **Low**: Character Minting (one-time use)

### Testing Checklist
- [ ] Test with 0 energy (disable play button)
- [ ] Test with low energy (warning state)
- [ ] Test with no sessions (empty state)
- [ ] Test with very long names (truncation)
- [ ] Test with large numbers (formatting)
- [ ] Test error states (failed transactions)

### Performance Considerations
- Cache player profile data (5 min)
- Virtual scrolling for leaderboard (100+ items)
- Lazy load session history
- Debounce refill button (prevent double-clicks)

---

**Ready for Unity Development!** 🎮

These specifications should give your Unity developer everything needed to build these three screens. Let me know if you need any clarification or additional details!

