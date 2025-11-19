import express from 'express';
import { activeNetwork } from '../services/blockchain.service.js';

const router = express.Router();

/**
 * GET /api/config/network
 * Returns current network configuration including explorer URL
 * Used by Unity client to determine which blockchain explorer to use
 */
router.get('/network', (req, res) => {
    try {
        res.json({
            success: true,
            network: {
                key: activeNetwork.key,
                name: activeNetwork.name,
                chainId: activeNetwork.chainId,
                explorerUrl: activeNetwork.explorerUrl,
                contracts: activeNetwork.contracts,
            },
        });
    } catch (error) {
        console.error('❌ Error fetching network config:', error);
        res.status(500).json({
            success: false,
            error: 'Failed to fetch network configuration',
            message: error.message,
        });
    }
});

export default router;

