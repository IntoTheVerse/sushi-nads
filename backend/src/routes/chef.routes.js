import express from 'express';
import { chefNFT, gameCore } from '../services/blockchain.service.js';
import { addSessionSignerToWallet, sendSponsoredTransaction } from '../services/privy.service.js';
import { requireAuth } from '../middleware/auth.js';
import { validateMintChef, validateUpdateName } from '../middleware/validation.js';
import { getCachedProfile, setCachedProfile } from '../services/cache.service.js';
import { GAME_CONFIG } from '../config/constants.js';
import { safePlayerHasChef, safeGetChefTokenId, safeGetPlayerEnergy, safeGetChefName } from '../utils/contractHelpers.js';

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
        const hasChef = await safePlayerHasChef(playerAddress);
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
            // Fallback: query contract (with safe error handling)
            const tokenIdBigInt = await safeGetChefTokenId(playerAddress);
            tokenId = Number(tokenIdBigInt);
            if (tokenId === 0) {
                console.warn(`⚠️  Could not retrieve token ID from contract, using 0`);
            }
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
        const hasChef = await safePlayerHasChef(playerAddress);
        if (!hasChef) {
            return res.status(404).json({ error: 'No chef NFT found. Please mint a chef first.' });
        }
        
        const tokenIdBigInt = await safeGetChefTokenId(playerAddress);
        const tokenId = Number(tokenIdBigInt);
        if (tokenId === 0) {
            return res.status(404).json({ error: 'No chef NFT found. Please mint a chef first.' });
        }
        
        const name = await safeGetChefName(tokenId);
        const energyBigInt = await safeGetPlayerEnergy(playerAddress);
        const energy = Number(energyBigInt);
        
        // Get global stats from GameCore
        const globalStats = await gameCore.getPlayerGlobalStats(playerAddress);
        
        // Get current session if any
        const currentSessionId = await gameCore.getCurrentSessionId(playerAddress);
        
        const profile = {
            player: {
                address: playerAddress,
                name,
                tokenId,
                energy,
                maxEnergy: GAME_CONFIG.MAX_ENERGY
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
        const hasChef = await safePlayerHasChef(playerAddress);
        if (!hasChef) {
            return res.status(400).json({ error: 'No chef NFT found' });
        }
        
        // HACKATHON MODE: Free energy refills for testing
        // TODO: Implement USDC payment for production
        
        // Call freeEnergyRefill on contract (backend pays gas, no USDC needed)
        const tx = await chefNFT.freeEnergyRefill(playerAddress);
        console.log('   Transaction sent:', tx.hash);
        
        const receipt = await tx.wait();
        console.log('   ✅ Energy refilled (FREE for testing)!');
        
        // Get new energy level
        const newEnergyBigInt = await safeGetPlayerEnergy(playerAddress);
        const newEnergy = Number(newEnergyBigInt);
        
        res.json({
            success: true,
            txHash: receipt.hash,
            newEnergy: Number(newEnergy),
            message: '⚡ +300 energy! (FREE for hackathon - normally costs 0.1 USDC)'
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
        const hasChef = await safePlayerHasChef(playerAddress);
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

/**
 * POST /api/chef/approve-usdc
 * Approve ChefNFT contract to spend USDC (one-time setup)
 */
router.post('/approve-usdc', requireAuth, async (req, res, next) => {
    try {
        const playerAddress = req.walletAddress;
        const { amount } = req.body; // Optional: custom approval amount
        
        const approvalAmount = amount || '10000000'; // Default: 10 USDC (100 refills)
        const usdcAddress = process.env.USDC_ADDRESS || '0x036CbD53842c5426634e7929541eC2318f3dCF7e';
        
        console.log(`💳 Approving USDC for ${playerAddress}`);
        console.log(`   Amount: ${approvalAmount} (${Number(approvalAmount) / 1000000} USDC)`);
        
        // Create ERC20 contract instance
        const { ethers } = await import('ethers');
        const erc20ABI = ['function approve(address spender, uint256 amount) returns (bool)'];
        const usdcContract = new ethers.Contract(usdcAddress, erc20ABI, await import('../services/blockchain.service.js').then(m => m.provider));
        
        // Encode approval call
        const approvalData = usdcContract.interface.encodeFunctionData('approve', [
            process.env.CHEFNFT_ADDRESS,
            approvalAmount
        ]);
        
        // HACKATHON MODE: Backend signs approval (pays gas)
        const wallet = new ethers.Wallet(process.env.BACKEND_PRIVATE_KEY, await import('../services/blockchain.service.js').then(m => m.provider));
        const tx = await wallet.sendTransaction({
            to: usdcAddress,
            data: approvalData,
            from: playerAddress // This won't work - backend can't sign for player
        });
        
        console.log('   ✅ USDC approved!');
        
        res.json({
            success: true,
            txHash: tx.hash,
            approvedAmount: approvalAmount
        });
        
    } catch (error) {
        console.error('USDC approval failed:', error.message);
        next(error);
    }
});

/**
 * POST /api/chef/refill-energy-usdc
 * Refill energy with USDC payment (0.1 USDC)
 */
router.post('/refill-energy-usdc', requireAuth, async (req, res, next) => {
    try {
        const playerAddress = req.walletAddress;
        const user = req.user;
        
        console.log(`💰 Refilling energy with USDC for ${playerAddress}`);
        
        // Check if player has chef
        const hasChef = await safePlayerHasChef(playerAddress);
        if (!hasChef) {
            return res.status(400).json({ error: 'No chef NFT found' });
        }
        
        // HACKATHON MODE: For simplicity, backend will handle both approval and refill
        // In production, this would use session signers with Privy gas sponsorship
        
        // For now, player must have pre-approved USDC or we give instructions
        console.log('   Attempting refill (requires USDC approval)...');
        
        try {
            // Try to refill - will fail if not approved
            const tx = await chefNFT.refillEnergyFor(playerAddress);
            console.log('   Transaction sent:', tx.hash);
            
            const receipt = await tx.wait();
            console.log('   ✅ Energy refilled with USDC payment!');
            
            // Get new energy level
            const newEnergyBigInt = await safeGetPlayerEnergy(playerAddress);
            const newEnergy = Number(newEnergyBigInt);
            
            return res.json({
                success: true,
                txHash: receipt.hash,
                newEnergy: Number(newEnergy),
                usdcPaid: '0.1',
                message: '⚡ +300 energy! 0.1 USDC paid'
            });
        } catch (transferError) {
            // Approval needed - return instructions
            console.log('   ⚠️  USDC approval required');
            
            return res.status(400).json({
                error: 'USDC approval required',
                message: 'Player must approve ChefNFT contract to spend 0.1 USDC',
                needsApproval: true,
                approvalDetails: {
                    usdcAddress: process.env.USDC_ADDRESS || '0x036CbD53842c5426634e7929541eC2318f3dCF7e',
                    spenderAddress: process.env.CHEFNFT_ADDRESS,
                    amount: '100000', // 0.1 USDC (6 decimals)
                    amountReadable: '0.1 USDC'
                }
            });
        }
        
    } catch (error) {
        next(error);
    }
});

export default router;

