import express from 'express';
import { chefNFT, gameCore } from '../services/blockchain.service.js';
import { requireAuth } from '../middleware/auth.js';
import { validateSessionId } from '../middleware/validation.js';
import { setCachedSession, deleteCachedSession } from '../services/cache.service.js';
import { GAME_CONFIG } from '../config/constants.js';

const router = express.Router();

/**
 * POST /api/session/start
 * Start a new game session
 */
router.post('/start', requireAuth, async (req, res, next) => {
    try {
        const playerAddress = req.walletAddress;
        
        console.log(`🎮 Starting session for ${playerAddress}`);
        
        // Check if player has chef
        const hasChef = await chefNFT.playerHasChef(playerAddress);
        if (!hasChef) {
            return res.status(400).json({ 
                error: 'No chef NFT found',
                message: 'Please mint a chef before playing'
            });
        }
        
        // Check energy
        const energy = await chefNFT.getPlayerEnergy(playerAddress);
        if (energy < GAME_CONFIG.MIN_ENERGY_TO_START) {
            return res.status(400).json({ 
                error: 'Insufficient energy',
                message: `Need at least ${GAME_CONFIG.MIN_ENERGY_TO_START} energy to start. Current: ${energy}`,
                currentEnergy: Number(energy)
            });
        }
        
        // Start session on blockchain
        const tx = await gameCore.startGameSession(playerAddress);
        console.log('   Transaction sent:', tx.hash);
        
        const receipt = await tx.wait();
        console.log('   ✅ Session started!');
        
        // Parse SessionStarted event
        const event = receipt.logs.find(log => {
            try {
                const parsed = gameCore.interface.parseLog(log);
                return parsed && parsed.name === 'SessionStarted';
            } catch {
                return false;
            }
        });
        
        let sessionId = 0;
        let startTime = Date.now();
        
        if (event) {
            const parsed = gameCore.interface.parseLog(event);
            sessionId = Number(parsed.args.sessionId);
            startTime = Number(parsed.args.startTime) * 1000; // Convert to ms
        }
        
        // Cache session
        await setCachedSession(sessionId, {
            sessionId,
            player: playerAddress,
            startTime,
            isActive: true,
            moveCount: 0,
            recipesDelivered: 0,
            recipesDiscarded: 0
        });
        
        res.json({
            success: true,
            sessionId,
            txHash: receipt.hash,
            startTime,
            energyRemaining: Number(energy)
        });
    } catch (error) {
        next(error);
    }
});

/**
 * POST /api/session/end
 * End a game session and compute final score
 */
router.post('/end', requireAuth, validateSessionId, async (req, res, next) => {
    try {
        const { sessionId } = req;
        const playerAddress = req.walletAddress;
        
        console.log(`🏁 Ending session ${sessionId} for ${playerAddress}`);
        
        // Verify session ownership
        const session = await gameCore.getSession(sessionId);
        if (session.player.toLowerCase() !== playerAddress.toLowerCase()) {
            return res.status(403).json({ error: 'Not your session' });
        }
        
        if (!session.isActive) {
            return res.status(400).json({ error: 'Session already ended' });
        }
        
        // End session (triggers score computation onchain)
        const tx = await gameCore.endGameSession(sessionId, 'PLAYER_ENDED');
        console.log('   Transaction sent:', tx.hash);
        
        const receipt = await tx.wait();
        console.log('   ✅ Session ended!');
        
        // Parse ScoreFinalized event
        const event = receipt.logs.find(log => {
            try {
                const parsed = gameCore.interface.parseLog(log);
                return parsed && parsed.name === 'ScoreFinalized';
            } catch {
                return false;
            }
        });
        
        let finalScore = 0;
        if (event) {
            const parsed = gameCore.interface.parseLog(event);
            finalScore = Number(parsed.args.score);
        } else {
            // Fallback: query session
            const updatedSession = await gameCore.getSession(sessionId);
            finalScore = Number(updatedSession.finalScore);
        }
        
        // Delete cached session
        await deleteCachedSession(sessionId);
        
        res.json({
            success: true,
            sessionId,
            finalScore,
            txHash: receipt.hash
        });
    } catch (error) {
        next(error);
    }
});

/**
 * GET /api/session/current
 * Get current active session for player
 */
router.get('/current', requireAuth, async (req, res, next) => {
    try {
        const playerAddress = req.walletAddress;
        
        const sessionId = await gameCore.getCurrentSessionId(playerAddress);
        
        if (sessionId === 0n || sessionId === 0) {
            return res.json({ 
                hasActiveSession: false,
                sessionId: 0
            });
        }
        
        const session = await gameCore.getSession(sessionId);
        
        res.json({
            hasActiveSession: true,
            sessionId: Number(sessionId),
            startTime: Number(session.startTime) * 1000,
            moveCount: Number(session.moveCount),
            recipesSpawned: Number(session.recipesSpawned),
            recipesDelivered: Number(session.recipesDelivered),
            recipesDiscarded: Number(session.recipesDiscarded),
            wellCookedCount: Number(session.wellCookedCount),
            overCookedCount: Number(session.overCookedCount),
            consecutivePerfectDeliveries: Number(session.consecutivePerfectDeliveries)
        });
    } catch (error) {
        next(error);
    }
});

/**
 * GET /api/session/history
 * Get player's session history
 */
router.get('/history', requireAuth, async (req, res, next) => {
    try {
        const playerAddress = req.walletAddress;
        const limit = parseInt(req.query.limit) || 10;
        const offset = parseInt(req.query.offset) || 0;
        
        const sessionIds = await gameCore.getPlayerSessionHistory(playerAddress);
        
        // Reverse to get newest first
        const reversedIds = [...sessionIds].reverse();
        const paginatedIds = reversedIds.slice(offset, offset + limit);
        
        // Fetch session data
        const sessions = await Promise.all(
            paginatedIds.map(async (id) => {
                const session = await gameCore.getSession(id);
                return {
                    sessionId: Number(id),
                    startTime: Number(session.startTime) * 1000,
                    endTime: Number(session.endTime) * 1000,
                    finalScore: Number(session.finalScore),
                    recipesDelivered: Number(session.recipesDelivered),
                    moveCount: Number(session.moveCount)
                };
            })
        );
        
        res.json({
            sessions,
            total: sessionIds.length,
            limit,
            offset
        });
    } catch (error) {
        next(error);
    }
});

export default router;

