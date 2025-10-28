// Quick script to fetch session data from blockchain
import { gameCore } from '../src/services/blockchain.service.js';

async function getSessionData(sessionId) {
    try {
        console.log(`\n📊 Fetching Session ${sessionId} data from blockchain...\n`);
        
        const session = await gameCore.getSession(sessionId);
        
        console.log('═══════════════════════════════════════════════');
        console.log(`           SESSION ${sessionId} DETAILS           `);
        console.log('═══════════════════════════════════════════════\n');
        
        console.log('🎮 Session Info:');
        console.log(`   Session ID: ${session.sessionId}`);
        console.log(`   Player: ${session.player}`);
        console.log(`   Active: ${session.isActive ? '✅ Yes' : '❌ No'}`);
        console.log(`   Score Finalized: ${session.scoreFinalized ? '✅ Yes' : '❌ No'}\n`);
        
        console.log('⏱️  Timing:');
        const startTime = new Date(Number(session.startTime) * 1000);
        const endTime = Number(session.endTime) > 0 ? new Date(Number(session.endTime) * 1000) : null;
        const duration = endTime ? (Number(session.endTime) - Number(session.startTime)) : 0;
        
        console.log(`   Start Time: ${startTime.toLocaleString()}`);
        console.log(`   End Time: ${endTime ? endTime.toLocaleString() : 'Still active'}`);
        console.log(`   Duration: ${duration > 0 ? `${duration} seconds (${(duration / 60).toFixed(1)} mins)` : 'N/A'}\n`);
        
        console.log('📈 Gameplay Stats:');
        console.log(`   Moves: ${session.moveCount}`);
        console.log(`   Recipes Spawned: ${session.recipesSpawned}`);
        console.log(`   Recipes Delivered: ${session.recipesDelivered} ✅`);
        console.log(`   Recipes Discarded: ${session.recipesDiscarded} 🗑️`);
        console.log(`   Well Cooked: ${session.wellCookedCount} 🔥`);
        console.log(`   Over Cooked: ${session.overCookedCount} 💀\n`);
        
        console.log('🎯 Combo & Score:');
        console.log(`   Current Streak: ${session.consecutivePerfectDeliveries}`);
        console.log(`   Max Combo: ${session.maxComboAchieved}`);
        console.log(`   Final Score: ${session.finalScore}\n`);
        
        // Calculate efficiency
        const totalRecipes = Number(session.recipesDelivered) + Number(session.recipesDiscarded);
        const efficiency = totalRecipes > 0 ? (Number(session.recipesDelivered) / totalRecipes * 100) : 0;
        
        console.log('📊 Performance:');
        console.log(`   Delivery Accuracy: ${efficiency.toFixed(1)}%`);
        if (efficiency >= 90) {
            console.log(`   Efficiency Bonus: ×1.2 (90%+ accuracy) 🌟`);
        } else if (efficiency >= 75) {
            console.log(`   Efficiency Bonus: ×1.1 (75%+ accuracy) ⭐`);
        } else {
            console.log(`   Efficiency Bonus: ×1.0 (no bonus)`);
        }
        
        // Calculate combo bonus
        let comboBonus = 0;
        const maxCombo = Number(session.maxComboAchieved);
        if (maxCombo >= 10) comboBonus = 500;
        else if (maxCombo >= 7) comboBonus = 300;
        else if (maxCombo >= 5) comboBonus = 150;
        else if (maxCombo >= 3) comboBonus = 50;
        
        if (comboBonus > 0) {
            console.log(`   Combo Bonus: +${comboBonus} points (${maxCombo} streak) 🔥\n`);
        } else {
            console.log(`   Combo Bonus: None\n`);
        }
        
        console.log('═══════════════════════════════════════════════\n');
        
        // Export as JSON too
        const sessionData = {
            sessionId: Number(session.sessionId),
            player: session.player,
            isActive: session.isActive,
            startTime: Number(session.startTime),
            endTime: Number(session.endTime),
            duration: duration,
            moveCount: Number(session.moveCount),
            recipesSpawned: Number(session.recipesSpawned),
            recipesDelivered: Number(session.recipesDelivered),
            recipesDiscarded: Number(session.recipesDiscarded),
            wellCookedCount: Number(session.wellCookedCount),
            overCookedCount: Number(session.overCookedCount),
            consecutivePerfectDeliveries: Number(session.consecutivePerfectDeliveries),
            maxComboAchieved: Number(session.maxComboAchieved),
            finalScore: Number(session.finalScore),
            scoreFinalized: session.scoreFinalized,
            efficiency: efficiency,
            comboBonus: comboBonus
        };
        
        console.log('📋 JSON Export:');
        console.log(JSON.stringify(sessionData, null, 2));
        console.log('\n');
        
        return sessionData;
        
    } catch (error) {
        console.error(`❌ Error fetching session ${sessionId}:`, error.message);
        process.exit(1);
    }
}

// Get session ID from command line with validation
let arg = process.argv[2];
let sessionId;

if (arg === undefined) {
    sessionId = 5;
} else {
    // Support decimal (and guard against accidental strings like "node")
    const parsed = Number(arg);
    if (!Number.isFinite(parsed) || Number.isNaN(parsed) || parsed < 0) {
        console.error(`❌ Invalid session id: ${arg}. Please provide a positive integer (e.g., 1).`);
        process.exit(1);
    }
    sessionId = Math.floor(parsed);
}

getSessionData(sessionId).then(() => process.exit(0));

