require("@nomicfoundation/hardhat-toolbox");
require("@nomicfoundation/hardhat-verify");
const path = require("path");
const fs = require("fs");

const envPath = path.resolve(__dirname, ".env");
if (!fs.existsSync(envPath)) {
  console.warn("[Hardhat] WARNING: .env not found at", envPath);
}
require("dotenv").config({ path: envPath });

const normalizePrivateKey = (key) => {
  if (!key) return undefined;
  return key.startsWith("0x") ? key : `0x${key}`;
};

// DEPLOYER_PRIVATE_KEY overrides PRIVATE_KEY (useful if .env has wrong key)
const deployerKey = normalizePrivateKey(process.env.DEPLOYER_PRIVATE_KEY || process.env.PRIVATE_KEY);
const sharedAccounts = deployerKey ? [deployerKey] : [];
const etherscanApiKey =
  process.env.ETHERSCAN_API_KEY ||
  process.env.BASESCAN_API_KEY ||
  "PLACEHOLDER";

/** @type import('hardhat/config').HardhatUserConfig */
module.exports = {
  solidity: {
    version: "0.8.28",
    settings: {
      metadata: {
        bytecodeHash: "none",
        useLiteralContent: true,
      },
      optimizer: {
        enabled: true,
        runs: 200,
      },
      viaIR: true,
    },
  },
  networks: {
    hardhat: {
      chainId: 31337,
    },
    baseSepolia: {
      url: process.env.BASE_SEPOLIA_RPC_URL || "https://sepolia.base.org",
      accounts: sharedAccounts,
      chainId: 84532,
      gasPrice: 1000000000, // 1 gwei
    },
    monadTestnet: {
      url: process.env.MONAD_RPC_URL || "https://testnet-rpc.monad.xyz",
      accounts: sharedAccounts,
      chainId: 10143,
    },
    localhost: {
      url: "http://127.0.0.1:8545",
      chainId: 31337,
    },
  },
  etherscan: {
    apiKey: etherscanApiKey,
    customChains: [
      {
        network: "baseSepolia",
        chainId: 84532,
        urls: {
          apiURL: "https://api-sepolia.basescan.org/api",
          browserURL: "https://sepolia.basescan.org",
        },
      },
    ],
  },
  sourcify: {
    enabled: true,
    apiUrl:
      process.env.MONAD_SOURCIFY_API_URL ||
      "https://sourcify-api-monad.blockvision.org",
    browserUrl:
      process.env.MONAD_SOURCIFY_BROWSER_URL ||
      "https://testnet.monadexplorer.com",
  },
  gasReporter: {
    enabled: process.env.REPORT_GAS === "true",
    currency: "USD",
    coinmarketcap: process.env.COINMARKETCAP_API_KEY,
  },
  paths: {
    sources: "./contracts",
    tests: "./test",
    cache: "./cache",
    artifacts: "./artifacts",
  },
};

