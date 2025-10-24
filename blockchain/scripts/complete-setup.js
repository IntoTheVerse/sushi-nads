const hre = require("hardhat");

async function main() {
  console.log("🔗 Completing contract setup...\n");
  
  const leaderboardAddress = "0xB38343DA4Ba50dDD7D6F6d7E7cE6Dc08253C5dA7";
  const gameCoreAddress = "0x9e6C89a53E270d87AE1f50b3fe00988Db2323931";
  
  const Leaderboard = await hre.ethers.getContractFactory("Leaderboard");
  const leaderboard = Leaderboard.attach(leaderboardAddress);
  
  console.log("Setting GameCore in Leaderboard...");
  const tx = await leaderboard.setGameCore(gameCoreAddress);
  console.log("Transaction sent:", tx.hash);
  
  await tx.wait();
  console.log("✅ Transaction confirmed!");
  
  // Verify all connections
  console.log("\n🔍 Verifying contract connections...");
  const storedGameCore = await leaderboard.gameCoreAddress();
  console.log("✓ Leaderboard -> GameCore:", storedGameCore);
  
  console.log("\n✅ All contracts deployed and connected!");
  console.log("\n📋 Deployed Addresses:");
  console.log("ChefNFT:    ", "0x49889375450342FF44a56891034078Ad982Ef95E");
  console.log("GameCore:   ", gameCoreAddress);
  console.log("Leaderboard:", leaderboardAddress);
}

main()
  .then(() => process.exit(0))
  .catch((error) => {
    console.error(error);
    process.exit(1);
  });

