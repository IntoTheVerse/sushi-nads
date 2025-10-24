const hre = require("hardhat");

async function main() {
  const addresses = {
    chefNFT: "0x9D7d4bA848070c5Abc40f78d8da0cb370c892D27",
    gameCore: "0xf39a920E7B7d368277967c28Ac1B913d310BE013",
    leaderboard: "0xf09C95534569C0cb31229D0B5cBfF752AD22d945"
  };

  console.log("🔗 Linking contracts...\n");

  // Get contract instances
  const ChefNFT = await hre.ethers.getContractFactory("ChefNFT");
  const GameCore = await hre.ethers.getContractFactory("GameCore");
  const Leaderboard = await hre.ethers.getContractFactory("Leaderboard");

  const chefNFT = ChefNFT.attach(addresses.chefNFT);
  const gameCore = GameCore.attach(addresses.gameCore);
  const leaderboard = Leaderboard.attach(addresses.leaderboard);

  // 1. Set GameCore in ChefNFT
  console.log("1. Setting GameCore in ChefNFT...");
  const tx1 = await chefNFT.setGameCore(addresses.gameCore);
  await tx1.wait();
  console.log("   ✅ Done");

  // 2. Set Leaderboard in GameCore
  console.log("2. Setting Leaderboard in GameCore...");
  const tx2 = await gameCore.setLeaderboard(addresses.leaderboard);
  await tx2.wait();
  console.log("   ✅ Done");

  // 3. Set GameCore in Leaderboard
  console.log("3. Setting GameCore in Leaderboard...");
  const tx3 = await leaderboard.setGameCore(addresses.gameCore);
  await tx3.wait();
  console.log("   ✅ Done");

  console.log("\n✅ All contracts linked!");
  console.log("\n📋 New Deployment:");
  console.log("ChefNFT:    ", addresses.chefNFT);
  console.log("GameCore:   ", addresses.gameCore);
  console.log("Leaderboard:", addresses.leaderboard);
}

main()
  .then(() => process.exit(0))
  .catch((error) => {
    console.error(error);
    process.exit(1);
  });

