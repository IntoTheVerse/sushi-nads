import { PrivyClient } from '@privy-io/node';
import dotenv from 'dotenv';

// Load environment variables first
dotenv.config();

// Initialize Privy client (NEW API)
const privy = new PrivyClient({
    appId: process.env.PRIVY_APP_ID,
    appSecret: process.env.PRIVY_APP_SECRET
});

console.log('🔐 Privy Service Initialized');
console.log('   App ID:', process.env.PRIVY_APP_ID);

/**
 * Verify Privy authentication token (NEW API)
 * @param {string} token - Privy auth token from Unity client
 * @returns {Promise<Object>} User object
 */
export async function verifyAuthToken(token) {
    try {
        // New API: privy.verifyAuthToken(accessToken)
        const claims = await privy.verifyAuthToken(token);
        
        // Get full user data if needed
        const user = await privy.users().getByUserId(claims.userId);
        
        // Return user with wallet info
        return {
            id: user.id,
            wallet: user.linked_accounts?.find(a => a.type === 'wallet'),
            email: user.linked_accounts?.find(a => a.type === 'email'),
            linked_accounts: user.linked_accounts,
            created_at: user.created_at
        };
    } catch (error) {
        console.error('Auth verification failed:', error.message);
        throw new Error('Invalid authentication token');
    }
}

/**
 * Get user by ID (NEW API)
 * @param {string} userId - Privy user ID
 * @returns {Promise<Object>} User object
 */
export async function getUserById(userId) {
    try {
        return await privy.users().getByUserId(userId);
    } catch (error) {
        console.error('Get user failed:', error.message);
        throw error;
    }
}

/**
 * Get user by wallet address (NEW API)
 * @param {string} address - Wallet address
 * @returns {Promise<Object>} User object
 */
export async function getUserByWalletAddress(address) {
    try {
        return await privy.users().getByWalletAddress(address);
    } catch (error) {
        console.error('Get user by wallet failed:', error.message);
        return null;
    }
}

/**
 * Send transaction with gas sponsorship using wallet
 * Note: With @privy-io/node, we use wallet.sendTransaction with gas sponsorship enabled in dashboard
 * @param {string} walletId - Privy wallet ID
 * @param {Object} txParams - Transaction parameters
 * @returns {Promise<Object>} Transaction receipt
 */
export async function sendSponsoredTransaction(walletId, txParams) {
    try {
        // New API: Use wallets().ethereum().sendTransaction()
        // Gas sponsorship is automatic if enabled in dashboard for the contract
        const response = await privy.wallets().ethereum().sendTransaction(walletId, {
            caip2: 'eip155:84532', // Base Sepolia
            params: {
                transaction: txParams
            }
        });
        
        console.log('✅ Transaction sent:', response.hash);
        return response;
    } catch (error) {
        console.error('❌ Transaction failed:', error.message);
        throw error;
    }
}

/**
 * Create an embedded wallet for a user
 * @param {string} userId - Privy user ID
 * @returns {Promise<Object>} Wallet object
 */
export async function createWalletForUser(userId) {
    try {
        const wallet = await privy.wallets().create({
            chain_type: 'ethereum'
        });
        
        console.log('✅ Wallet created:', wallet.address);
        return wallet;
    } catch (error) {
        console.error('❌ Wallet creation failed:', error.message);
        throw error;
    }
}

export default privy;

