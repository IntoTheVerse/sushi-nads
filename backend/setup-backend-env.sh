#!/bin/bash

echo "🍣 Sushi Bonks Backend - Environment Setup"
echo "=========================================="
echo ""

if [ -f .env ]; then
    echo "⚠️  .env file already exists!"
    read -p "Overwrite? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

echo "📝 Enter your Privy credentials:"
echo ""
read -p "Privy App ID: " PRIVY_APP_ID
read -p "Privy App Secret: " PRIVY_APP_SECRET
read -p "Privy Client ID: " PRIVY_CLIENT_ID
echo ""

echo "📝 Enter your Firebase credentials:"
echo ""
read -p "Firebase Project ID: " FIREBASE_PROJECT_ID
echo "Paste Firebase credentials JSON (all on one line, then press Enter):"
read FIREBASE_CREDS
echo ""

cat > .env << EOF
# Privy Configuration
PRIVY_APP_ID=$PRIVY_APP_ID
PRIVY_APP_SECRET=$PRIVY_APP_SECRET
PRIVY_CLIENT_ID=$PRIVY_CLIENT_ID

# Blockchain Configuration
BASE_SEPOLIA_RPC_URL=https://sepolia.base.org
BACKEND_PRIVATE_KEY=<PRIVATE_KEY_BACKEND_SIGNER>

# Contract Addresses (deployed on Base Sepolia)
CHEFNFT_ADDRESS=0x49889375450342FF44a56891034078Ad982Ef95E
GAMECORE_ADDRESS=0x9e6C89a53E270d87AE1f50b3fe00988Db2323931
LEADERBOARD_ADDRESS=0xB38343DA4Ba50dDD7D6F6d7E7cE6Dc08253C5dA7

# Firebase Configuration
FIREBASE_PROJECT_ID=$FIREBASE_PROJECT_ID
FIREBASE_CREDENTIALS=$FIREBASE_CREDS

# Server Configuration
PORT=3000
NODE_ENV=development

# CORS
ALLOWED_ORIGINS=*
EOF

chmod 600 .env
echo "✅ Backend .env created!"
echo ""
echo "🔐 Backend Signer Address: 0x8cE6f62bFf4ACeaDd4C49116214ba13BF446d527"
echo ""
echo "📋 Next: npm run dev"

