import { PrivyClient } from '@privy-io/server-auth';
import dotenv from 'dotenv';

// Load environment variables first
dotenv.config();

// Initialize Privy client
const privy = new PrivyClient(
    process.env.PRIVY_APP_ID,
    process.env.PRIVY_APP_SECRET
);

console.log('🔐 Privy Service Initialized');
console.log('   App ID:', process.env.PRIVY_APP_ID);

/**
 * Verify Privy authentication token
 * @param {string} token - Privy auth token from Unity client
 * @returns {Promise<Object>} User object
 */
export async function verifyAuthToken(token) {
    try {
        const user = await privy.verifyAuthToken(token);
        return user;
    } catch (error) {
        console.error('Auth verification failed:', error.message);
        throw new Error('Invalid authentication token');
    }
}

/**
 * Get user by ID
 * @param {string} userId - Privy user ID
 * @returns {Promise<Object>} User object
 */
export async function getUserById(userId) {
    try {
        return await privy.getUser(userId);
    } catch (error) {
        console.error('Get user failed:', error.message);
        throw error;
    }
}

/**
 * Get user by wallet address
 * @param {string} address - Wallet address
 * @returns {Promise<Object>} User object
 */
export async function getUserByWalletAddress(address) {
    try {
        return await privy.getUserByWalletAddress(address);
    } catch (error) {
        console.error('Get user by wallet failed:', error.message);
        return null;
    }
}

/**
 * Relay transaction with Privy gas sponsorship
 * Note: According to Privy docs, gas sponsorship for Unity requires server relay
 * @param {Object} txParams - Transaction parameters
 * @returns {Promise<Object>} Transaction receipt
 */
export async function sponsorTransaction(txParams) {
    try {
        // Privy's relayTransaction for gas sponsorship
        // See: https://docs.privy.io/wallets/gas-and-asset-management/gas/ethereum#nodejs
        const sponsoredTx = await privy.relayTransaction({
            chain: 'base-sepolia',
            transaction: txParams
        });
        
        console.log('✅ Transaction sponsored:', sponsoredTx.hash);
        return sponsoredTx;
    } catch (error) {
        console.error('❌ Gas sponsorship failed:', error.message);
        throw error;
    }
}

export default privy;

