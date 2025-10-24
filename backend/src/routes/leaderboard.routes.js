import express from 'express';
import { leaderboard, chefNFT, gameCore } from '../services/blockchain.service.js';
import { optionalAuth } from '../middleware/auth.js';
import { getCachedLeaderboard, setCachedLeaderboard } from '../services/cache.service.js';

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
        
        // Fetch from blockchain
        const [players, scores] = await leaderboard.getTopPlayers(limit);
        
        // Enrich with player names
        const rankings = await Promise.all(
            players.map(async (player, index) => {
                let name = 'Unknown';
                try {
                    const hasChef = await chefNFT.playerHasChef(player);
                    if (hasChef) {
                        const tokenId = await chefNFT.getChefTokenId(player);
                        name = await chefNFT.getChefName(tokenId);
                    }
                } catch (error) {
                    console.warn(`Failed to get name for ${player}:`, error.message);
                }
                
                return {
                    rank: index + 1,
                    player,
                    playerName: name,
                    score: Number(scores[index])
                };
            })
        );
        
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
        const playerAddress = req.query.address || req.walletAddress;
        
        if (!playerAddress) {
            return res.status(400).json({ error: 'address parameter or auth required' });
        }
        
        const currentSeason = await leaderboard.currentSeason();
        const season = parseInt(req.query.season) || Number(currentSeason);
        
        const rank = await leaderboard.getPlayerRank(playerAddress);
        const score = await leaderboard.getPlayerHighScore(playerAddress);
        const totalPlayers = await leaderboard.getTotalPlayers();
        
        res.json({
            rank: Number(rank),
            score: Number(score),
            totalPlayers: Number(totalPlayers),
            season
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

