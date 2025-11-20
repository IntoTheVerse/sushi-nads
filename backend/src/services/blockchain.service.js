import { ethers } from 'ethers';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import dotenv from 'dotenv';

// Load environment variables first
dotenv.config();

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const NETWORKS = {
    base: {
        name: 'Base Sepolia',
        chainId: 84532,
        rpcUrl: process.env.BASE_SEPOLIA_RPC_URL || 'https://sepolia.base.org',
        explorerUrl: 'https://sepolia.basescan.org',
        contracts: {
            chefNFT: process.env.CHEFNFT_ADDRESS,
            gameCore: process.env.GAMECORE_ADDRESS,
            leaderboard: process.env.LEADERBOARD_ADDRESS,
        },
    },
    monad: {
        name: 'Monad Testnet',
        chainId: 10143,
        rpcUrl: process.env.MONAD_RPC_URL || 'https://testnet-rpc.monad.xyz',
        explorerUrl: 'https://testnet.monadexplorer.com',
        contracts: {
            chefNFT: process.env.MONAD_CHEFNFT_ADDRESS,
            gameCore: process.env.MONAD_GAMECORE_ADDRESS,
            leaderboard: process.env.MONAD_LEADERBOARD_ADDRESS,
        },
    },
};

const targetNetwork =
    (process.env.TARGET_NETWORK || 'base').toLowerCase() in NETWORKS
        ? (process.env.TARGET_NETWORK || 'base').toLowerCase()
        : 'base';

// Load ABIs per network
const loadABI = (contractName) => {
    const abiPath = path.join(
        __dirname,
        '..',
        'abis',
        targetNetwork,
        `${contractName}.json`
    );

    if (!fs.existsSync(abiPath)) {
        throw new Error(
            `[BlockchainService] Missing ABI for ${contractName} on network "${targetNetwork}".`
        );
    }

    const abiFile = fs.readFileSync(abiPath, 'utf8');
    return JSON.parse(abiFile).abi;
};

const { name: networkName, chainId, rpcUrl, explorerUrl, contracts } = NETWORKS[targetNetwork];

if (!contracts.chefNFT || !contracts.gameCore || !contracts.leaderboard) {
    throw new Error(
        `[BlockchainService] Missing contract addresses for ${networkName}. ` +
            'Check your environment variables.'
    );
}

// Initialize provider
const provider = new ethers.JsonRpcProvider(rpcUrl);

// Initialize backend signer (add 0x prefix if missing)
const privateKey = process.env.BACKEND_PRIVATE_KEY;
const formattedKey = privateKey.startsWith('0x') ? privateKey : `0x${privateKey}`;
export const signer = new ethers.Wallet(formattedKey, provider);

console.log('🔗 Blockchain Service Initialized');
console.log(`   Network: ${networkName}`);
console.log('   Signer:', signer.address);

export const activeNetwork = {
    key: targetNetwork,
    name: networkName,
    chainId,
    explorerUrl,
    contracts: { ...contracts },
};

// Load contract ABIs
const ChefNFTABI = loadABI('ChefNFT');
const GameCoreABI = loadABI('GameCore');
const LeaderboardABI = loadABI('Leaderboard');

// Initialize contract instances
export const chefNFT = new ethers.Contract(
    contracts.chefNFT,
    ChefNFTABI,
    signer
);

export const gameCore = new ethers.Contract(
    contracts.gameCore,
    GameCoreABI,
    signer
);

export const leaderboard = new ethers.Contract(
    contracts.leaderboard,
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

