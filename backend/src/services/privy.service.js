import { PrivyClient } from '@privy-io/node';
import dotenv from 'dotenv';
import { activeNetwork } from './blockchain.service.js';

// Load environment variables first
dotenv.config();

// Initialize Privy client with authorization key for session signers
// See: https://docs.privy.io/wallets/using-wallets/session-signers/quickstart
const privy = new PrivyClient({
    appId: process.env.PRIVY_APP_ID,
    appSecret: process.env.PRIVY_APP_SECRET,
    walletApi: {
        // Authorization key allows backend to send transactions from user wallets
        authorizationPrivateKey: process.env.PRIVY_BACKEND_AUTH_PRIVATE_KEY
    }
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
 * Add session signer to user's wallet (allows backend to send txs on their behalf)
 * See: https://docs.privy.io/wallets/using-wallets/session-signers/add-session-signers
 * 
 * @param {string} walletId - User's Privy wallet ID
 * @returns {Promise<boolean>} Success status
 */
export async function addSessionSignerToWallet(walletId) {
    try {
        // Add backend authorization key as session signer
        await privy.wallets().addSessionSigner(walletId, {
            authorization_key_id: process.env.PRIVY_BACKEND_AUTH_KEY_ID,
            // Optional: Add policy IDs to restrict what backend can do
            policy_ids: []
        });
        
        console.log(`✅ Session signer added to wallet ${walletId}`);
        return true;
    } catch (error) {
        // May fail if already added - that's okay
        if (error.message?.includes('already exists')) {
            console.log(`ℹ️  Session signer already exists for wallet ${walletId}`);
            return true;
        }
        console.error('Failed to add session signer:', error.message);
        return false;
    }
}

/**
 * Send gas-sponsored transaction from user's wallet
 * See: https://docs.privy.io/wallets/gas-and-asset-management/gas/setup#ethereum-rest-api
 * 
 * @param {string} walletId - User's Privy wallet ID
 * @param {Object} txParams - {to, data, value}
 * @returns {Promise<Object>} Transaction response
 */
export async function sendSponsoredTransaction(walletId, txParams) {
    try {
        // Send transaction from user's wallet with gas sponsorship
        // Chain ID is dynamically determined from TARGET_NETWORK env variable
        const caip2 = `eip155:${activeNetwork.chainId}`;
        const response = await privy.wallets().ethereum().sendTransaction(walletId, {
            caip2, // Dynamic chain ID based on TARGET_NETWORK (84532 for Base, 10143 for Monad)
            params: {
                transaction: {
                    to: txParams.to,
                    data: txParams.data,
                    value: txParams.value || '0x0'
                }
            },
            sponsor: true // ← Privy sponsors gas!
        });
        
        console.log('✅ Sponsored tx sent:', response.transaction_hash);
        
        return {
            hash: response.transaction_hash,
            status: response.status
        };
    } catch (error) {
        console.error('❌ Sponsored tx failed:', error);
        throw error;
    }
}

export default privy;

