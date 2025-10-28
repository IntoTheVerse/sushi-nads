import express from 'express';
import { gameCore } from '../services/blockchain.service.js';
import { queueTransaction } from '../services/transactionQueue.service.js';
import { addSessionSignerToWallet, sendSponsoredTransaction } from '../services/privy.service.js';
import { requireAuth } from '../middleware/auth.js';
import { validateSessionId } from '../middleware/validation.js';
import { rateLimitGameActions } from '../middleware/rateLimit.js';
import { BATCH_CONFIG } from '../config/constants.js';

const router = express.Router();

// Action queue for batching moves
const actionQueues = new Map();
const queueTimers = new Map();

/**
 * POST /api/gameplay/move
 * Record a player move (batched for gas optimization)
 */
router.post('/move', requireAuth, validateSessionId, rateLimitGameActions, async (req, res, next) => {
    try {
        const { sessionId } = req;
        const playerAddress = req.walletAddress;
        
        // Verify session ownership and active status
        const session = await gameCore.getSession(sessionId);
        if (session.player.toLowerCase() !== playerAddress.toLowerCase()) {
            return res.status(403).json({ error: 'Not your session' });
        }
        
        if (!session.isActive) {
            return res.status(400).json({ error: 'Session not active' });
        }
        
        // Add to batch queue
        if (!actionQueues.has(sessionId)) {
            actionQueues.set(sessionId, []);
        }
        
        actionQueues.get(sessionId).push({ 
            type: 'move', 
            timestamp: Date.now(),
            player: playerAddress
        });
        
        // Setup auto-flush timer if not already set
        if (!queueTimers.has(sessionId)) {
            const timer = setTimeout(() => {
                flushQueue(sessionId);
            }, BATCH_CONFIG.maxBatchInterval);
            queueTimers.set(sessionId, timer);
        }
        
        // Flush immediately if batch size reached
        if (actionQueues.get(sessionId).length >= BATCH_CONFIG.maxBatchSize) {
            await flushQueue(sessionId);
        }
        
        // Return immediately for instant UX
        res.json({ 
            success: true,
            queued: true,
            queueSize: actionQueues.get(sessionId)?.length || 0
        });
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
        
        const tx = await gameCore.recordRecipeSpawned(sessionId);
        await tx.wait();
        
        res.json({ success: true });
    } catch (error) {
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
        
        console.log(`✅ Recipe delivered in session ${sessionId}`);
        
        // Queue transaction to prevent nonce conflicts
        const receipt = await queueTransaction(
            () => gameCore.recordRecipeDelivered(sessionId),
            `Recipe Delivery (Session ${sessionId})`
        );
        
        res.json({ success: true, txHash: receipt.hash, gasSponsored: true });
    } catch (error) {
        console.error(`❌ Failed to record recipe delivery:`, error.message);
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
        
        console.log(`🗑️  Recipe discarded in session ${sessionId}`);
        
        // Queue transaction to prevent nonce conflicts
        const receipt = await queueTransaction(
            () => gameCore.recordRecipeDiscarded(sessionId),
            `Recipe Discard (Session ${sessionId})`
        );
        
        res.json({ success: true, txHash: receipt.hash, gasSponsored: true });
    } catch (error) {
        console.error(`❌ Failed to record discard:`, error.message);
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
        
        console.log(`🔥 Well-cooked ingredient in session ${sessionId}`);
        
        // Queue transaction to prevent nonce conflicts
        const receipt = await queueTransaction(
            () => gameCore.recordWellCooked(sessionId),
            `Well-Cooked (Session ${sessionId})`
        );
        
        res.json({ success: true, txHash: receipt.hash, gasSponsored: true });
    } catch (error) {
        console.error(`❌ Failed to record well-cooked:`, error.message);
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
        
        console.log(`💀 Overcooked ingredient in session ${sessionId}`);
        
        // Queue transaction to prevent nonce conflicts
        const receipt = await queueTransaction(
            () => gameCore.recordOverCooked(sessionId),
            `Overcooked (Session ${sessionId})`
        );
        
        res.json({ success: true, txHash: receipt.hash, gasSponsored: true });
    } catch (error) {
        console.error(`❌ Failed to record overcooked:`, error.message);
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
 * Flush queued actions to blockchain
 */
async function flushQueue(sessionId) {
    const queue = actionQueues.get(sessionId);
    if (!queue || queue.length === 0) return;
    
    const moves = queue.filter(a => a.type === 'move');
    
    if (moves.length > 0) {
        try {
            console.log(`📦 Flushing ${moves.length} moves for session ${sessionId}`);
            
            // Submit each move using transaction queue to prevent nonce conflicts
            for (let i = 0; i < moves.length; i++) {
                await queueTransaction(
                    () => gameCore.recordMove(sessionId),
                    `Move ${i + 1}/${moves.length} (Session ${sessionId})`
                );
            }
            
            console.log(`   ✅ Flushed ${moves.length} moves successfully`);
        } catch (error) {
            console.error('Flush queue error:', error.message);
        }
    }
    
    // Clear queue and timer
    actionQueues.delete(sessionId);
    if (queueTimers.has(sessionId)) {
        clearTimeout(queueTimers.get(sessionId));
        queueTimers.delete(sessionId);
    }
}

// Periodic queue flush (backup, in case interval-based flush missed)
setInterval(() => {
    for (const [sessionId] of actionQueues) {
        flushQueue(sessionId);
    }
}, BATCH_CONFIG.maxBatchInterval);

export default router;

