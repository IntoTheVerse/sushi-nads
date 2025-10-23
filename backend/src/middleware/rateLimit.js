import { RATE_LIMITS } from '../config/constants.js';

// In-memory store for rate limiting (use Redis in production)
const rateLimitStore = new Map();

/**
 * Rate limit by IP address
 */
export function rateLimitByIP(req, res, next) {
    const ip = req.ip || req.connection.remoteAddress;
    const now = Date.now();
    const windowMs = 60000; // 1 minute
    
    const key = `ip:${ip}`;
    const record = rateLimitStore.get(key) || { count: 0, resetTime: now + windowMs };
    
    if (now > record.resetTime) {
        // Reset window
        record.count = 1;
        record.resetTime = now + windowMs;
    } else {
        record.count++;
    }
    
    rateLimitStore.set(key, record);
    
    // Clean up old entries periodically
    if (Math.random() < 0.01) {
        cleanupOldEntries();
    }
    
    if (record.count > RATE_LIMITS.apiCalls) {
        return res.status(429).json({ 
            error: 'Too many requests',
            message: `Limit: ${RATE_LIMITS.apiCalls} requests per minute`,
            retryAfter: Math.ceil((record.resetTime - now) / 1000)
        });
    }
    
    // Add rate limit headers
    res.setHeader('X-RateLimit-Limit', RATE_LIMITS.apiCalls);
    res.setHeader('X-RateLimit-Remaining', Math.max(0, RATE_LIMITS.apiCalls - record.count));
    res.setHeader('X-RateLimit-Reset', Math.ceil(record.resetTime / 1000));
    
    next();
}

/**
 * Rate limit game actions by player address
 */
export function rateLimitGameActions(req, res, next) {
    const address = req.walletAddress;
    
    if (!address) {
        return next(); // No wallet = let auth middleware handle it
    }
    
    const now = Date.now();
    const windowMs = 1000; // 1 second
    
    const key = `game:${address}`;
    const record = rateLimitStore.get(key) || { count: 0, resetTime: now + windowMs };
    
    if (now > record.resetTime) {
        record.count = 1;
        record.resetTime = now + windowMs;
    } else {
        record.count++;
    }
    
    rateLimitStore.set(key, record);
    
    if (record.count > RATE_LIMITS.gameActions) {
        return res.status(429).json({ 
            error: 'Too many game actions',
            message: `Slow down! Limit: ${RATE_LIMITS.gameActions} actions per second`,
            retryAfter: Math.ceil((record.resetTime - now) / 1000)
        });
    }
    
    next();
}

/**
 * Clean up old rate limit entries
 */
function cleanupOldEntries() {
    const now = Date.now();
    const keysToDelete = [];
    
    for (const [key, record] of rateLimitStore.entries()) {
        if (now > record.resetTime + 60000) { // 1 minute past reset
            keysToDelete.push(key);
        }
    }
    
    keysToDelete.forEach(key => rateLimitStore.delete(key));
    
    if (keysToDelete.length > 0) {
        console.log(`🧹 Cleaned up ${keysToDelete.length} old rate limit entries`);
    }
}

export default {
    rateLimitByIP,
    rateLimitGameActions,
};

