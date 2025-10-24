#!/bin/bash

echo "🔄 Updating backend with new contract addresses..."
echo ""

# Check if .env exists
if [ ! -f .env ]; then
    echo "❌ .env file not found!"
    exit 1
fi

# Backup current .env
cp .env .env.backup
echo "✅ Backed up current .env to .env.backup"

# Update contract addresses
sed -i '' 's/CHEFNFT_ADDRESS=.*/CHEFNFT_ADDRESS=0x9D7d4bA848070c5Abc40f78d8da0cb370c892D27/' .env
sed -i '' 's/GAMECORE_ADDRESS=.*/GAMECORE_ADDRESS=0xf39a920E7B7d368277967c28Ac1B913d310BE013/' .env
sed -i '' 's/LEADERBOARD_ADDRESS=.*/LEADERBOARD_ADDRESS=0xf09C95534569C0cb31229D0B5cBfF752AD22d945/' .env

echo "✅ Contract addresses updated in .env"
echo ""
echo "📋 New addresses:"
echo "   ChefNFT:     0x9D7d4bA848070c5Abc40f78d8da0cb370c892D27"
echo "   GameCore:    0xf39a920E7B7d368277967c28Ac1B913d310BE013"
echo "   Leaderboard: 0xf09C95534569C0cb31229D0B5cBfF752AD22d945"
echo ""
echo "🚀 Next: Copy new ABIs to backend"

