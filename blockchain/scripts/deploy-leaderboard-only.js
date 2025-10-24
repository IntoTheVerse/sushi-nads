const hre = require("hardhat");

async function main() {
  console.log("📝 Deploying Leaderboard only...\n");

  const Leaderboard = await hre.ethers.getContractFactory("Leaderboard");
  const leaderboard = await Leaderboard.deploy();
  await leaderboard.waitForDeployment();
  const leaderboardAddress = await leaderboard.getAddress();
  
  console.log("✅ Leaderboard deployed to:", leaderboardAddress);
  
  return leaderboardAddress;
}

main()
  .then(() => process.exit(0))
  .catch((error) => {
    console.error(error);
    process.exit(1);
  });

