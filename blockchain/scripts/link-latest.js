const hre = require("hardhat");

async function main() {
    console.log("🔗 Linking latest deployment...");
    
    const CHEFNFT_ADDRESS = "0x4ef5835f754fb83364ec8bB7CC5e3976087a46ad";
    const GAMECORE_ADDRESS = "0x04E9f92c8e4Ce69c3f73eac9bF18F3c38d905012";
    const LEADERBOARD_ADDRESS = "0xD13bb48824451EAEDdB8a4cb000066F65aA7eB44";
    
    const ChefNFT = await hre.ethers.getContractAt("ChefNFT", CHEFNFT_ADDRESS);
    const GameCore = await hre.ethers.getContractAt("GameCore", GAMECORE_ADDRESS);
    const Leaderboard = await hre.ethers.getContractAt("Leaderboard", LEADERBOARD_ADDRESS);
    
    console.log("1. Setting GameCore in ChefNFT...");
    await (await ChefNFT.setGameCore(GAMECORE_ADDRESS)).wait();
    console.log("   ✅ Done");
    
    console.log("2. Setting Leaderboard in GameCore...");
    await (await GameCore.setLeaderboard(LEADERBOARD_ADDRESS)).wait();
    console.log("   ✅ Done");
    
    console.log("3. Setting GameCore in Leaderboard...");
    await (await Leaderboard.setGameCore(GAMECORE_ADDRESS)).wait();
    console.log("   ✅ Done");
    
    console.log("\n✅ All contracts linked!");
    console.log(`\nChefNFT:     ${CHEFNFT_ADDRESS}`);
    console.log(`GameCore:    ${GAMECORE_ADDRESS}`);
    console.log(`Leaderboard: ${LEADERBOARD_ADDRESS}`);
}

main().then(() => process.exit(0)).catch((error) => { console.error(error); process.exit(1); });
