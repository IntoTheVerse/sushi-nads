import express from 'express';
import { gameCore } from '../services/blockchain.service.js';
import { queueTransaction, sendParallelTransactions } from '../services/transactionQueue.service.js';
import { signer } from '../services/blockchain.service.js';
import { addSessionSignerToWallet, sendSponsoredTransaction } from '../services/privy.service.js';
import { requireAuth } from '../middleware/auth.js';
import { validateSessionId } from '../middleware/validation.js';
import { rateLimitGameActions } from '../middleware/rateLimit.js';
import { BATCH_CONFIG } from '../config/constants.js';
import { clearLeaderboardCache } from '../services/cache.service.js';

const router = express.Router();

// Action queue for batching moves
const actionQueues = new Map();
const queueTimers = new Map();
const flushingQueues = new Set(); // Track which queues are currently being flushed
const FAILED_MOVES = new Map(); // Track failed moves for retry: sessionId -> [moves]
const moveCounters = new Map(); // Track total moves processed per session: sessionId -> { total: number, recorded: number, lastProcessedIds: Set<string> }

function buildSessionStats(session) {
    const recipesDelivered = Number(session.recipesDelivered);
    const recipesDiscarded = Number(session.recipesDiscarded);
    const totalRecipes = recipesDelivered + recipesDiscarded;
    const startTime = Number(session.startTime);
    const endTime = Number(session.endTime);

    return {
        moves: Number(session.moveCount),
        recipesDelivered,
        recipesDiscarded,
        wellCooked: Number(session.wellCookedCount),
        overCooked: Number(session.overCookedCount),
        recipesSpawned: Number(session.recipesSpawned),
        duration: endTime > 0 ? endTime - startTime : 0,
        efficiency: totalRecipes > 0 ? Math.round((recipesDelivered * 100) / totalRecipes) : 0,
        currentStreak: Number(session.consecutivePerfectDeliveries),
        maxCombo: Number(session.maxComboAchieved)
    };
}

/**
 * POST /api/gameplay/move
 * NO-OP: Moves are now tracked locally in the client and sent at session end.
 * Kept for backward compatibility - returns success without recording.
 */
router.post('/move', requireAuth, validateSessionId, rateLimitGameActions, async (req, res, next) => {
    try {
        res.json({ success: true, queued: false, moveCountLocal: true });
    } catch (error) {
        next(error);
    }
});

/**
 * POST /api/gameplay/recipe-spawned
 * Record a recipe spawned
 */
router.post('/recipe-spawned', requireAuth, validateSessionId, async (req, res, next) => {
    try {
        const { sessionId } = req;
        const playerAddress = req.walletAddress;
        
        // Add to action queue - process in order with other events
        if (!actionQueues.has(sessionId)) {
            actionQueues.set(sessionId, []);
        }
        
        const queue = actionQueues.get(sessionId);
        const actionId = `${sessionId}-spawned-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
        queue.push({
            type: 'recipe-spawned',
            id: actionId,
            timestamp: Date.now(),
            player: playerAddress
        });
        
        console.log(`📋 Recipe spawned queued in session ${sessionId} (queue size: ${queue.length})`);
        
        // Trigger flush if needed - non-blocking
        const queueSize = queue.length;
        if (queueSize >= BATCH_CONFIG.maxBatchSize && !flushingQueues.has(sessionId)) {
            setImmediate(() => flushQueue(sessionId));
        } else if (queueSize >= 8 && !flushingQueues.has(sessionId)) {
            setImmediate(() => flushQueue(sessionId));
        } else if (!queueTimers.has(sessionId) && !flushingQueues.has(sessionId)) {
            const timer = setTimeout(() => flushQueue(sessionId), 1500);
            queueTimers.set(sessionId, timer);
        }
        
        res.json({ success: true, queued: true, queueSize: queue.length });
    } catch (error) {
        console.error(`❌ Failed to queue recipe spawn:`, error.message);
        next(error);
    }
});

/**
 * POST /api/gameplay/recipe-delivered
 * Record a recipe successfully delivered
 */
router.post('/recipe-delivered', requireAuth, validateSessionId, async (req, res, next) => {
    try {
        const { sessionId } = req;
        const playerAddress = req.walletAddress;
        
        // Add to action queue - process in order with other events
        if (!actionQueues.has(sessionId)) {
            actionQueues.set(sessionId, []);
        }
        
        const queue = actionQueues.get(sessionId);
        const actionId = `${sessionId}-delivered-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
        queue.push({
            type: 'recipe-delivered',
            id: actionId,
            timestamp: Date.now(),
            player: playerAddress
        });
        
        console.log(`✅ Recipe delivered queued in session ${sessionId} (queue size: ${queue.length})`);
        
        // Trigger flush if needed - NEVER await here to avoid blocking HTTP handler.
        // Blocking causes 26+ requests to pile up; when client times out and sends end-session,
        // those events may not be in the queue yet. Fire-and-forget ensures we can accept all requests.
        const queueSize = queue.length;
        const nonMoveCount = queue.filter(a => a.type !== 'move').length;
        if (queueSize >= BATCH_CONFIG.maxBatchSize && !flushingQueues.has(sessionId)) {
            setImmediate(() => flushQueue(sessionId));
        } else if (queueSize >= 8 && !flushingQueues.has(sessionId)) {
            setImmediate(() => flushQueue(sessionId));
        } else if (nonMoveCount >= 3 && !flushingQueues.has(sessionId)) {
            console.log(`   ⚡ Flushing ${nonMoveCount} scoring events (non-blocking)`);
            setImmediate(() => flushQueue(sessionId));
        } else if (!queueTimers.has(sessionId) && !flushingQueues.has(sessionId)) {
            const timer = setTimeout(() => {
                flushQueue(sessionId);
            }, 1000); // Faster: 1s for recipe-delivered
            queueTimers.set(sessionId, timer);
        }
        
        res.json({ success: true, queued: true, queueSize: queue.length });
    } catch (error) {
        console.error(`❌ Failed to queue recipe delivery:`, error.message);
        next(error);
    }
});

/**
 * POST /api/gameplay/recipe-discarded
 * Record a recipe discarded
 */
router.post('/recipe-discarded', requireAuth, validateSessionId, async (req, res, next) => {
    try {
        const { sessionId } = req;
        const playerAddress = req.walletAddress;
        
        // Add to action queue - process in order with other events
        if (!actionQueues.has(sessionId)) {
            actionQueues.set(sessionId, []);
        }
        
        const queue = actionQueues.get(sessionId);
        const actionId = `${sessionId}-discarded-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
        queue.push({
            type: 'recipe-discarded',
            id: actionId,
            timestamp: Date.now(),
            player: playerAddress
        });
        
        console.log(`🗑️  Recipe discarded queued in session ${sessionId} (queue size: ${queue.length})`);
        
        // Trigger flush if needed - non-blocking (see recipe-delivered comment)
        const queueSize = queue.length;
        const nonMoveCount = queue.filter(a => a.type !== 'move').length;
        if (queueSize >= BATCH_CONFIG.maxBatchSize && !flushingQueues.has(sessionId)) {
            setImmediate(() => flushQueue(sessionId));
        } else if (queueSize >= 8 && !flushingQueues.has(sessionId)) {
            setImmediate(() => flushQueue(sessionId));
        } else if (nonMoveCount >= 3 && !flushingQueues.has(sessionId)) {
            setImmediate(() => flushQueue(sessionId));
        } else if (!queueTimers.has(sessionId) && !flushingQueues.has(sessionId)) {
            const timer = setTimeout(() => flushQueue(sessionId), 1000);
            queueTimers.set(sessionId, timer);
        }
        
        res.json({ success: true, queued: true, queueSize: queue.length });
    } catch (error) {
        console.error(`❌ Failed to queue recipe discard:`, error.message);
        next(error);
    }
});

/**
 * POST /api/gameplay/well-cooked
 * Record a perfectly cooked ingredient
 */
router.post('/well-cooked', requireAuth, validateSessionId, async (req, res, next) => {
    try {
        const { sessionId } = req;
        const playerAddress = req.walletAddress;
        
        // Add to action queue - process in order with other events
        if (!actionQueues.has(sessionId)) {
            actionQueues.set(sessionId, []);
        }
        
        const queue = actionQueues.get(sessionId);
        const actionId = `${sessionId}-well-cooked-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
        queue.push({
            type: 'well-cooked',
            id: actionId,
            timestamp: Date.now(),
            player: playerAddress
        });
        
        console.log(`🔥 Well-cooked ingredient queued in session ${sessionId} (queue size: ${queue.length})`);
        
        // Trigger flush if needed - non-blocking (see recipe-delivered comment)
        const queueSize = queue.length;
        const nonMoveCount = queue.filter(a => a.type !== 'move').length;
        if (queueSize >= BATCH_CONFIG.maxBatchSize && !flushingQueues.has(sessionId)) {
            setImmediate(() => flushQueue(sessionId));
        } else if (queueSize >= 8 && !flushingQueues.has(sessionId)) {
            setImmediate(() => flushQueue(sessionId));
        } else if (nonMoveCount >= 3 && !flushingQueues.has(sessionId)) {
            setImmediate(() => flushQueue(sessionId));
        } else if (!queueTimers.has(sessionId) && !flushingQueues.has(sessionId)) {
            const timer = setTimeout(() => flushQueue(sessionId), 1000);
            queueTimers.set(sessionId, timer);
        }
        
        res.json({ success: true, queued: true, queueSize: queue.length });
    } catch (error) {
        console.error(`❌ Failed to queue well-cooked:`, error.message);
        next(error);
    }
});

/**
 * POST /api/gameplay/over-cooked
 * Record an overcooked ingredient
 */
router.post('/over-cooked', requireAuth, validateSessionId, async (req, res, next) => {
    try {
        const { sessionId } = req;
        const playerAddress = req.walletAddress;
        
        // Add to action queue - process in order with other events
        if (!actionQueues.has(sessionId)) {
            actionQueues.set(sessionId, []);
        }
        
        const queue = actionQueues.get(sessionId);
        const actionId = `${sessionId}-over-cooked-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
        queue.push({
            type: 'over-cooked',
            id: actionId,
            timestamp: Date.now(),
            player: playerAddress
        });
        
        console.log(`💀 Overcooked ingredient queued in session ${sessionId} (queue size: ${queue.length})`);
        
        // Trigger flush if needed - non-blocking
        const queueSize = queue.length;
        const nonMoveCount = queue.filter(a => a.type !== 'move').length;
        if (queueSize >= BATCH_CONFIG.maxBatchSize && !flushingQueues.has(sessionId)) {
            setImmediate(() => flushQueue(sessionId));
        } else if (queueSize >= 8 && !flushingQueues.has(sessionId)) {
            setImmediate(() => flushQueue(sessionId));
        } else if (nonMoveCount >= 3 && !flushingQueues.has(sessionId)) {
            setImmediate(() => flushQueue(sessionId));
        } else if (!queueTimers.has(sessionId) && !flushingQueues.has(sessionId)) {
            const timer = setTimeout(() => flushQueue(sessionId), 1000);
            queueTimers.set(sessionId, timer);
        }
        
        res.json({ success: true, queued: true, queueSize: queue.length });
    } catch (error) {
        console.error(`❌ Failed to queue overcooked:`, error.message);
        next(error);
    }
});

/**
 * GET /api/gameplay/session-stats
 * Get current session statistics
 */
router.get('/session-stats', requireAuth, validateSessionId, async (req, res, next) => {
    try {
        const { sessionId } = req;
        
        const session = await gameCore.getSession(sessionId);
        
        res.json({
            sessionId,
            isActive: session.isActive,
            moveCount: Number(session.moveCount),
            recipesSpawned: Number(session.recipesSpawned),
            recipesDelivered: Number(session.recipesDelivered),
            recipesDiscarded: Number(session.recipesDiscarded),
            wellCookedCount: Number(session.wellCookedCount),
            overCookedCount: Number(session.overCookedCount),
            consecutivePerfectDeliveries: Number(session.consecutivePerfectDeliveries),
            maxComboAchieved: Number(session.maxComboAchieved)
        });
    } catch (error) {
        next(error);
    }
});

/**
 * Retry failed moves for a session
 */
async function retryFailedMoves(sessionId) {
    const failedMoves = FAILED_MOVES.get(sessionId);
    if (!failedMoves || failedMoves.length === 0) {
        return;
    }
    
    // Check if session is still active before retrying
    try {
        const session = await gameCore.getSession(sessionId);
        if (!session.isActive) {
            console.log(`⚠️  Session ${sessionId} is no longer active. Clearing ${failedMoves.length} failed moves.`);
            FAILED_MOVES.delete(sessionId);
            return;
        }
    } catch (error) {
        console.error(`⚠️  Error checking session ${sessionId} status for retry:`, error.message);
        // If we can't check, don't retry to avoid infinite loops
        FAILED_MOVES.delete(sessionId);
        return;
    }
    
    console.log(`🔄 Retrying ${failedMoves.length} failed moves for session ${sessionId}`);
    
    // Add failed moves back to main queue
    if (!actionQueues.has(sessionId)) {
        actionQueues.set(sessionId, []);
    }
    actionQueues.get(sessionId).push(...failedMoves);
    
    // Clear failed moves list
    FAILED_MOVES.delete(sessionId);
    
    // Trigger flush if not already flushing
    if (!flushingQueues.has(sessionId)) {
        await flushQueue(sessionId);
    }
}

/**
 * Execute end-session action
 * @param {number} sessionId The session ID
 * @param {Object} action The end-session action
 * @returns {Object|null} End session result
 */
async function executeEndSession(sessionId, action) {
    try {
        console.log(`🏁 Executing end-session for session ${sessionId} (reason: ${action.reason || 'PLAYER_ENDED'})`);
        
        const moveCount = Math.max(0, action.moveCount ?? 0);
        const receipt = await queueTransaction(
            () => gameCore.endGameSession(sessionId, action.reason || 'PLAYER_ENDED', moveCount),
            `End Session ${sessionId}`
        );
        
        console.log('   ✅ Session ended!');
        
        const updatedSession = await gameCore.getSession(sessionId);
        const sessionStats = buildSessionStats(updatedSession);
        const finalScore = Number(updatedSession.finalScore);
        
        // Store result in action for later retrieval
        action.result = {
            success: true,
            sessionId,
            finalScore,
            txHash: receipt.hash,
            sessionStats
        };
        
        // Leaderboard entries get updated inside GameCore, but our cache might be stale.
        try {
            await clearLeaderboardCache();
        } catch (cacheError) {
            console.warn(`⚠️ Failed to clear leaderboard cache after session ${sessionId}:`, cacheError.message);
        }
        
        return action.result;
    } catch (error) {
        // Check if session was already ended (duplicate call or race condition)
        if (error.reason === 'SessionNotActive' || 
            error.data === '0x27406db9' || 
            error.message?.includes('SessionNotActive') ||
            error.message?.includes('execution reverted')) {
            
            console.log(`   ℹ️  Session ${sessionId} already ended (duplicate call or race condition)`);
            const updatedSession = await gameCore.getSession(sessionId);
            const finalScore = Number(updatedSession.finalScore);
            const sessionStats = buildSessionStats(updatedSession);
            
            action.result = {
                success: true,
                sessionId,
                finalScore,
                message: 'Session was already ended',
                sessionStats
            };
            
            try {
                await clearLeaderboardCache();
            } catch (cacheError) {
                console.warn(`⚠️ Failed to clear leaderboard cache after session ${sessionId}:`, cacheError.message);
            }
            
            return action.result;
        }
        // Re-throw if it's a different error
        throw error;
    }
}

/**
 * Flush queued actions to blockchain
 * Processes all actions in order until encountering "end-session"
 * Executes "end-session" only after all prior actions are complete
 * @param {number} sessionId The session ID to flush
 * @param {boolean} force If true, process actions even if session check fails (for cleanup)
 * @returns {Object|null} Returns end session result if end-session was executed, null otherwise
 */
async function flushQueue(sessionId, force = false) {
    // ATOMIC: Check and set flush lock in one operation to prevent race conditions
    if (flushingQueues.has(sessionId)) {
        console.log(`⏸️  Session ${sessionId} already flushing, skipping...`);
        return null;
    }
    
    // Immediately set the lock BEFORE any async operations
    flushingQueues.add(sessionId);
    
    try {
        const queue = actionQueues.get(sessionId);
        if (!queue || queue.length === 0) {
            flushingQueues.delete(sessionId);
            return null;
        }
        
        // Find where "end-session" action is (if any)
        const endSessionIndex = queue.findIndex(a => a.type === 'end-session');
        
        // Process actions BEFORE end-session (if it exists)
        // IMPORTANT: Include ALL actions before end-session, even if some were added just before flush
        const actionsToProcess = endSessionIndex >= 0 
            ? queue.slice(0, endSessionIndex)  // All actions before end-session
            : queue;  // All actions if no end-session
        
        // Log queue state for debugging
        if (endSessionIndex >= 0) {
            const actionsAfterEndSession = queue.slice(endSessionIndex + 1);
            if (actionsAfterEndSession.length > 0) {
                console.log(`   ⚠️  Found ${actionsAfterEndSession.length} action(s) after end-session in queue: ${actionsAfterEndSession.map(a => a.type).join(', ')}`);
                console.log(`   🔄 These will be processed in a subsequent flush before end-session is executed`);
            }
        }
        
        if (actionsToProcess.length === 0) {
            // Only end-session in queue
            if (endSessionIndex >= 0) {
                const endSessionAction = queue[endSessionIndex];
                // Execute end-session
                const result = await executeEndSession(sessionId, endSessionAction);
                // Remove end-session from queue
                const remaining = queue.filter(a => a.id !== endSessionAction.id);
                if (remaining.length > 0) {
                    actionQueues.set(sessionId, remaining);
                } else {
                    actionQueues.delete(sessionId);
                }
                flushingQueues.delete(sessionId);
                return result;
            }
            flushingQueues.delete(sessionId);
            return null;
        }
        
        // Check if session is still active before processing (unless force mode)
        if (!force) {
            try {
                const session = await gameCore.getSession(sessionId);
                if (!session.isActive) {
                    // Session already ended - clear queue (except end-session which we'll handle)
                    const nonEndActions = queue.filter(a => a.type !== 'end-session');
                    console.log(`⚠️  Session ${sessionId} is no longer active. Clearing ${nonEndActions.length} queued actions.`);
                    if (endSessionIndex >= 0) {
                        // Keep only end-session in queue - it will be handled by clearSessionQueue
                        actionQueues.set(sessionId, [queue[endSessionIndex]]);
                    } else {
                        actionQueues.delete(sessionId);
                    }
                    FAILED_MOVES.delete(sessionId);
                    if (queueTimers.has(sessionId)) {
                        clearTimeout(queueTimers.get(sessionId));
                        queueTimers.delete(sessionId);
                    }
                    flushingQueues.delete(sessionId);
                    return null;
                }
            } catch (error) {
                console.error(`⚠️  Error checking session ${sessionId} status:`, error.message);
                // Continue processing, but be prepared for errors
            }
        } else {
            // Force mode: try to process actions even if session might be ended
            console.log(`   🔄 Force mode: Attempting to process ${actionsToProcess.length} actions (session may be ended)`);
        }
        
        // Process ALL actions in order - every action must be recorded before end-session
        console.log(`📦 Flushing ${actionsToProcess.length} actions for session ${sessionId} (before end-session)`);
        
        // Log all actions for debugging
        const actionTypeCounts = {};
        actionsToProcess.forEach(a => {
            actionTypeCounts[a.type] = (actionTypeCounts[a.type] || 0) + 1;
        });
        const typeSummary = Object.entries(actionTypeCounts).map(([type, count]) => `${count} ${type}`).join(', ');
        console.log(`   📋 Actions to process: ${typeSummary}`);
        
        // Separate moves from other actions for parallel processing
        const moves = actionsToProcess.filter(a => a.type === 'move');
        const otherActions = actionsToProcess.filter(a => a.type !== 'move');
        
        // Log breakdown for debugging
        if (otherActions.length > 0) {
            const actionTypes = otherActions.map(a => a.type);
            console.log(`   📋 Action breakdown: ${moves.length} moves, ${otherActions.length} other actions (${[...new Set(actionTypes)].join(', ')})`);
        }
        
        let successCount = 0;
        const failedActions = [];
        let moveResults = null; // Declare at function scope for tracking later
        
        // Step 1: Process moves in parallel (50-100x speedup for bulk operations)
    if (moves.length > 0) {
            console.log(`🚀 Processing ${moves.length} moves in parallel with proper nonce management...`);
            
            // Create transaction functions for parallel processing
            // Use populateTransaction to get transaction request without sending
            const moveTransactions = moves.map((action, index) => ({
                txFunction: async () => {
                    // In ethers v6, contract methods have populateTransaction property
                    // This creates a transaction request without sending it
                    const populatedTx = await gameCore.recordMove.populateTransaction(sessionId);
                    return populatedTx;
                },
                description: `Move ${index + 1}/${moves.length} (Session ${sessionId})`
            }));
            
            // Send all moves in parallel batches
            moveResults = await sendParallelTransactions(moveTransactions, signer, 50);
            
            // Process results and handle errors
            for (let i = 0; i < moveResults.length; i++) {
                const result = moveResults[i];
                const action = moves[i];
                
                if (result.success) {
                    successCount++;
                } else {
                    // Check for critical errors
                    const error = result.error;
                    const errorData = error?.data || error?.info?.error?.data || '';
                    const errorString = errorData.toString();
                    
                    if (errorString.includes('27406db9') || // SessionNotActive
                        error?.reason === 'SessionNotActive' ||
                        error?.message?.includes('SessionNotActive')) {
                        if (force) {
                            console.error(`⚠️  Session ${sessionId} ended during parallel move processing. ${moves.length - i} moves will not be recorded.`);
                        } else {
                            console.error(`🛑 Session ${sessionId} is no longer active. Stopping move processing.`);
                            // Clear remaining actions from queue (except end-session)
                            const remaining = queue.filter((a, idx) => idx > endSessionIndex || a.type === 'end-session');
                            if (remaining.length > 0) {
                                actionQueues.set(sessionId, remaining);
                            } else {
                                actionQueues.delete(sessionId);
                            }
                            FAILED_MOVES.delete(sessionId);
                            flushingQueues.delete(sessionId);
                            return null;
                        }
                        // Add remaining moves to failed actions
                        failedActions.push(...moves.slice(i));
                        break;
                    }
                    
                    if (errorString.includes('817b679a') || // InsufficientEnergy
                        errorString.includes('fa2fb967') ||
                        error?.reason === 'InsufficientEnergy' ||
                        error?.message?.includes('InsufficientEnergy')) {
                        const remainingMoves = moves.length - i;
                        console.error(`⚡ Insufficient energy during parallel move processing. ${remainingMoves} moves cannot be recorded.`);
                        failedActions.push(...moves.slice(i));
                        break;
                    }
                    
                    // Other errors - add to failed for retry
                    failedActions.push(action);
                }
            }
        }
        
        // Step 2: Process other actions sequentially (there are fewer, and they're more critical)
        if (otherActions.length > 0) {
            console.log(`➡️  Processing ${otherActions.length} other actions sequentially for session ${sessionId}...`);
        }
        
        for (let i = 0; i < otherActions.length; i++) {
            const action = otherActions[i];
            const globalIndex = moves.length + i;
            
            try {
                console.log(`📤 Processing: ${action.type.charAt(0).toUpperCase() + action.type.slice(1).replace(/-/g, ' ')} ${globalIndex + 1}/${actionsToProcess.length} (Session ${sessionId})`);
                let receipt;
                
                switch (action.type) {
                    case 'recipe-delivered':
                        receipt = await queueTransaction(
                            () => gameCore.recordRecipeDelivered(sessionId),
                            `Recipe Delivery ${globalIndex + 1}/${actionsToProcess.length} (Session ${sessionId})`
                        );
                        break;
                    case 'recipe-discarded':
                        receipt = await queueTransaction(
                            () => gameCore.recordRecipeDiscarded(sessionId),
                            `Recipe Discard ${globalIndex + 1}/${actionsToProcess.length} (Session ${sessionId})`
                        );
                        break;
                    case 'well-cooked':
                        receipt = await queueTransaction(
                            () => gameCore.recordWellCooked(sessionId),
                            `Well-Cooked ${globalIndex + 1}/${actionsToProcess.length} (Session ${sessionId})`
                        );
                        break;
                    case 'over-cooked':
                        receipt = await queueTransaction(
                            () => gameCore.recordOverCooked(sessionId),
                            `Overcooked ${globalIndex + 1}/${actionsToProcess.length} (Session ${sessionId})`
                        );
                        break;
                    case 'recipe-spawned':
                        receipt = await queueTransaction(
                            () => gameCore.recordRecipeSpawned(sessionId),
                            `Recipe Spawned ${globalIndex + 1}/${actionsToProcess.length} (Session ${sessionId})`
                        );
                        break;
                    default:
                        console.warn(`⚠️  Unknown action type: ${action.type}, skipping...`);
                        continue;
                }
                
                successCount++;
                
                // Minimal delay between non-move actions
                if (i < otherActions.length - 1) {
                    await new Promise(resolve => setTimeout(resolve, 10));
                }
            } catch (error) {
                const errorData = error.data || error.info?.error?.data || '';
                const errorString = errorData.toString();
                
                // Check for critical errors that mean we should stop processing
                if (errorString.includes('27406db9') || // SessionNotActive
                    error.reason === 'SessionNotActive' ||
                    error.message?.includes('SessionNotActive')) {
                    if (force) {
                        const remainingCount = otherActions.length - i;
                        console.error(`⚠️  Session ${sessionId} ended before action ${globalIndex + 1} (${action.type}) could be recorded. ${remainingCount} actions will not be recorded.`);
                        // Don't add to failed actions - session is ended, can't record these
                        // Clear remaining actions from queue (except end-session)
                        const remaining = queue.filter((a, idx) => idx > endSessionIndex || a.type === 'end-session');
                        if (remaining.length > 0) {
                            actionQueues.set(sessionId, remaining);
                        } else {
                            actionQueues.delete(sessionId);
                        }
                        FAILED_MOVES.delete(sessionId);
                        flushingQueues.delete(sessionId);
                        return null;
                    } else {
                        console.error(`🛑 Session ${sessionId} is no longer active. Stopping action processing.`);
                        // Clear remaining actions from queue (except end-session)
                        const remaining = queue.filter((a, idx) => idx > endSessionIndex || a.type === 'end-session');
                        if (remaining.length > 0) {
                            actionQueues.set(sessionId, remaining);
                        } else {
                            actionQueues.delete(sessionId);
                        }
                        FAILED_MOVES.delete(sessionId);
                        flushingQueues.delete(sessionId);
                        return null;
                    }
                }
                
                // Check for InsufficientEnergy
                if (errorString.includes('817b679a') || // InsufficientEnergy (ChefNFT)
                    errorString.includes('fa2fb967') || // InsufficientEnergy (GameCore) 
                    error.reason === 'InsufficientEnergy' ||
                    error.message?.includes('InsufficientEnergy')) {
                    const remainingActions = otherActions.length - i;
                    console.error(`⚡ Insufficient energy on action ${globalIndex + 1}/${actionsToProcess.length} (${action.type}). Player has no energy left.`);
                    console.error(`   ⚠️  ${remainingActions} actions queued but cannot be recorded (energy consumed by other actions or depleted)`);
                    
                    // Don't add to failed actions - no point retrying without energy
                    // Clear remaining actions from queue (except end-session)
                    const remaining = queue.filter((a, idx) => idx > endSessionIndex || a.type === 'end-session');
                    if (remaining.length > 0) {
                        actionQueues.set(sessionId, remaining);
                        
                        // If end-session is in the remaining queue, try to execute it
                        const remainingEndSession = remaining.find(a => a.type === 'end-session');
                        if (remainingEndSession) {
                            console.log(`   🏁 Energy ran out, but end-session is queued. Executing end-session...`);
                            try {
                                const result = await executeEndSession(sessionId, remainingEndSession);
                                // Remove end-session from queue
                                const finalRemaining = remaining.filter(a => a.id !== remainingEndSession.id);
                                if (finalRemaining.length > 0) {
                                    actionQueues.set(sessionId, finalRemaining);
                                } else {
                                    actionQueues.delete(sessionId);
                                }
                                flushingQueues.delete(sessionId);
                                return result;
                            } catch (endSessionError) {
                                // If end-session fails (e.g., session already ended), that's okay
                                console.error(`   ⚠️  Failed to execute end-session after energy depletion:`, endSessionError.message);
                            }
                        }
                    } else {
                        actionQueues.delete(sessionId);
                    }
                    FAILED_MOVES.delete(sessionId);
                    flushingQueues.delete(sessionId);
                    return null;
                }
                
                // For other errors, track for retry
                failedActions.push(action);
                
                if (error.message.includes('nonce') || error.code === 'NONCE_EXPIRED') {
                    console.error(`⚠️  Nonce error on action ${i + 1} (${action.type}), will retry...`);
                    await new Promise(resolve => setTimeout(resolve, 1000));
                } else {
                    console.error(`❌ Error on action ${i + 1} (${action.type}):`, error.message);
                    await new Promise(resolve => setTimeout(resolve, 500));
                }
            }
            }
            
        console.log(`   ✅ Flushed ${successCount}/${actionsToProcess.length} actions successfully`);
        
        // Track recorded moves (only for move actions)
        // Note: 'moves' variable is already declared above for parallel processing
        if (moves.length > 0 && moveResults) {
            // Map successful move results to their corresponding move actions
            const successfulMoves = [];
            for (let i = 0; i < moveResults.length && i < moves.length; i++) {
                if (moveResults[i].success) {
                    successfulMoves.push(moves[i]);
                }
            }
            
            if (successfulMoves.length > 0) {
                if (!moveCounters.has(sessionId)) {
                    moveCounters.set(sessionId, { total: 0, recorded: 0, lastProcessedIds: new Set() });
                }
                const counter = moveCounters.get(sessionId);
                const moveIds = successfulMoves.map(m => m.id);
                let newRecordedCount = 0;
                for (const moveId of moveIds) {
                    if (!counter.lastProcessedIds.has(moveId)) {
                        counter.lastProcessedIds.add(moveId);
                        newRecordedCount++;
                    }
                }
                counter.recorded += newRecordedCount;
                console.log(`   📈 Session ${sessionId} stats: ${counter.recorded} moves recorded on-chain (${newRecordedCount} new from this flush)`);
            }
        }
        
        // Handle failed actions - add them back to queue for retry
        if (failedActions.length > 0) {
            console.log(`   ⚠️  ${failedActions.length} actions failed, adding back to queue for retry...`);
            if (!FAILED_MOVES.has(sessionId)) {
                FAILED_MOVES.set(sessionId, []);
            }
            FAILED_MOVES.get(sessionId).push(...failedActions);
            
            // Retry failed actions after a delay
            setTimeout(() => {
                retryFailedMoves(sessionId);
            }, 2000);
        }
        
        // Remove successfully processed actions from queue
        // CRITICAL: Get fresh queue to check for actions added during flush
        const currentQueue = actionQueues.get(sessionId);
        if (!currentQueue) {
            flushingQueues.delete(sessionId);
            return null;
        }
        
        // Track processed action IDs for reliable comparison
        const processedActionIds = new Set(actionsToProcess.map(a => a.id));
        const failedActionIds = new Set(failedActions.map(a => a.id));
        
        // Remove processed actions from the queue
        // Keep failed actions (for retry), end-session (if exists), and new actions (added during flush)
        const remaining = [];
        const currentEndSessionIndex = currentQueue.findIndex(a => a.type === 'end-session');
        
        for (let idx = 0; idx < currentQueue.length; idx++) {
            const action = currentQueue[idx];
            
            // Always keep end-session actions
            if (action.type === 'end-session') {
                remaining.push(action);
                continue;
            }
            
            // If this action was in our processing batch
            if (processedActionIds.has(action.id)) {
                // Only keep if it failed (for retry)
                if (failedActionIds.has(action.id)) {
                    remaining.push(action);
                }
                // Otherwise, it succeeded - remove it
            } else {
                // This is a NEW action added during flush - KEEP IT
                remaining.push(action);
            }
        }
        
        // Update queue
        if (remaining.length > 0) {
            actionQueues.set(sessionId, remaining);
            const remainingMoveCount = remaining.filter(a => a.type === 'move').length;
            const remainingEndSessionCount = remaining.filter(a => a.type === 'end-session').length;
            console.log(`   📊 Queue after flush: ${remaining.length} actions remaining (${failedActions.length} failed, ${remainingEndSessionCount} end-session, ${remaining.length - failedActions.length - remainingEndSessionCount} new)`);
        } else {
            actionQueues.delete(sessionId);
    }
    
        // If end-session is now at the front (all prior actions processed), execute it
        // BUT: Check if there are any non-end-session actions first - they should be processed before end-session
        const nonEndSessionActions = remaining.filter(a => a.type !== 'end-session');
        if (remaining.length > 0 && remaining[0].type === 'end-session' && nonEndSessionActions.length === 0) {
            console.log(`   🏁 All actions processed, executing end-session...`);
            const endSessionAction = remaining[0];
            const result = await executeEndSession(sessionId, endSessionAction);
            // Remove end-session from queue
            const finalRemaining = remaining.filter(a => a.id !== endSessionAction.id);
            if (finalRemaining.length > 0) {
                actionQueues.set(sessionId, finalRemaining);
            } else {
    actionQueues.delete(sessionId);
            }
            flushingQueues.delete(sessionId);
            return result;
        } else if (remaining.length > 0 && remaining[0].type === 'end-session' && nonEndSessionActions.length > 0) {
            // End-session is at front but there are other actions - they should be processed first
            console.log(`   ⚠️  End-session queued but ${nonEndSessionActions.length} actions still pending. Processing those first...`);
            // Reorder: move end-session to end, process other actions first
            const reorderedQueue = nonEndSessionActions.concat(remaining.filter(a => a.type === 'end-session'));
            actionQueues.set(sessionId, reorderedQueue);
            // Trigger another flush to process remaining actions
            if (!flushingQueues.has(sessionId)) {
                setTimeout(() => flushQueue(sessionId), 100);
            }
        }
    } catch (error) {
        console.error('Flush queue error:', error.message);
    } finally {
        // Always remove flushing flag FIRST before any other operations
        flushingQueues.delete(sessionId);
        
        // Clear timer
    if (queueTimers.has(sessionId)) {
        clearTimeout(queueTimers.get(sessionId));
        queueTimers.delete(sessionId);
        }
        
        // Check if more moves were added during flush - schedule auto-flush if needed
        // DON'T trigger immediate flush here to avoid race conditions
        // Let the normal auto-flush mechanism handle it (queue timer or batch size trigger)
        const remainingQueue = actionQueues.get(sessionId);
        if (remainingQueue && remainingQueue.length > 0) {
            const remainingMoves = remainingQueue.filter(a => a.type === 'move');
            if (remainingMoves.length > 0) {
                console.log(`🔄 ${remainingMoves.length} moves remaining after flush - will be processed by auto-flush`);
                // Ensure auto-flush timer is set if not already
                if (!queueTimers.has(sessionId)) {
                    queueTimers.set(sessionId, setTimeout(() => {
                        queueTimers.delete(sessionId);
                        if (!flushingQueues.has(sessionId)) {
                            flushQueue(sessionId);
                        }
                    }, BATCH_CONFIG.autoFlushDelay));
                }
            } else {
                console.log(`✅ No moves remaining after flush - queue cleared`);
            }
        } else {
            console.log(`✅ Queue empty after flush - all moves processed`);
        }
    }
}

// Periodic queue flush (backup, in case interval-based flush missed)
setInterval(() => {
    for (const [sessionId] of actionQueues) {
        // Only flush if not already flushing
        if (!flushingQueues.has(sessionId)) {
        flushQueue(sessionId);
        }
    }
}, BATCH_CONFIG.maxBatchInterval);

/**
 * Clear move queue for a session (called when session ends)
 * IMPORTANT: Flush all remaining moves before clearing to ensure 100% recording
 * @param {number} sessionId The session ID to clear
 */
/**
 * Queue end-session action
 * This ensures all moves/events before end-session are processed first
 * @param {number} sessionId The session ID
 * @param {string} reason Reason for ending (e.g., 'PLAYER_ENDED', 'TIMER_EXPIRED', 'OUT_OF_ENERGY')
 * @param {string} playerAddress The player's address
 * @returns {Promise<Object>} End session result (waits for all prior actions to complete)
 */
export async function queueEndSession(sessionId, reason = 'PLAYER_ENDED', playerAddress, moveCount = 0) {
    // Add end-session to action queue (will be processed after all prior actions)
    // IMPORTANT: Deduplicate end-session actions - only allow ONE end-session in queue
    if (!actionQueues.has(sessionId)) {
        actionQueues.set(sessionId, []);
    }

    const queue = actionQueues.get(sessionId);

    // Check if end-session is already queued
    const existingEndSession = queue.find(a => a.type === 'end-session');

    let endSessionAction;
    if (existingEndSession) {
        // Reuse existing end-session action (deduplicate), but update moveCount
        console.log(`🏁 End-session already queued for session ${sessionId}. Reusing existing end-session action.`);
        endSessionAction = existingEndSession;
        endSessionAction.moveCount = moveCount;
    } else {
        // Create new end-session action
        const actionId = `${sessionId}-end-session-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
        endSessionAction = {
            type: 'end-session',
            id: actionId,
            timestamp: Date.now(),
            player: playerAddress,
            reason: reason,
            moveCount: Math.max(0, moveCount)
        };
        queue.push(endSessionAction);
        console.log(`🏁 End-session queued for session ${sessionId} (reason: ${reason}, moveCount: ${moveCount}). Queue size: ${queue.length}`);
    }
    
    // Count actions before end-session
    const endSessionIndex = queue.indexOf(endSessionAction);
    const actionsBeforeEndSession = endSessionIndex >= 0 ? endSessionIndex : 0;
    console.log(`   ⏳ Processing ${actionsBeforeEndSession} actions before end-session...`);
    
    // Grace period: allow in-flight events from client to arrive before we flush.
    // With non-blocking handlers, backend accepts requests quickly; 1s gives late events time to land.
    const END_SESSION_GRACE_MS = 1000;
    await new Promise(resolve => setTimeout(resolve, END_SESSION_GRACE_MS));
    
    // Trigger immediate flush to process all actions before end-session
    await flushQueue(sessionId);
    
    // Wait for end-session to complete (poll until result is available)
    const maxWaitTime = 60000; // 60 seconds max wait
    const startTime = Date.now();
    while (!endSessionAction.result && (Date.now() - startTime) < maxWaitTime) {
        // Check if end-session was executed
        if (endSessionAction.result) {
            break;
        }
        
        // If queue is empty and no result, end-session was already executed
        const currentQueue = actionQueues.get(sessionId);
        if (!currentQueue || currentQueue.length === 0) {
            // Queue is empty - end-session was executed
            // Result should be in action.result
            break;
        }
        
        // Wait a bit before checking again
        await new Promise(resolve => setTimeout(resolve, 500));
        
        // Trigger flush again if needed (in case it's stuck)
        if (!flushingQueues.has(sessionId) && currentQueue.length > 0) {
            await flushQueue(sessionId);
        }
    }
    
    if (endSessionAction.result) {
        return endSessionAction.result;
    } else {
        // Timeout - check if session was already ended
        try {
            const session = await gameCore.getSession(sessionId);
            if (!session.isActive) {
                // Session ended - return success with final score
                return {
                    success: true,
                    sessionId,
                    finalScore: Number(session.finalScore),
                    message: 'Session ended (timeout waiting for end-session action)',
                    sessionStats: buildSessionStats(session)
                };
            }
        } catch (error) {
            // Ignore errors
        }
        // Timeout - return error
        throw new Error('End-session timeout - session may have already ended');
    }
}

export async function clearSessionQueue(sessionId) {
    console.log(`\n🏁 Clearing session ${sessionId} queue - ensuring all actions are recorded...`);
    
    // Wait for any current flush to complete (with timeout)
    let waitCount = 0;
    const maxWait = 50; // 5 seconds max wait
    while (flushingQueues.has(sessionId) && waitCount < maxWait) {
        await new Promise(resolve => setTimeout(resolve, 100));
        waitCount++;
    }
    
    // Flush any remaining actions (moves, events, end-session, etc.)
    // Use force mode to attempt processing even if session might be ended
    // (actions made during active session should still be recordable)
    let flushAttempts = 0;
    const maxFlushAttempts = 10; // Try up to 10 times to flush remaining actions
    while (actionQueues.has(sessionId) && actionQueues.get(sessionId).length > 0 && flushAttempts < maxFlushAttempts) {
        const remainingQueue = actionQueues.get(sessionId);
        const remainingCount = remainingQueue.length;
        const moveCount = remainingQueue.filter(a => a.type === 'move').length;
        const endSessionCount = remainingQueue.filter(a => a.type === 'end-session').length;
        const eventCount = remainingCount - moveCount - endSessionCount;
        console.log(`🔄 Flush attempt ${flushAttempts + 1}: ${remainingCount} actions remaining (${moveCount} moves, ${eventCount} events, ${endSessionCount} end-session)`);
        
        // Wait for any current flush to complete
        while (flushingQueues.has(sessionId) && waitCount < maxWait) {
            await new Promise(resolve => setTimeout(resolve, 100));
            waitCount++;
        }
        
        // Flush remaining actions with force mode (attempt even if session check fails)
        // Note: Contract will reject if session is not active, which is expected
        await flushQueue(sessionId, true); // force = true
        
        // Wait a bit for flush to complete
        await new Promise(resolve => setTimeout(resolve, 500));
        flushAttempts++;
    }
    
    // Retry any failed actions one last time
    if (FAILED_MOVES.has(sessionId) && FAILED_MOVES.get(sessionId).length > 0) {
        const failedActions = FAILED_MOVES.get(sessionId);
        console.log(`🔄 Retrying ${failedActions.length} failed actions before clearing session ${sessionId}`);
        await retryFailedMoves(sessionId);
        // Wait a bit for retry to process
        await new Promise(resolve => setTimeout(resolve, 1000));
    }
    
    // Final check and summary
    const finalQueue = actionQueues.get(sessionId);
    const finalRemaining = finalQueue ? finalQueue.length : 0;
    const finalMoveCount = finalQueue ? finalQueue.filter(a => a.type === 'move').length : 0;
    const finalEndSessionCount = finalQueue ? finalQueue.filter(a => a.type === 'end-session').length : 0;
    const finalEventCount = finalRemaining - finalMoveCount - finalEndSessionCount;
    const counter = moveCounters.get(sessionId);
    
    if (finalRemaining > 0) {
        console.log(`⚠️  Warning: ${finalRemaining} actions still in queue when clearing session ${sessionId} (${finalMoveCount} moves, ${finalEventCount} events, ${finalEndSessionCount} end-session)`);
    }
    
    // Show final summary
    if (counter) {
        console.log(`\n📊 Session ${sessionId} Final Summary:`);
        console.log(`   📥 Total moves queued: ${counter.total}`);
        console.log(`   ✅ Moves recorded on-chain: ${counter.recorded}`);
        if (counter.total > counter.recorded) {
            console.log(`   ⚠️  ${counter.total - counter.recorded} moves not yet recorded (may be in progress)`);
        } else if (counter.total === counter.recorded) {
            console.log(`   🎉 100% of moves recorded!`);
        }
        moveCounters.delete(sessionId);
    }
    
    // Now clear everything
    if (actionQueues.has(sessionId)) {
        actionQueues.delete(sessionId);
    }
    if (queueTimers.has(sessionId)) {
        clearTimeout(queueTimers.get(sessionId));
        queueTimers.delete(sessionId);
    }
    // Clear flushing flag and failed moves
    flushingQueues.delete(sessionId);
    FAILED_MOVES.delete(sessionId);
    
    console.log(`✅ Session ${sessionId} queue cleared\n`);
}

export default router;

