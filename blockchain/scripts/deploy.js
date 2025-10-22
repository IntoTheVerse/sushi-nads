const hre = require("hardhat");
const { ethers } = require("hardhat");

async function main() {
  console.log("🚀 Starting Sushi Bonks Contract Deployment...\n");

  // Get deployer account
  const [deployer] = await ethers.getSigners();
  console.log("📍 Deploying contracts with account:", deployer.address);
  console.log("💰 Account balance:", ethers.formatEther(await ethers.provider.getBalance(deployer.address)), "ETH\n");

  // Configuration
  const USDC_ADDRESS = process.env.USDC_ADDRESS || "0x036CbD53842c5426634e7929541eC2318f3dCF7e"; // Base Sepolia USDC
  const ENERGY_REFILL_COST = ethers.parseUnits("0.1", 6); // 0.1 USDC (6 decimals)
  const BACKEND_SIGNER = process.env.BACKEND_SIGNER_ADDRESS || deployer.address;

  console.log("⚙️  Configuration:");
  console.log("   USDC Address:", USDC_ADDRESS);
  console.log("   Energy Refill Cost:", ethers.formatUnits(ENERGY_REFILL_COST, 6), "USDC");
  console.log("   Backend Signer:", BACKEND_SIGNER);
  console.log();

  // ============================================
  // 1. Deploy ChefNFT
  // ============================================
  console.log("📝 Deploying ChefNFT...");
  const ChefNFT = await ethers.getContractFactory("ChefNFT");
  const chefNFT = await ChefNFT.deploy(USDC_ADDRESS, ENERGY_REFILL_COST);
  await chefNFT.waitForDeployment();
  const chefNFTAddress = await chefNFT.getAddress();
  console.log("✅ ChefNFT deployed to:", chefNFTAddress);
  console.log();

  // ============================================
  // 2. Deploy GameCore
  // ============================================
  console.log("📝 Deploying GameCore...");
  const GameCore = await ethers.getContractFactory("GameCore");
  const gameCore = await GameCore.deploy(chefNFTAddress, BACKEND_SIGNER);
  await gameCore.waitForDeployment();
  const gameCoreAddress = await gameCore.getAddress();
  console.log("✅ GameCore deployed to:", gameCoreAddress);
  console.log();

  // ============================================
  // 3. Deploy Leaderboard
  // ============================================
  console.log("📝 Deploying Leaderboard...");
  const Leaderboard = await ethers.getContractFactory("Leaderboard");
  const leaderboard = await Leaderboard.deploy();
  await leaderboard.waitForDeployment();
  const leaderboardAddress = await leaderboard.getAddress();
  console.log("✅ Leaderboard deployed to:", leaderboardAddress);
  console.log();

  // ============================================
  // 4. Setup Contract Connections
  // ============================================
  console.log("🔗 Setting up contract connections...");
  
  // Set GameCore address in ChefNFT
  console.log("   Setting GameCore in ChefNFT...");
  const setGameCoreTx = await chefNFT.setGameCore(gameCoreAddress);
  await setGameCoreTx.wait();
  console.log("   ✓ GameCore address set in ChefNFT");

  // Set Leaderboard address in GameCore
  console.log("   Setting Leaderboard in GameCore...");
  const setLeaderboardTx = await gameCore.setLeaderboard(leaderboardAddress);
  await setLeaderboardTx.wait();
  console.log("   ✓ Leaderboard address set in GameCore");

  // Set GameCore address in Leaderboard
  console.log("   Setting GameCore in Leaderboard...");
  const setGameCoreInLeaderboardTx = await leaderboard.setGameCore(gameCoreAddress);
  await setGameCoreInLeaderboardTx.wait();
  console.log("   ✓ GameCore address set in Leaderboard");
  console.log();

  // ============================================
  // 5. Verify Setup
  // ============================================
  console.log("🔍 Verifying deployment...");
  
  const gameCoreInChefNFT = await chefNFT.gameCoreAddress();
  const leaderboardInGameCore = await gameCore.leaderboard();
  const gameCoreInLeaderboard = await leaderboard.gameCoreAddress();
  
  console.log("   ChefNFT -> GameCore:", gameCoreInChefNFT);
  console.log("   GameCore -> Leaderboard:", leaderboardInGameCore);
  console.log("   Leaderboard -> GameCore:", gameCoreInLeaderboard);
  
  const isSetupCorrect = 
    gameCoreInChefNFT === gameCoreAddress &&
    leaderboardInGameCore === leaderboardAddress &&
    gameCoreInLeaderboard === gameCoreAddress;
  
  if (isSetupCorrect) {
    console.log("   ✅ All connections verified!");
  } else {
    console.log("   ⚠️  Warning: Some connections may be incorrect");
  }
  console.log();

  // ============================================
  // 6. Summary
  // ============================================
  console.log("📋 Deployment Summary");
  console.log("=====================================");
  console.log("ChefNFT:      ", chefNFTAddress);
  console.log("GameCore:     ", gameCoreAddress);
  console.log("Leaderboard:  ", leaderboardAddress);
  console.log("=====================================");
  console.log();

  // ============================================
  // 7. Save Deployment Info
  // ============================================
  const deploymentInfo = {
    network: hre.network.name,
    chainId: (await ethers.provider.getNetwork()).chainId.toString(),
    deployer: deployer.address,
    timestamp: new Date().toISOString(),
    contracts: {
      ChefNFT: chefNFTAddress,
      GameCore: gameCoreAddress,
      Leaderboard: leaderboardAddress,
    },
    config: {
      usdcAddress: USDC_ADDRESS,
      energyRefillCost: ENERGY_REFILL_COST.toString(),
      backendSigner: BACKEND_SIGNER,
    },
  };

  const fs = require("fs");
  const path = require("path");
  
  const deploymentsDir = path.join(__dirname, "..", "deployments");
  if (!fs.existsSync(deploymentsDir)) {
    fs.mkdirSync(deploymentsDir, { recursive: true });
  }
  
  const filename = `deployment-${hre.network.name}-${Date.now()}.json`;
  const filepath = path.join(deploymentsDir, filename);
  
  fs.writeFileSync(filepath, JSON.stringify(deploymentInfo, null, 2));
  console.log("💾 Deployment info saved to:", filepath);
  console.log();

  // ============================================
  // 8. Verification Instructions
  // ============================================
  if (hre.network.name !== "hardhat" && hre.network.name !== "localhost") {
    console.log("📝 To verify contracts on Basescan, run:");
    console.log();
    console.log(`npx hardhat verify --network ${hre.network.name} ${chefNFTAddress} "${USDC_ADDRESS}" "${ENERGY_REFILL_COST}"`);
    console.log();
    console.log(`npx hardhat verify --network ${hre.network.name} ${gameCoreAddress} "${chefNFTAddress}" "${BACKEND_SIGNER}"`);
    console.log();
    console.log(`npx hardhat verify --network ${hre.network.name} ${leaderboardAddress}`);
    console.log();
  }

  console.log("✨ Deployment complete!");
}

main()
  .then(() => process.exit(0))
  .catch((error) => {
    console.error(error);
    process.exit(1);
  });

