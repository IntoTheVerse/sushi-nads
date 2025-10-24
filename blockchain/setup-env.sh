#!/bin/bash

# Sushi Bonks - Environment Setup Script
# This script will help you create your .env file securely

echo "🍣 Sushi Bonks - Environment Setup"
echo "===================================="
echo ""

# Check if .env already exists
if [ -f .env ]; then
    echo "⚠️  .env file already exists!"
    read -p "Do you want to overwrite it? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Aborted."
        exit 1
    fi
fi

echo "📝 Please provide the following information:"
echo ""

# Get deployer private key
echo "1. Deployer Private Key (the wallet with Base Sepolia ETH)"
read -p "   Enter private key (without 0x): " DEPLOYER_KEY
echo ""

# Get Basescan API key (optional)
echo "2. Basescan API Key (optional, for contract verification)"
echo "   Get one from: https://basescan.org/myapikey"
read -p "   Enter API key (press enter to skip): " BASESCAN_KEY
echo ""

# Confirm backend signer address
BACKEND_SIGNER="0x8cE6f62bFf4ACeaDd4C49116214ba13BF446d527"
echo "3. Backend Signer Address"
echo "   Using generated address: $BACKEND_SIGNER"
read -p "   Press enter to confirm, or paste different address: " CUSTOM_BACKEND
if [ ! -z "$CUSTOM_BACKEND" ]; then
    BACKEND_SIGNER=$CUSTOM_BACKEND
fi
echo ""

# Create .env file
cat > .env << EOF
# Sushi Bonks Environment Configuration
# Generated on $(date)

# Deployer Private Key (DO NOT SHARE!)
PRIVATE_KEY=$DEPLOYER_KEY

# Base Sepolia RPC URL
BASE_SEPOLIA_RPC_URL=https://sepolia.base.org

# Basescan API Key for contract verification
BASESCAN_API_KEY=$BASESCAN_KEY

# USDC Token Address on Base Sepolia (standard testnet address)
USDC_ADDRESS=0x036CbD53842c5426634e7929541eC2318f3dCF7e

# Backend signer address (authorized to call game functions)
BACKEND_SIGNER_ADDRESS=$BACKEND_SIGNER

# Gas reporting (optional)
REPORT_GAS=false
COINMARKETCAP_API_KEY=
EOF

# Set proper permissions (readable only by owner)
chmod 600 .env

echo "✅ .env file created successfully!"
echo ""
echo "🔐 Security:"
echo "   - File permissions set to 600 (owner read/write only)"
echo "   - File is gitignored (won't be committed)"
echo ""
echo "📋 Next steps:"
echo "   1. Verify your .env file: cat .env"
echo "   2. Make sure your deployer wallet has Base Sepolia ETH"
echo "   3. Run deployment: npm run deploy:sepolia"
echo ""
echo "⚠️  IMPORTANT: Save your backend signer private key!"
echo "   Backend Private Key: <PRIVATE_KEY_BACKEND_SIGNER>"
echo "   (You'll need this for the backend server)"
echo ""

