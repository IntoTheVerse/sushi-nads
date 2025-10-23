import { verifyAuthToken } from '../services/privy.service.js';

/**
 * Middleware to require Privy authentication
 * Verifies the Privy auth token and attaches user info to request
 */
export async function requireAuth(req, res, next) {
    try {
        const authHeader = req.headers.authorization;
        
        if (!authHeader || !authHeader.startsWith('Bearer ')) {
            return res.status(401).json({ 
                error: 'No authorization token provided',
                message: 'Include Authorization: Bearer <token> header'
            });
        }
        
        const token = authHeader.replace('Bearer ', '');
        
        // Verify token with Privy
        const user = await verifyAuthToken(token);
        
        // Extract wallet address
        const walletAddress = user.wallet?.address || 
                             user.linked_accounts?.find(a => a.type === 'wallet')?.address;
        
        if (!walletAddress) {
            return res.status(400).json({ 
                error: 'No wallet found for user',
                message: 'User must have an embedded wallet'
            });
        }
        
        // Attach user info to request
        req.user = user;
        req.userId = user.id;
        req.walletAddress = walletAddress.toLowerCase();
        
        next();
    } catch (error) {
        console.error('Authentication error:', error.message);
        res.status(401).json({ 
            error: 'Invalid or expired token',
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

