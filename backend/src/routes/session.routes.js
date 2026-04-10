import express from 'express';
import { chefNFT, gameCore } from '../services/blockchain.service.js';
import { queueTransaction } from '../services/transactionQueue.service.js';
import { addSessionSignerToWallet, sendSponsoredTransaction } from '../services/privy.service.js';
import { requireAuth } from '../middleware/auth.js';
import { validateSessionId } from '../middleware/validation.js';
import { getCachedSession, setCachedSession, deleteCachedSession, clearLeaderboardCache, deleteCachedProfile } from '../services/cache.service.js';
import { GAME_CONFIG } from '../config/constants.js';
import { clearSessionQueue, queueEndSession } from './gameplay.routes.js';
import { safePlayerHasChef, safeGetChefTokenId, safeGetPlayerEnergy } from '../utils/contractHelpers.js';

const router = express.Router();
const SESSION_TIMEOUT_SECONDS = Number(process.env.SESSION_TIMEOUT_SECONDS || GAME_CONFIG.DURATION || 600);
const SESSION_TIMEOUT_GRACE_SECONDS = Number(process.env.SESSION_TIMEOUT_GRACE_SECONDS || 60);
const HEARTBEAT_STALE_SECONDS = Number(process.env.SESSION_HEARTBEAT_STALE_SECONDS || 90);
const ENABLE_HEARTBEAT_RECOVERY = process.env.ENABLE_HEARTBEAT_RECOVERY === 'true';

function formatSessionStats(session) {
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
        combo: Number(session.maxComboAchieved),
        currentStreak: Number(session.consecutivePerfectDeliveries)
    };
}

function getSessionAgeSeconds(session) {
    const startTime = Number(session.startTime || 0);
    if (startTime <= 0) return 0;
    return Math.max(0, Math.floor(Date.now() / 1000) - startTime);
}

async function resolveActiveSessionRecovery(sessionId, session) {
    const ageSeconds = getSessionAgeSeconds(session);
    const timeoutThreshold = SESSION_TIMEOUT_SECONDS + SESSION_TIMEOUT_GRACE_SECONDS;

    if (ageSeconds >= timeoutThreshold) {
        return {
            canAutoEnd: true,
            reason: 'AUTO_TIMEOUT',
            ageSeconds
        };
    }

    if (ENABLE_HEARTBEAT_RECOVERY) {
        const cachedSession = await getCachedSession(Number(sessionId));
        if (cachedSession?.lastHeartbeatAt) {
            const heartbeatAgeSeconds = Math.floor((Date.now() - Number(cachedSession.lastHeartbeatAt)) / 1000);
            if (heartbeatAgeSeconds >= HEARTBEAT_STALE_SECONDS && ageSeconds >= HEARTBEAT_STALE_SECONDS) {
                return {
                    canAutoEnd: true,
                    reason: 'AUTO_HEARTBEAT_STALE',
                    ageSeconds
                };
            }
        }
    }

    return {
        canAutoEnd: false,
        ageSeconds
    };
}

function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

async function retryRpcRead(fn, label, attempts = 3, delayMs = 250) {
    let lastError;
    for (let attempt = 1; attempt <= attempts; attempt++) {
        try {
            return await fn();
        } catch (error) {
            lastError = error;
            if (attempt === attempts) break;
            console.warn(
                `⚠️ ${label} failed (attempt ${attempt}/${attempts}): ${error.message}. Retrying...`
            );
            await sleep(delayMs * attempt);
        }
    }

    throw lastError;
}

/**
 * POST /api/session/start
 * Start a new game session
 */
router.post('/start', requireAuth, async (req, res, next) => {
    try {
        const playerAddress = req.walletAddress;
        const user = req.user;
        
        console.log(`🎮 Starting session for ${playerAddress}`);
        
        // Check if player has chef (retry to smooth transient RPC read errors).
        let hasChef = await retryRpcRead(
            () => safePlayerHasChef(playerAddress),
            `Checking chef for ${playerAddress}`
        );
        if (!hasChef) {
            // Fallback: some RPC nodes intermittently fail on boolean reads.
            const tokenIdFallback = await retryRpcRead(
                () => safeGetChefTokenId(playerAddress),
                `Checking chef tokenId fallback for ${playerAddress}`
            );
            hasChef = Number(tokenIdFallback) > 0;
        }
        if (!hasChef) {
            return res.status(400).json({ 
                error: 'No chef NFT found',
                message: 'Please mint a chef before playing'
            });
        }
        
        // Check if there's an active session first
        const currentSessionId = await gameCore.getCurrentSessionId(playerAddress);
        if (currentSessionId != 0n) {
            try {
                const existingSession = await gameCore.getSession(currentSessionId);
                if (existingSession.isActive) {
                    const recovery = await resolveActiveSessionRecovery(currentSessionId, existingSession);
                    if (recovery.canAutoEnd) {
                        console.log(`⏰ Auto-ending stale session ${currentSessionId} (${recovery.reason}, age: ${recovery.ageSeconds}s)`);
                        await queueEndSession(Number(currentSessionId), recovery.reason, playerAddress, Number(existingSession.moveCount || 0));
                        await deleteCachedSession(Number(currentSessionId));
                        await deleteCachedProfile(playerAddress);
                        try {
                            await clearLeaderboardCache();
                        } catch (cacheError) {
                            console.warn(`⚠️ Failed to clear leaderboard cache after auto-ending session ${currentSessionId}:`, cacheError.message);
                        }
                    } else {
                        return res.status(400).json({
                            error: 'Session already active',
                            message: `You already have an active session (ID: ${currentSessionId}). Resume or end it before starting a new one.`,
                            sessionId: Number(currentSessionId),
                            canResume: true,
                            sessionAgeSeconds: recovery.ageSeconds
                        });
                    }
                }
            } catch (err) {
                // Session might not exist, continue
            }
        }
        
        // Check energy (retry to smooth transient RPC read errors).
        const energyBigInt = await retryRpcRead(
            () => safeGetPlayerEnergy(playerAddress),
            `Checking energy for ${playerAddress}`
        );
        const energy = Number(energyBigInt);
        const requiredEnergy = GAME_CONFIG.MOVE_COST * 5; // Contract requires MOVE_COST * 5
        if (energy < requiredEnergy) {
            return res.status(400).json({ 
                error: 'Insufficient energy',
                message: `Need at least ${requiredEnergy} energy to start. Current: ${energy}`,
                currentEnergy: energy,
                requiredEnergy
            });
        }
        
        // HACKATHON MODE: Backend pays gas - use transaction queue
        try {
            const receipt = await queueTransaction(
                () => gameCore.startGameSession(playerAddress),
                `Start Session (${playerAddress})`
            );
        console.log('   ✅ Session started!');
        
        // Get session ID from event
        const event = receipt.logs.find(log => {
            try {
                const parsed = gameCore.interface.parseLog(log);
                return parsed && parsed.name === 'SessionStarted';
            } catch {
                return false;
            }
        });
        
        let sessionId = 0;
        if (event) {
            const parsed = gameCore.interface.parseLog(event);
            sessionId = Number(parsed.args.sessionId);
        } else {
            // Fallback
            sessionId = Number(await gameCore.getCurrentSessionId(playerAddress));
        }
        
        const startTime = Date.now();
        
        // Cache session
        await setCachedSession(sessionId, {
            sessionId,
            player: playerAddress,
            startTime,
            lastHeartbeatAt: Date.now(),
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
            // Handle specific contract errors
            const errorData = error.data || error.info?.error?.data || '';
            const errorString = errorData.toString();
            
            // Check for InsufficientEnergy error (0x27a7585d)
            if (errorString.includes('27a7585d') || 
                error.reason === 'InsufficientEnergy' ||
                error.message?.includes('InsufficientEnergy')) {
                // Re-check energy (might have changed)
                const currentEnergyBigInt = await safeGetPlayerEnergy(playerAddress);
                const currentEnergy = Number(currentEnergyBigInt);
                return res.status(400).json({ 
                    error: 'Insufficient energy',
                    message: `Contract rejected: Need at least ${requiredEnergy} energy to start. Current on-chain: ${currentEnergy}`,
                    currentEnergy,
                    requiredEnergy
                });
            }
            
            // Check for SessionAlreadyActive error
            if (errorString.includes('SessionAlreadyActive') ||
                error.reason === 'SessionAlreadyActive' ||
                error.message?.includes('SessionAlreadyActive')) {
                const currentSessionId = await gameCore.getCurrentSessionId(playerAddress);
                return res.status(400).json({ 
                    error: 'Session already active',
                    message: `You already have an active session on-chain. Please end it first.`,
                    sessionId: Number(currentSessionId)
                });
            }
            
            // Fallback recovery: if tx submission/reporting failed but session is now active
            // on-chain, return success so client can continue gameplay.
            try {
                const recoveredSessionId = await gameCore.getCurrentSessionId(playerAddress);
                if (recoveredSessionId != 0n) {
                    const recoveredSession = await gameCore.getSession(recoveredSessionId);
                    if (recoveredSession?.isActive) {
                        const sessionId = Number(recoveredSessionId);
                        const startTime = Number(recoveredSession.startTime || 0) * 1000 || Date.now();

                        await setCachedSession(sessionId, {
                            sessionId,
                            player: playerAddress,
                            startTime,
                            lastHeartbeatAt: Date.now(),
                            isActive: true,
                            moveCount: Number(recoveredSession.moveCount || 0),
                            recipesDelivered: Number(recoveredSession.recipesDelivered || 0),
                            recipesDiscarded: Number(recoveredSession.recipesDiscarded || 0)
                        });

                        return res.json({
                            success: true,
                            sessionId,
                            txHash: null,
                            startTime,
                            energyRemaining: Number(energy),
                            recovered: true,
                            message: 'Recovered active on-chain session after transient start error'
                        });
                    }
                }
            } catch (recoveryError) {
                console.warn(
                    `⚠️ Failed to recover session state after start error for ${playerAddress}:`,
                    recoveryError.message
                );
            }

            // Pass other errors to error handler
            next(error);
        }
    } catch (error) {
        next(error);
    }
});

/**
 * POST /api/session/heartbeat
 * Keep active session alive while app is running.
 */
router.post('/heartbeat', requireAuth, validateSessionId, async (req, res, next) => {
    try {
        const sessionId = req.sessionId;
        const playerAddress = req.walletAddress;

        const session = await gameCore.getSession(sessionId);
        if (session.player.toLowerCase() !== playerAddress.toLowerCase()) {
            return res.status(403).json({ error: 'Not your session' });
        }

        if (!session.isActive) {
            return res.status(400).json({
                error: 'Session already ended',
                sessionId
            });
        }

        await setCachedSession(sessionId, {
            sessionId,
            player: playerAddress,
            isActive: true,
            startTime: Number(session.startTime) * 1000,
            lastHeartbeatAt: Date.now()
        });

        res.json({
            success: true,
            sessionId,
            heartbeatAt: Date.now()
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
        const sessionId = req.sessionId; // From validateSessionId
        const { moveCount: clientMoveCount } = req.body || {};
        const playerAddress = req.walletAddress;
        const user = req.user;
        const moveCount = Math.max(0, parseInt(clientMoveCount, 10) || 0);

        console.log(`🏁 Ending session ${sessionId} for ${playerAddress} (moveCount: ${moveCount})`);
        
        // Verify session ownership
        const session = await gameCore.getSession(sessionId);
        if (session.player.toLowerCase() !== playerAddress.toLowerCase()) {
            return res.status(403).json({ error: 'Not your session' });
        }
        
        if (!session.isActive) {
            // Session already ended - clear queue and return final score
            console.log(`   ℹ️  Session ${sessionId} already ended (duplicate call), fetching final score...`);
            const updatedSession = await gameCore.getSession(sessionId);
            const sessionStats = formatSessionStats(updatedSession);
            
            // Clear any pending move queues for this session
            await clearSessionQueue(sessionId);
            
            // Delete cached session and profile (so recent sessions refresh)
            await deleteCachedSession(sessionId);
            await deleteCachedProfile(playerAddress);
            try {
                await clearLeaderboardCache();
            } catch (cacheError) {
                console.warn(`⚠️ Failed to clear leaderboard cache after session ${sessionId}:`, cacheError.message);
            }
            
            return res.json({
                success: true,
                sessionId,
                finalScore: Number(updatedSession.finalScore),
                message: 'Session was already ended',
                sessionStats
            });
        }
        
        // CRITICAL: Queue end-session action instead of executing immediately
        // This ensures ALL moves/events before end-session are processed first
        // The queue system will process all prior actions, then execute end-session
        console.log(`   🔄 Queuing end-session - all actions before this will be processed first...`);
        
        let result;
        try {
            result = await queueEndSession(sessionId, 'PLAYER_ENDED', playerAddress, moveCount);
        } catch (error) {
            // Check if session was already ended (race condition)
            if (error.message?.includes('already ended') || 
                error.message?.includes('SessionNotActive')) {
                
                console.log(`   ℹ️  Session ${sessionId} already ended (race condition), fetching final score...`);
                const updatedSession = await gameCore.getSession(sessionId);
                const sessionStats = formatSessionStats(updatedSession);
                
                // Delete cached session and profile
                await deleteCachedSession(sessionId);
                await deleteCachedProfile(playerAddress);
                try {
                    await clearLeaderboardCache();
                } catch (cacheError) {
                    console.warn(`⚠️ Failed to clear leaderboard cache after session ${sessionId}:`, cacheError.message);
                }
                
                return res.json({
                    success: true,
                    sessionId,
                    finalScore: Number(updatedSession.finalScore),
                    message: 'Session was already ended',
                    sessionStats
                });
            }
            // Re-throw if it's a different error
            throw error;
        }
        
        // Delete cached session and profile (so recent sessions refresh)
        await deleteCachedSession(sessionId);
        await deleteCachedProfile(playerAddress);
        try {
            await clearLeaderboardCache();
        } catch (cacheError) {
            console.warn(`⚠️ Failed to clear leaderboard cache after session ${sessionId}:`, cacheError.message);
        }
        
        const sessionStats = result.sessionStats || formatSessionStats(await gameCore.getSession(sessionId));
        
        res.json({
            success: true,
            sessionId,
            finalScore: result.finalScore,
            txHash: result.txHash,
            message: result.message,
            sessionStats
        });
    } catch (error) {
        next(error);
    }
});

/**
 * POST /api/session/force-end
 * Player-initiated recovery when a stale/blocked session prevents a new game.
 */
router.post('/force-end', requireAuth, validateSessionId, async (req, res, next) => {
    try {
        const sessionId = req.sessionId;
        const playerAddress = req.walletAddress;
        const { moveCount: clientMoveCount } = req.body || {};
        const moveCount = Math.max(0, parseInt(clientMoveCount, 10) || 0);

        const session = await gameCore.getSession(sessionId);
        if (session.player.toLowerCase() !== playerAddress.toLowerCase()) {
            return res.status(403).json({ error: 'Not your session' });
        }

        if (!session.isActive) {
            await deleteCachedSession(sessionId);
            return res.json({
                success: true,
                sessionId,
                finalScore: Number(session.finalScore),
                message: 'Session already ended'
            });
        }

        const result = await queueEndSession(sessionId, 'PLAYER_FORCE_ENDED', playerAddress, moveCount);
        await deleteCachedSession(sessionId);
        await deleteCachedProfile(playerAddress);
        try {
            await clearLeaderboardCache();
        } catch (cacheError) {
            console.warn(`⚠️ Failed to clear leaderboard cache after force-ending session ${sessionId}:`, cacheError.message);
        }

        res.json({
            success: true,
            sessionId,
            finalScore: result.finalScore,
            txHash: result.txHash,
            message: result.message || 'Session force-ended'
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

