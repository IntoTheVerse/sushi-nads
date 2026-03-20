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

# Blockchain Configuration (Monad Testnet)
BACKEND_PRIVATE_KEY=<PRIVATE_KEY_BACKEND_SIGNER>
MONAD_RPC_URL=https://testnet-rpc.monad.xyz

# Contract Addresses (deployed on Monad Testnet)
MONAD_CHEFNFT_ADDRESS=0xb82949eC11B51BFfb2F75a8c5e4DD17184964F5d
MONAD_GAMECORE_ADDRESS=0x228e318BF68794e7012539D2EE48B14D66A6aFF8
MONAD_LEADERBOARD_ADDRESS=0xac4Df66c40EBE451767c6E9DfCE353B6212BA7ed

# Firebase Configuration
FIREBASE_PROJECT_ID=$FIREBASE_PROJECT_ID
FIREBASE_CREDENTIALS=$FIREBASE_CREDS

# Server Configuration
PORT=8081
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

