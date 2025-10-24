import express from 'express';
import { chefNFT, gameCore } from '../services/blockchain.service.js';
import { addSessionSignerToWallet, sendSponsoredTransaction } from '../services/privy.service.js';
import { requireAuth } from '../middleware/auth.js';
import { validateMintChef, validateUpdateName } from '../middleware/validation.js';
import { getCachedProfile, setCachedProfile } from '../services/cache.service.js';

const router = express.Router();

/**
 * POST /api/chef/mint
 * Mint a Chef NFT with player name (gas sponsored)
 */
router.post('/mint', requireAuth, validateMintChef, async (req, res, next) => {
    try {
        const { playerName } = req.body;
        const playerAddress = req.walletAddress;
        
        console.log(`🎨 Minting chef for ${playerAddress} with name: ${playerName}`);
        
        // Check if player already has chef
        const hasChef = await chefNFT.playerHasChef(playerAddress);
        if (hasChef) {
            return res.status(400).json({ error: 'Already has chef NFT' });
        }
        
        // HACKATHON MODE: Backend pays gas (simple approach)
        // Mint chef NFT directly to player using mintChefTo
        const tx = await chefNFT.mintChefTo(playerAddress, playerName);
        console.log('   Transaction sent:', tx.hash);
        
        const receipt = await tx.wait();
        console.log('   ✅ Chef minted directly to player!');
        
        // Get token ID from event
        const mintEvent = receipt.logs.find(log => {
            try {
                const parsed = chefNFT.interface.parseLog(log);
                return parsed && parsed.name === 'ChefMinted';
            } catch {
                return false;
            }
        });
        
        let tokenId = 0;
        if (mintEvent) {
            const parsed = chefNFT.interface.parseLog(mintEvent);
            tokenId = Number(parsed.args.tokenId);
        } else {
            // Fallback: query contract
            tokenId = Number(await chefNFT.getChefTokenId(playerAddress));
        }
        
        res.json({
            success: true,
            txHash: receipt.hash,
            tokenId,
            name: playerName
        });
    } catch (error) {
        next(error);
    }
});

/**
 * GET /api/chef/profile
 * Get player profile with global stats
 */
router.get('/profile', requireAuth, async (req, res, next) => {
    try {
        const playerAddress = req.walletAddress;
        
        // Try cache first
        const cached = await getCachedProfile(playerAddress);
        if (cached) {
            return res.json({ ...cached, cached: true });
        }
        
        // Get chef info
        const hasChef = await chefNFT.playerHasChef(playerAddress);
        if (!hasChef) {
            return res.status(404).json({ error: 'No chef NFT found. Please mint a chef first.' });
        }
        
        const tokenId = await chefNFT.getChefTokenId(playerAddress);
        const name = await chefNFT.getChefName(tokenId);
        const energy = await chefNFT.getPlayerEnergy(playerAddress);
        
        // Get global stats from GameCore
        const globalStats = await gameCore.getPlayerGlobalStats(playerAddress);
        
        // Get current session if any
        const currentSessionId = await gameCore.getCurrentSessionId(playerAddress);
        
        const profile = {
            player: {
                address: playerAddress,
                name,
                tokenId: Number(tokenId),
                energy: Number(energy),
                maxEnergy: 300
            },
            globalStats: {
                totalSessionsPlayed: Number(globalStats.totalSessionsPlayed),
                totalMoves: Number(globalStats.totalMoves),
                totalRecipesSpawned: Number(globalStats.totalRecipesSpawned),
                totalRecipesDelivered: Number(globalStats.totalRecipesDelivered),
                totalRecipesDiscarded: Number(globalStats.totalRecipesDiscarded),
                totalWellCooked: Number(globalStats.totalWellCooked),
                totalOverCooked: Number(globalStats.totalOverCooked)
            },
            currentSessionId: Number(currentSessionId),
            cached: false
        };
        
        // Cache profile
        await setCachedProfile(playerAddress, profile);
        
        res.json(profile);
    } catch (error) {
        next(error);
    }
});

/**
 * POST /api/chef/refill-energy
 * Refill energy with USDC payment (player pays gas)
 */
router.post('/refill-energy', requireAuth, async (req, res, next) => {
    try {
        const playerAddress = req.walletAddress;
        
        console.log(`⚡ Refilling energy for ${playerAddress}`);
        
        // Check if player has chef
        const hasChef = await chefNFT.playerHasChef(playerAddress);
        if (!hasChef) {
            return res.status(400).json({ error: 'No chef NFT found' });
        }
        
        // Note: Player must have approved USDC beforehand
        // This transaction is signed and paid by the player, not sponsored
        
        // For Unity integration, the client will need to:
        // 1. Approve USDC spending to ChefNFT contract
        // 2. Call this endpoint
        // 3. We relay the refillEnergy() call
        
        // Since we're using Privy embedded wallets, the player can't sign directly in Unity
        // So we return instructions for manual USDC approval
        
        return res.status(400).json({
            error: 'Energy refill requires USDC approval',
            message: 'Please use the Unity client to approve USDC and refill energy',
            instructions: {
                step1: 'Approve USDC to ChefNFT contract',
                step2: 'Call refillEnergy() from client',
                chefNFTAddress: process.env.CHEFNFT_ADDRESS,
                usdcAmount: '100000' // 0.1 USDC (6 decimals)
            }
        });
        
        // TODO: Implement Privy signature request for USDC approval + refill
        
    } catch (error) {
        next(error);
    }
});

/**
 * PUT /api/chef/update-name
 * Update chef name (gas sponsored)
 */
router.put('/update-name', requireAuth, validateUpdateName, async (req, res, next) => {
    try {
        const { newName } = req.body;
        const playerAddress = req.walletAddress;
        
        console.log(`✏️  Updating name for ${playerAddress} to: ${newName}`);
        
        // Check if player has chef
        const hasChef = await chefNFT.playerHasChef(playerAddress);
        if (!hasChef) {
            return res.status(400).json({ error: 'No chef NFT found' });
        }
        
        // Update name (gas sponsored)
        const tx = await chefNFT.updateChefName(newName);
        const receipt = await tx.wait();
        
        res.json({
            success: true,
            txHash: receipt.hash,
            newName
        });
    } catch (error) {
        next(error);
    }
});

export default router;

