const hre = require("hardhat");

async function main() {
    console.log("🔗 Linking newly deployed contracts...");
    
    // NEW deployment addresses
    const CHEFNFT_ADDRESS = "0xC3dDBBA92CE071D7B872D31f3870bC4672c3D05E";
    const GAMECORE_ADDRESS = "0x17C43c95226250c116E904dea0a7507a74ADa99a";
    const LEADERBOARD_ADDRESS = "0x41ba1B1bd3A81955D476Cb909DD4eA056caaE45f";
    
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

