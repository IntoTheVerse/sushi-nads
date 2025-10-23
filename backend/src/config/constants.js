// Game configuration constants (must match smart contract values)

export const GAME_CONFIG = {
    DURATION: 1000, // seconds
    MOVE_COST: 1, // energy per move
    MAX_ENERGY: 300,
    ENERGY_REFILL_AMOUNT: 300,
    ENERGY_REFILL_COST_USDC: '100000', // 0.1 USDC (6 decimals)
    MIN_ENERGY_TO_START: 5, // minimum energy required to start session
};

// Scoring configuration (must match GameCore.sol)
export const SCORING_CONFIG = {
    deliveryBonus: 100,
    cookBonus: 10,
    discardPenalty: 50,
    overcookPenalty: 20,
    movePenalty: 1,
};

// Batching configuration
export const BATCH_CONFIG = {
    maxBatchSize: 10, // actions before auto-flush
    maxBatchInterval: 5000, // ms before auto-flush
};

// Cache TTL (time-to-live)
export const CACHE_TTL = {
    energy: 60000, // 1 minute
    session: 30000, // 30 seconds
    leaderboard: 300000, // 5 minutes
    profile: 120000, // 2 minutes
};

// Rate limiting
export const RATE_LIMITS = {
    loginAttempts: 5, // per 15 minutes
    apiCalls: 100, // per minute per IP
    gameActions: 20, // per second per player
};

export default {
    GAME_CONFIG,
    SCORING_CONFIG,
    BATCH_CONFIG,
    CACHE_TTL,
    RATE_LIMITS,
};

