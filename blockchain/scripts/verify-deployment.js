const hre = require("hardhat");

async function main() {
  console.log("🔍 Verifying Sushi Bonks Deployment\n");
  
  const addresses = {
    chefNFT: "0x49889375450342FF44a56891034078Ad982Ef95E",
    gameCore: "0x9e6C89a53E270d87AE1f50b3fe00988Db2323931",
    leaderboard: "0xB38343DA4Ba50dDD7D6F6d7E7cE6Dc08253C5dA7"
  };
  
  // Get contract instances
  const ChefNFT = await hre.ethers.getContractFactory("ChefNFT");
  const GameCore = await hre.ethers.getContractFactory("GameCore");
  const Leaderboard = await hre.ethers.getContractFactory("Leaderboard");
  
  const chefNFT = ChefNFT.attach(addresses.chefNFT);
  const gameCore = GameCore.attach(addresses.gameCore);
  const leaderboard = Leaderboard.attach(addresses.leaderboard);
  
  // Check ChefNFT
  console.log("📋 ChefNFT:", addresses.chefNFT);
  try {
    const gameCoreAddr = await chefNFT.gameCoreAddress();
    const usdcAddr = await chefNFT.usdcToken();
    const energyCost = await chefNFT.energyRefillCost();
    
    console.log("  ├─ GameCore address:", gameCoreAddr);
    console.log("  ├─ USDC token:", usdcAddr);
    console.log("  └─ Energy refill cost:", hre.ethers.formatUnits(energyCost, 6), "USDC");
    console.log("  Status:", gameCoreAddr === addresses.gameCore ? "✅" : "⚠️ NEEDS SETUP");
  } catch (error) {
    console.log("  ❌ Error:", error.message);
  }
  
  console.log();
  
  // Check GameCore
  console.log("📋 GameCore:", addresses.gameCore);
  try {
    const chefNFTAddr = await gameCore.chefNFT();
    const leaderboardAddr = await gameCore.leaderboard();
    const backendSigner = await gameCore.backendSigner();
    
    console.log("  ├─ ChefNFT address:", chefNFTAddr);
    console.log("  ├─ Leaderboard address:", leaderboardAddr);
    console.log("  └─ Backend signer:", backendSigner);
    console.log("  Status:", 
      chefNFTAddr === addresses.chefNFT && leaderboardAddr === addresses.leaderboard ? "✅" : "⚠️ NEEDS SETUP"
    );
  } catch (error) {
    console.log("  ❌ Error:", error.message);
  }
  
  console.log();
  
  // Check Leaderboard
  console.log("📋 Leaderboard:", addresses.leaderboard);
  try {
    const gameCoreAddr = await leaderboard.gameCoreAddress();
    const season = await leaderboard.currentSeason();
    
    console.log("  ├─ GameCore address:", gameCoreAddr);
    console.log("  └─ Current season:", season.toString());
    console.log("  Status:", gameCoreAddr === addresses.gameCore ? "✅" : "⚠️ NEEDS SETUP");
    
    // If not set, set it now
    if (gameCoreAddr === "0x0000000000000000000000000000000000000000") {
      console.log("\n🔧 Fixing Leaderboard connection...");
      const tx = await leaderboard.setGameCore(addresses.gameCore);
      console.log("  Transaction:", tx.hash);
      await tx.wait();
      console.log("  ✅ Fixed!");
      
      // Verify again
      const newGameCoreAddr = await leaderboard.gameCoreAddress();
      console.log("  Verified:", newGameCoreAddr);
    }
  } catch (error) {
    console.log("  ❌ Error:", error.message);
  }
  
  console.log("\n" + "=".repeat(60));
  console.log("✅ Deployment verification complete!");
  console.log("=".repeat(60));
}

main()
  .then(() => process.exit(0))
  .catch((error) => {
    console.error(error);
    process.exit(1);
  });

