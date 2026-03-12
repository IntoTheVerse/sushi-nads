import express from 'express';
import { leaderboard, chefNFT, gameCore } from '../services/blockchain.service.js';
import { optionalAuth } from '../middleware/auth.js';
import { getCachedLeaderboard, setCachedLeaderboard } from '../services/cache.service.js';
import { safePlayerHasChef, safeGetChefTokenId, safeGetChefName } from '../utils/contractHelpers.js';

const ZERO_ADDRESS = '0x0000000000000000000000000000000000000000';

async function fetchAllChefProfiles() {
    try {
        const totalChefs = Number(await chefNFT.totalChefs());
        if (!totalChefs) {
            return [];
        }

        const tokenIds = Array.from({ length: totalChefs }, (_, index) => index + 1);
        const chefs = await Promise.all(
            tokenIds.map(async tokenId => {
                try {
                    const [owner, name] = await Promise.all([
                        chefNFT.ownerOf(tokenId),
                        chefNFT.chefNames(tokenId)
                    ]);

                    if (!owner || owner === ZERO_ADDRESS) {
                        return null;
                    }

                    return {
                        tokenId,
                        player: owner,
                        playerName: name || null
                    };
                } catch (error) {
                    console.warn(`⚠️  Failed to load chef token ${tokenId}:`, error.message);
                    return null;
                }
            })
        );

        return chefs.filter(Boolean);
    } catch (error) {
        console.warn('⚠️  Failed to read chef roster from chain:', error.message);
        return [];
    }
}

function normalizeAddress(address) {
    return typeof address === 'string' ? address.trim().toLowerCase() : '';
}

const router = express.Router();

/**
 * GET /api/leaderboard/top
 * Get top N players for current season
 */
router.get('/top', optionalAuth, async (req, res, next) => {
    try {
        const limit = parseInt(req.query.limit) || 100;
        const currentSeason = await leaderboard.currentSeason();
        const season = parseInt(req.query.season) || Number(currentSeason);
        
        console.log(`🏆 Fetching top ${limit} players for season ${season}`);
        
        // Check cache first
        const cached = await getCachedLeaderboard(season, limit);
        if (cached) {
            return res.json({ rankings: cached, cached: true, season });
        }
        
        // Fetch from blockchain (prefer detailed entries if available)
        let entries;
        try {
            entries = await leaderboard.getTopPlayersDetailed(limit);
        } catch (detailedError) {
            console.warn('⚠️  Failed to fetch detailed leaderboard entries, falling back to basic data:', detailedError.message);
            const [players, scores] = await leaderboard.getTopPlayers(limit);
            entries = players.map((player, index) => ({
                player,
                highScore: scores[index],
                sessionId: 0,
                timestamp: 0,
                rank: index + 1
            }));
        }
        
        // Enrich with player names
        let rankings = await Promise.all(
            entries.map(async (entry, index) => {
                let name = 'Unknown';
                try {
                    const hasChef = await safePlayerHasChef(entry.player);
                    if (hasChef) {
                        const tokenIdBigInt = await safeGetChefTokenId(entry.player);
                        const tokenId = Number(tokenIdBigInt);
                        if (tokenId > 0) {
                            name = await safeGetChefName(tokenId);
                        }
                    }
                } catch (error) {
                    console.warn(`Failed to get name for ${entry.player}:`, error.message);
                }
                
                const computedRank = Number(entry.rank || 0);
                
                return {
                    rank: computedRank > 0 ? computedRank : index + 1,
                    player: entry.player,
                    playerName: name,
                    score: Number(entry.highScore),
                    sessionId: Number(entry.sessionId || 0),
                    lastUpdated: Number(entry.timestamp || 0) * 1000
                };
            })
        );

        // Ensure every minted chef shows up even if they haven't posted a score yet.
        const chefRoster = await fetchAllChefProfiles();
        if (chefRoster.length) {
            const rankedAddresses = new Set(
                rankings
                    .map(entry => normalizeAddress(entry.player))
                    .filter(addr => addr.length > 0)
            );

            const missingChefs = chefRoster.filter(
                chef => chef.player && !rankedAddresses.has(normalizeAddress(chef.player))
            );

            if (missingChefs.length) {
                const fallbackEntries = await Promise.all(
                    missingChefs.map(async chef => {
                        let entry = null;
                        try {
                            entry = await leaderboard.getPlayerEntry(chef.player);
                        } catch (error) {
                            console.warn(`⚠️  Failed to load fallback entry for ${chef.player}:`, error.message);
                        }

                        return {
                            rank: entry ? Number(entry.rank || 0) : 0,
                            player: chef.player,
                            playerName: chef.playerName && chef.playerName.trim().length > 0
                                ? chef.playerName
                                : chef.player,
                            score: entry ? Number(entry.highScore || 0) : 0,
                            sessionId: entry ? Number(entry.sessionId || 0) : 0,
                            lastUpdated: entry ? Number(entry.timestamp || 0) * 1000 : 0
                        };
                    })
                );

                rankings = rankings.concat(fallbackEntries);
            }
        }
        
        // Cache result
        await setCachedLeaderboard(season, limit, rankings);
        
        res.json({ rankings, cached: false, season });
    } catch (error) {
        next(error);
    }
});

/**
 * GET /api/leaderboard/player-rank
 * Get rank for a specific player
 */
router.get('/player-rank', optionalAuth, async (req, res, next) => {
    try {
        const playerAddress =
            req.query.address ||
            req.query.walletAddress ||
            req.walletAddress;
        
        if (!playerAddress) {
            return res.status(400).json({ error: 'address parameter or auth required' });
        }
        
        let currentSeason = 0;
        let totalPlayers = 0;
        try {
            currentSeason = Number(await leaderboard.currentSeason());
            totalPlayers = Number(await leaderboard.getTotalPlayers());
        } catch (e) {
            console.warn('⚠️  Leaderboard currentSeason/getTotalPlayers failed, using defaults:', e.message);
        }
        
        const season = parseInt(req.query.season) || currentSeason;
        
        let entry = null;
        try {
            entry = await leaderboard.getPlayerEntry(playerAddress);
        } catch (entryError) {
            console.warn(`⚠️  getPlayerEntry failed for ${playerAddress}:`, entryError.message);
        }
        
        let rank = 0;
        let score = 0;
        let sessionId = 0;
        let lastUpdated = 0;
        
        if (entry) {
            rank = Number(entry.rank || 0);
            score = Number(entry.highScore || 0);
            sessionId = Number(entry.sessionId || 0);
            lastUpdated = Number(entry.timestamp || 0) * 1000;
        } else {
            try {
                rank = Number(await leaderboard.getPlayerRank(playerAddress));
            } catch (rankErr) {
                console.warn(`⚠️  getPlayerRank failed for ${playerAddress}:`, rankErr.message);
            }
            try {
                score = Number(await leaderboard.getPlayerHighScore(playerAddress));
            } catch (scoreErr) {
                console.warn(`⚠️  getPlayerHighScore failed for ${playerAddress}:`, scoreErr.message);
            }
        }

        // Fallback: when chain doesn't support rank (e.g. Monad), derive from top rankings
        let rankSupported = entry != null;
        const addr = (playerAddress || '').trim().toLowerCase();
        if (rank <= 0 && addr) {
            const cached = await getCachedLeaderboard(season, 100);
            if (cached && Array.isArray(cached)) {
                const found = cached.findIndex(
                    (r) => r && (r.player || '').toLowerCase() === addr
                );
                if (found >= 0) {
                    const e = cached[found];
                    rank = Number(e.rank || found + 1);
                    if (score <= 0 && e.score != null) score = Number(e.score);
                    if (sessionId <= 0 && e.sessionId != null) sessionId = Number(e.sessionId);
                    if (lastUpdated <= 0 && e.lastUpdated != null) lastUpdated = Number(e.lastUpdated);
                    rankSupported = true;
                }
            }
        }

        res.json({
            rank: Number(rank),
            score: Number(score),
            sessionId,
            lastUpdated,
            totalPlayers,
            season,
            rankSupported
        });
    } catch (error) {
        next(error);
    }
});

/**
 * GET /api/leaderboard/global-stats
 * Get aggregated global statistics
 */
router.get('/global-stats', async (req, res, next) => {
    try {
        const totalSessions = await gameCore.totalSessions();
        const totalPlayers = await leaderboard.getTotalPlayers();
        const currentSeason = await leaderboard.currentSeason();
        
        res.json({
            totalSessions: Number(totalSessions),
            totalPlayers: Number(totalPlayers),
            currentSeason: Number(currentSeason)
        });
    } catch (error) {
        next(error);
    }
});

export default router;

