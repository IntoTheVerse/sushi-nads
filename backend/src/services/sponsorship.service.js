import privy from './privy.service.js';
import { gameCore, activeNetwork } from './blockchain.service.js';
import dotenv from 'dotenv';

dotenv.config();

/**
 * Privy Authorization Key for Backend
 * Create this in Privy Dashboard: Wallets → Authorization Keys
 * Store the private key securely
 */
const BACKEND_AUTH_KEY_ID = process.env.PRIVY_BACKEND_AUTH_KEY_ID || '';
const BACKEND_AUTH_PRIVATE_KEY = process.env.PRIVY_BACKEND_AUTH_PRIVATE_KEY || '';

/**
 * Add backend as authorized signer to user's wallet
 * This allows backend to send transactions on behalf of users
 * 
 * @param {string} walletId - User's Privy wallet ID
 * @returns {Promise<boolean>} Success status
 */
export async function addBackendAsSignerToWallet(walletId) {
    try {
        // Add authorization key as additional signer
        const response = await privy.wallets().update(walletId, {
            additional_signers: [
                {
                    authorization_key_id: BACKEND_AUTH_KEY_ID,
                    // Optional: Add policies to restrict what backend can do
                    override_policy_ids: []
                }
            ]
        });
        
        console.log(`✅ Backend added as signer to wallet ${walletId}`);
        return true;
    } catch (error) {
        console.error('Failed to add backend as signer:', error.message);
        return false;
    }
}

/**
 * Send sponsored transaction from user's wallet
 * 
 * @param {string} walletId - User's Privy wallet ID  
 * @param {Object} txParams - Transaction parameters {to, data, value}
 * @returns {Promise<Object>} Transaction receipt
 */
export async function sendSponsoredTransaction(walletId, txParams) {
    try {
        // Send transaction via Privy with gas sponsorship
        // See: https://docs.privy.io/wallets/gas-and-asset-management/gas/setup#ethereum-rest-api
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
        
        console.log('✅ Sponsored transaction sent:', response.transaction_hash);
        
        return {
            hash: response.transaction_hash,
            status: response.status
        };
    } catch (error) {
        console.error('Sponsored transaction failed:', error.message);
        throw error;
    }
}

/**
 * Helper: Encode function call for GameCore contract
 * 
 * @param {string} functionName - Function to call
 * @param {Array} args - Function arguments
 * @returns {string} Encoded data
 */
export function encodeGameCoreFunction(functionName, args) {
    return gameCore.interface.encodeFunctionData(functionName, args);
}

/**
 * Send sponsored game action
 * 
 * @param {string} walletId - User's wallet ID
 * @param {string} functionName - GameCore function name
 * @param {Array} args - Function arguments
 * @returns {Promise<Object>} Transaction receipt
 */
export async function sendSponsoredGameAction(walletId, functionName, args = []) {
    const data = encodeGameCoreFunction(functionName, args);
    
    return await sendSponsoredTransaction(walletId, {
        to: process.env.GAMECORE_ADDRESS,
        data,
        value: '0x0'
    });
}

export default {
    addBackendAsSignerToWallet,
    sendSponsoredTransaction,
    sendSponsoredGameAction,
    encodeGameCoreFunction,
};

