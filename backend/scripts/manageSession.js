// Complete session management CLI tool
import { gameCore } from '../src/services/blockchain.service.js';
import { queueTransaction } from '../src/services/transactionQueue.service.js';

const PLAYER_ADDRESS = '0x5c2c4baaF9b61A46880e08D408201E4b2c945f92'; // replace with your wallet address

// Colors for terminal output
const colors = {
    reset: '\x1b[0m',
    bright: '\x1b[1m',
    green: '\x1b[32m',
    yellow: '\x1b[33m',
    red: '\x1b[31m',
    cyan: '\x1b[36m',
};

function printHelp() {
    console.log(`
${colors.cyan}${colors.bright}Session Management CLI${colors.reset}

${colors.bright}Usage:${colors.reset}
  node scripts/manageSession.js [command] [options]

${colors.bright}Commands:${colors.reset}
  ${colors.green}check${colors.reset} [address]          Check if session is active
  ${colors.green}stats${colors.reset} [address]          Get detailed session stats (active sessions only)
  ${colors.green}end${colors.reset} <sessionId>          Manually end a session
  ${colors.green}history${colors.reset} [address]        Get last 5 sessions for player
  ${colors.green}help${colors.reset}                     Show this help message

${colors.bright}Examples:${colors.reset}
  node scripts/manageSession.js check
  node scripts/manageSession.js stats
  node scripts/manageSession.js end 7
  node scripts/manageSession.js check 0xYourAddress
  node scripts/manageSession.js history

${colors.bright}Default Address:${colors.reset} ${PLAYER_ADDRESS}
`);
}

async function checkSession(playerAddress) {
    try {
        console.log(`\n${colors.cyan}🔍 Checking session status...${colors.reset}\n`);
        
        const sessionId = await gameCore.getCurrentSessionId(playerAddress);
        
        if (sessionId === 0n || sessionId === 0) {
            console.log(`${colors.yellow}❌ No active session${colors.reset}`);
            console.log(`   Player: ${playerAddress}`);
            console.log(`\n💡 Start a new session in the game to see it here!\n`);
            return null;
        }
        
        const session = await gameCore.getSession(sessionId);
        
        console.log('═══════════════════════════════════════════════');
        if (session.isActive) {
            console.log(`${colors.green}    ✅ SESSION ACTIVE${colors.reset}`);
        } else {
            console.log(`${colors.red}    ❌ SESSION ENDED${colors.reset}`);
        }
        console.log('═══════════════════════════════════════════════\n');
        
        console.log(`🎮 Session ID: ${colors.bright}${sessionId}${colors.reset}`);
        console.log(`👤 Player: ${session.player}`);
        console.log(`📊 Status: ${session.isActive ? colors.green + 'ACTIVE ✅' : colors.red + 'ENDED ❌'}${colors.reset}`);
        
        const startTime = new Date(Number(session.startTime) * 1000);
        console.log(`\n⏰ Started: ${startTime.toLocaleString()}`);
        
        if (session.isActive) {
            const now = Math.floor(Date.now() / 1000);
            const elapsed = now - Number(session.startTime);
            const minutes = Math.floor(elapsed / 60);
            const seconds = elapsed % 60;
            console.log(`⏱️  Elapsed: ${minutes}m ${seconds}s`);
        } else {
            const endTime = new Date(Number(session.endTime) * 1000);
            console.log(`⏰ Ended: ${endTime.toLocaleString()}`);
            const duration = Number(session.endTime) - Number(session.startTime);
            console.log(`⏱️  Duration: ${Math.floor(duration / 60)}m ${duration % 60}s`);
        }
        
        console.log('\n═══════════════════════════════════════════════\n');
        
        return { sessionId: Number(sessionId), isActive: session.isActive };
        
    } catch (error) {
        console.error(`${colors.red}❌ Error:${colors.reset}`, error.message);
        throw error;
    }
}

async function getSessionStats(playerAddress) {
    try {
        console.log(`\n${colors.cyan}📊 Fetching session statistics...${colors.reset}\n`);
        
        const sessionId = await gameCore.getCurrentSessionId(playerAddress);
        
        if (sessionId === 0n || sessionId === 0) {
            console.log(`${colors.yellow}❌ No active session found${colors.reset}\n`);
            return null;
        }
        
        const session = await gameCore.getSession(sessionId);
        
        if (!session.isActive) {
            console.log(`${colors.yellow}⚠️  Session ${sessionId} has ended${colors.reset}`);
            console.log(`   Use: node scripts/getSession.js ${sessionId} for full details\n`);
            return null;
        }
        
        console.log('═══════════════════════════════════════════════');
        console.log(`    ${colors.green}LIVE SESSION STATS (${sessionId})${colors.reset}`);
        console.log('═══════════════════════════════════════════════\n');
        
        console.log(`${colors.bright}📈 Gameplay Stats:${colors.reset}`);
        console.log(`   Moves: ${session.moveCount}`);
        console.log(`   Recipes Delivered: ${colors.green}${session.recipesDelivered}${colors.reset} ✅`);
        console.log(`   Recipes Discarded: ${colors.red}${session.recipesDiscarded}${colors.reset} 🗑️`);
        console.log(`   Well Cooked: ${colors.green}${session.wellCookedCount}${colors.reset} 🔥`);
        console.log(`   Over Cooked: ${colors.red}${session.overCookedCount}${colors.reset} 💀`);
        
        console.log(`\n${colors.bright}🎯 Performance:${colors.reset}`);
        console.log(`   Current Streak: ${session.consecutivePerfectDeliveries}`);
        console.log(`   Max Combo: ${session.maxComboAchieved}`);
        
        const totalRecipes = Number(session.recipesDelivered) + Number(session.recipesDiscarded);
        if (totalRecipes > 0) {
            const accuracy = (Number(session.recipesDelivered) / totalRecipes * 100).toFixed(1);
            console.log(`   Delivery Accuracy: ${accuracy}%`);
        }
        
        const startTime = new Date(Number(session.startTime) * 1000);
        const now = Math.floor(Date.now() / 1000);
        const elapsed = now - Number(session.startTime);
        console.log(`\n${colors.bright}⏱️  Time:${colors.reset}`);
        console.log(`   Started: ${startTime.toLocaleString()}`);
        console.log(`   Elapsed: ${Math.floor(elapsed / 60)}m ${elapsed % 60}s`);
        console.log(`   Remaining: ~${Math.max(0, Math.floor((600 - elapsed) / 60))}m ${Math.max(0, (600 - elapsed) % 60)}s`);
        
        console.log('\n═══════════════════════════════════════════════\n');
        
        return session;
        
    } catch (error) {
        console.error(`${colors.red}❌ Error:${colors.reset}`, error.message);
        throw error;
    }
}

async function endSession(sessionId) {
    try {
        console.log(`\n${colors.yellow}⚠️  Manually ending session ${sessionId}...${colors.reset}\n`);
        
        // Get session details first
        const session = await gameCore.getSession(sessionId);
        
        if (!session.isActive) {
            console.log(`${colors.red}❌ Session ${sessionId} is already ended${colors.reset}\n`);
            return;
        }
        
        console.log(`📊 Session Info:`);
        console.log(`   Player: ${session.player}`);
        console.log(`   Recipes Delivered: ${session.recipesDelivered}`);
        console.log(`   Well Cooked: ${session.wellCookedCount}\n`);
        
        console.log(`${colors.yellow}⏳ Sending end transaction...${colors.reset}`);
        
        // End session using transaction queue
        const receipt = await queueTransaction(
            () => gameCore.endGameSession(sessionId, 'MANUAL_CLI'),
            `Manual End Session ${sessionId}`
        );
        
        console.log(`${colors.green}✅ Session ended successfully!${colors.reset}`);
        console.log(`   Transaction: ${receipt.hash}`);
        console.log(`   Block: ${receipt.blockNumber}\n`);
        
        // Get final score
        const updatedSession = await gameCore.getSession(sessionId);
        
        console.log('═══════════════════════════════════════════════');
        console.log(`         FINAL SCORE: ${colors.bright}${updatedSession.finalScore}${colors.reset}`);
        console.log('═══════════════════════════════════════════════\n');
        
        console.log(`🔗 View on Basescan:`);
        console.log(`   https://sepolia.basescan.org/tx/${receipt.hash}\n`);
        
        return receipt;
        
    } catch (error) {
        console.error(`${colors.red}❌ Error:${colors.reset}`, error.message);
        throw error;
    }
}

async function getSessionHistory(playerAddress) {
    try {
        console.log(`\n${colors.cyan}📜 Fetching session history...${colors.reset}\n`);
        
        const sessionIds = await gameCore.getPlayerSessionHistory(playerAddress);
        
        if (sessionIds.length === 0) {
            console.log(`${colors.yellow}No sessions found for ${playerAddress}${colors.reset}\n`);
            return;
        }
        
        console.log(`Found ${sessionIds.length} total sessions\n`);
        console.log('═══════════════════════════════════════════════');
        console.log(`         LAST 5 SESSIONS`);
        console.log('═══════════════════════════════════════════════\n');
        
        // Get last 5 sessions
        const recentIds = sessionIds.slice(-5).reverse();
        
        for (const id of recentIds) {
            const session = await gameCore.getSession(id);
            const status = session.isActive ? colors.green + 'ACTIVE' : colors.yellow + 'ENDED';
            
            console.log(`${colors.bright}Session ${id}:${colors.reset} ${status}${colors.reset}`);
            console.log(`   Score: ${session.finalScore} | Delivered: ${session.recipesDelivered} | Cooked: ${session.wellCookedCount}`);
            console.log('');
        }
        
        console.log('═══════════════════════════════════════════════\n');
        
    } catch (error) {
        console.error(`${colors.red}❌ Error:${colors.reset}`, error.message);
        throw error;
    }
}

// Main CLI handler
async function main() {
    const command = process.argv[2];
    const arg = process.argv[3];
    
    if (!command || command === 'help') {
        printHelp();
        process.exit(0);
    }
    
    try {
        switch (command) {
            case 'check':
                await checkSession(arg || PLAYER_ADDRESS);
                break;
                
            case 'stats':
                await getSessionStats(arg || PLAYER_ADDRESS);
                break;
                
            case 'end':
                if (!arg) {
                    console.log(`${colors.red}❌ Error: Session ID required${colors.reset}`);
                    console.log(`Usage: node scripts/manageSession.js end <sessionId>\n`);
                    process.exit(1);
                }
                await endSession(parseInt(arg));
                break;
                
            case 'history':
                await getSessionHistory(arg || PLAYER_ADDRESS);
                break;
                
            default:
                console.log(`${colors.red}❌ Unknown command: ${command}${colors.reset}\n`);
                printHelp();
                process.exit(1);
        }
        
        process.exit(0);
        
    } catch (error) {
        console.error(`\n${colors.red}Fatal error:${colors.reset}`, error.message);
        process.exit(1);
    }
}

main();