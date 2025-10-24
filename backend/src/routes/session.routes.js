import express from 'express';
import { chefNFT, gameCore } from '../services/blockchain.service.js';
import { addSessionSignerToWallet, sendSponsoredTransaction } from '../services/privy.service.js';
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
        const user = req.user;
        
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
        
        // Get user's wallet ID
        const wallet = user.linked_accounts?.find(a => a.type === 'wallet');
        if (!wallet || !wallet.wallet_id) {
            return res.status(400).json({ error: 'No wallet found for user' });
        }
        
        // Ensure session signer is added
        await addSessionSignerToWallet(wallet.wallet_id);
        
        // Encode startGameSession function call
        const data = gameCore.interface.encodeFunctionData('startGameSession', [playerAddress]);
        
        // Send sponsored transaction from USER's wallet
        const txResponse = await sendSponsoredTransaction(wallet.wallet_id, {
            to: process.env.GAMECORE_ADDRESS,
            data,
            value: '0x0'
        });
        
        console.log('   ✅ Session started (gas sponsored)!', txResponse.hash);
        
        // Wait for transaction confirmation and get session ID
        await new Promise(resolve => setTimeout(resolve, 3000));
        
        // Query the current session ID for this player
        const sessionId = Number(await gameCore.getCurrentSessionId(playerAddress));
        const startTime = Date.now();
        
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
            txHash: txResponse.hash,
            startTime,
            energyRemaining: Number(energy),
            gasSponsored: true
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
        const user = req.user;
        
        console.log(`🏁 Ending session ${sessionId} for ${playerAddress}`);
        
        // Verify session ownership
        const session = await gameCore.getSession(sessionId);
        if (session.player.toLowerCase() !== playerAddress.toLowerCase()) {
            return res.status(403).json({ error: 'Not your session' });
        }
        
        if (!session.isActive) {
            return res.status(400).json({ error: 'Session already ended' });
        }
        
        // Get user's wallet ID
        const wallet = user.linked_accounts?.find(a => a.type === 'wallet');
        if (!wallet || !wallet.wallet_id) {
            return res.status(400).json({ error: 'No wallet found for user' });
        }
        
        // Encode endGameSession function call
        const data = gameCore.interface.encodeFunctionData('endGameSession', [sessionId, 'PLAYER_ENDED']);
        
        // Send sponsored transaction
        const txResponse = await sendSponsoredTransaction(wallet.wallet_id, {
            to: process.env.GAMECORE_ADDRESS,
            data,
            value: '0x0'
        });
        
        console.log('   ✅ Session ended (gas sponsored)!', txResponse.hash);
        
        // Wait for transaction confirmation then query final score
        await new Promise(resolve => setTimeout(resolve, 3000));
        
        // Query session for final score
        const updatedSession = await gameCore.getSession(sessionId);
        const finalScore = Number(updatedSession.finalScore);
        
        // Delete cached session
        await deleteCachedSession(sessionId);
        
        res.json({
            success: true,
            sessionId,
            finalScore,
            txHash: txResponse.hash,
            gasSponsored: true
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

