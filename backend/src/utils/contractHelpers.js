import { chefNFT } from '../services/blockchain.service.js';

/**
 * Generic helper to safely call contract methods that might return empty data
 * Handles cases where contract call returns empty data (0x)
 * This can happen if the contract doesn't exist at the address,
 * the RPC is having issues, or the call is reverting silently
 */
async function safeContractCall(contractCall, defaultValue = null, errorMessage = 'Contract call') {
    try {
        return await contractCall();
    } catch (error) {
        // If the error is about decoding empty data, return default value
        if (error.code === 'BAD_DATA' && error.value === '0x') {
            console.log(`⚠️  ${errorMessage} returned empty data, using default value`);
            return defaultValue;
        }
        // For other errors, log and rethrow
        console.error(`❌ ${errorMessage} failed:`, error.message);
        throw error;
    }
}

/**
 * Helper function to safely check if player has chef
 * Handles cases where contract call returns empty data (0x)
 */
export async function safePlayerHasChef(playerAddress) {
    return await safeContractCall(
        () => chefNFT.playerHasChef(playerAddress),
        false,
        `Checking chef for ${playerAddress}`
    );
}

/**
 * Helper function to safely get chef token ID
 * Returns 0 if contract call fails or returns empty data
 */
export async function safeGetChefTokenId(playerAddress) {
    const result = await safeContractCall(
        () => chefNFT.getChefTokenId(playerAddress),
        0n,
        `Getting chef token ID for ${playerAddress}`
    );
    return typeof result === 'bigint' ? result : BigInt(result || 0);
}

/**
 * Helper function to safely get player energy
 * Returns 0 if contract call fails or returns empty data
 */
export async function safeGetPlayerEnergy(playerAddress) {
    const result = await safeContractCall(
        () => chefNFT.getPlayerEnergy(playerAddress),
        0n,
        `Getting energy for ${playerAddress}`
    );
    return typeof result === 'bigint' ? result : BigInt(result || 0);
}

/**
 * Helper function to safely get chef name
 * Returns empty string if contract call fails or returns empty data
 */
export async function safeGetChefName(tokenId) {
    return await safeContractCall(
        () => chefNFT.getChefName(tokenId),
        '',
        `Getting chef name for token ${tokenId}`
    );
}




