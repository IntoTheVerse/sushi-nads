import { ethers } from 'ethers';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Load ABIs
const loadABI = (contractName) => {
    const abiPath = path.join(__dirname, '..', 'abis', `${contractName}.json`);
    const abiFile = fs.readFileSync(abiPath, 'utf8');
    return JSON.parse(abiFile).abi;
};

// Initialize provider
const provider = new ethers.JsonRpcProvider(
    process.env.BASE_SEPOLIA_RPC_URL || 'https://sepolia.base.org'
);

// Initialize backend signer
const signer = new ethers.Wallet(process.env.BACKEND_PRIVATE_KEY, provider);

console.log('🔗 Blockchain Service Initialized');
console.log('   Network: Base Sepolia');
console.log('   Signer:', signer.address);

// Load contract ABIs
const ChefNFTABI = loadABI('ChefNFT');
const GameCoreABI = loadABI('GameCore');
const LeaderboardABI = loadABI('Leaderboard');

// Initialize contract instances
export const chefNFT = new ethers.Contract(
    process.env.CHEFNFT_ADDRESS,
    ChefNFTABI,
    signer
);

export const gameCore = new ethers.Contract(
    process.env.GAMECORE_ADDRESS,
    GameCoreABI,
    signer
);

export const leaderboard = new ethers.Contract(
    process.env.LEADERBOARD_ADDRESS,
    LeaderboardABI,
    signer
);

// Helper functions
export async function waitForTransaction(tx) {
    const receipt = await tx.wait();
    return receipt;
}

export async function getTransactionGasCost(receipt) {
    const gasUsed = receipt.gasUsed;
    const gasPrice = receipt.effectiveGasPrice || receipt.gasPrice;
    return ethers.formatEther(gasUsed * gasPrice);
}

export async function checkNetworkConnection() {
    try {
        const network = await provider.getNetwork();
        const blockNumber = await provider.getBlockNumber();
        
        console.log('✅ Network connection verified');
        console.log('   Chain ID:', network.chainId.toString());
        console.log('   Block number:', blockNumber);
        
        return true;
    } catch (error) {
        console.error('❌ Network connection failed:', error.message);
        return false;
    }
}

export default {
    provider,
    signer,
    chefNFT,
    gameCore,
    leaderboard,
    waitForTransaction,
    getTransactionGasCost,
    checkNetworkConnection,
};

