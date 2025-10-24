const hre = require("hardhat");

async function main() {
    console.log("🔗 Linking newly deployed contracts...");
    
    // NEW deployment addresses
    const CHEFNFT_ADDRESS = "0x59C826BbA45C9fbae9C3e04699A1287E798fE938";
    const GAMECORE_ADDRESS = "0x6460C256a0d3D9d2a024B821213440c1Fc2173AA";
    const LEADERBOARD_ADDRESS = "0xF14F8C427fd0758C98005Bd4C19deb93cbDDB666";
    
    // Get contract instances
    const ChefNFT = await hre.ethers.getContractAt("ChefNFT", CHEFNFT_ADDRESS);
    const GameCore = await hre.ethers.getContractAt("GameCore", GAMECORE_ADDRESS);
    const Leaderboard = await hre.ethers.getContractAt("Leaderboard", LEADERBOARD_ADDRESS);
    
    // 1. Set GameCore in ChefNFT
    console.log("\n1. Setting GameCore in ChefNFT...");
    const tx1 = await ChefNFT.setGameCore(GAMECORE_ADDRESS);
    await tx1.wait();
    console.log("   ✅ Done");
    
    // 2. Set Leaderboard in GameCore
    console.log("2. Setting Leaderboard in GameCore...");
    const tx2 = await GameCore.setLeaderboard(LEADERBOARD_ADDRESS);
    await tx2.wait();
    console.log("   ✅ Done");
    
    // 3. Set GameCore in Leaderboard
    console.log("3. Setting GameCore in Leaderboard...");
    const tx3 = await Leaderboard.setGameCore(GAMECORE_ADDRESS);
    await tx3.wait();
    console.log("   ✅ Done");
    
    console.log("\n✅ All contracts linked!");
    console.log("\n📋 New Deployment:");
    console.log(`ChefNFT:     ${CHEFNFT_ADDRESS}`);
    console.log(`GameCore:    ${GAMECORE_ADDRESS}`);
    console.log(`Leaderboard: ${LEADERBOARD_ADDRESS}`);
}

main()
    .then(() => process.exit(0))
    .catch((error) => {
        console.error(error);
        process.exit(1);
    });

