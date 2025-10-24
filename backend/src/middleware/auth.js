import { verifyAuthToken } from '../services/privy.service.js';

/**
 * Middleware to require authentication
 * 
 * HACKATHON MODE: Accepts wallet address from request body
 * TODO: Re-enable Privy token verification for production
 */
export async function requireAuth(req, res, next) {
    try {
        // HACKATHON WORKAROUND: Use wallet address from request
        // Privy Unity SDK doesn't expose auth tokens easily
        const walletAddress = req.body.walletAddress || req.query.walletAddress;
        
        if (!walletAddress) {
            return res.status(400).json({ 
                error: 'Wallet address required',
                message: 'Include walletAddress in request body or query params'
            });
        }
        
        // Validate format
        if (!walletAddress.match(/^0x[a-fA-F0-9]{40}$/)) {
            return res.status(400).json({ 
                error: 'Invalid wallet address format',
                message: 'Must be a valid Ethereum address'
            });
        }
        
        // Create minimal user object
        req.user = {
            wallet: { address: walletAddress.toLowerCase() },
            linked_accounts: [{ type: 'wallet', address: walletAddress.toLowerCase() }]
        };
        req.walletAddress = walletAddress.toLowerCase();
        
        console.log(`⚠️  [HACKATHON MODE] Auth for wallet: ${walletAddress}`);
        
        next();
        
        /* TODO: Re-enable for production
        const authHeader = req.headers.authorization;
        
        if (!authHeader || !authHeader.startsWith('Bearer ')) {
            return res.status(401).json({ 
                error: 'No authorization token provided',
                message: 'Include Authorization: Bearer <token> header'
            });
        }
        
        const token = authHeader.replace('Bearer ', '');
        const user = await verifyAuthToken(token);
        
        const walletAddress = user.wallet?.address || 
                             user.linked_accounts?.find(a => a.type === 'wallet')?.address;
        
        if (!walletAddress) {
            return res.status(400).json({ 
                error: 'No wallet found for user',
                message: 'User must have an embedded wallet'
            });
        }
        
        req.user = user;
        req.userId = user.id;
        req.walletAddress = walletAddress.toLowerCase();
        
        next();
        */
    } catch (error) {
        console.error('Authentication error:', error.message);
        res.status(500).json({ 
            error: 'Authentication failed',
            message: error.message 
        });
    }
}

/**
 * Optional auth - doesn't fail if no token provided
 * Useful for public endpoints that benefit from user context
 */
export async function optionalAuth(req, res, next) {
    try {
        const authHeader = req.headers.authorization;
        
        if (authHeader && authHeader.startsWith('Bearer ')) {
            const token = authHeader.replace('Bearer ', '');
            const user = await verifyAuthToken(token);
            
            req.user = user;
            req.userId = user.id;
            req.walletAddress = user.wallet?.address?.toLowerCase();
        }
    } catch (error) {
        // Silently fail for optional auth
        console.warn('Optional auth failed:', error.message);
    }
    
    next();
}

export default {
    requireAuth,
    optionalAuth,
};

