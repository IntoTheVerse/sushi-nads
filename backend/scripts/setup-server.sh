#!/bin/bash
# Setup script for Sushi Bonks backend on a fresh Ubuntu/Debian server
# Run as: bash scripts/setup-server.sh
#
# Prerequisites: SSH access to your dedicated cloud server
# Reference: https://medium.com/today-i-learned-chai/setup-node-js-application-with-pm2-and-nginx-72840f44ea73

set -e

echo "🍣 Sushi Bonks - Server Setup (PM2 + Nginx)"
echo "=========================================="

# Detect OS
if [ -f /etc/os-release ]; then
    . /etc/os-release
    OS=$ID
else
    echo "Cannot detect OS. Exiting."
    exit 1
fi

# Install Node.js 20 (Ubuntu/Debian)
if command -v node &> /dev/null; then
    NODE_VERSION=$(node -v)
    echo "Node.js already installed: $NODE_VERSION"
else
    echo "Installing Node.js 20..."
    curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
    sudo apt-get install -y nodejs
fi

# Install build tools (for native modules)
echo "Installing build-essential..."
sudo apt-get install -y build-essential

# Install Nginx
if command -v nginx &> /dev/null; then
    echo "Nginx already installed"
else
    echo "Installing Nginx..."
    sudo apt-get install -y nginx
fi

# Install PM2 globally
if command -v pm2 &> /dev/null; then
    echo "PM2 already installed: $(pm2 -v)"
else
    echo "Installing PM2..."
    sudo npm install -g pm2
fi

# Create logs directory
mkdir -p logs
echo "✅ Logs directory created"

echo ""
echo "=========================================="
echo "Next steps:"
echo "1. Copy .env to server and fill in your values"
echo "2. npm install"
echo "3. pm2 start ecosystem.config.cjs"
echo "4. pm2 save && pm2 startup  # Persist across reboots"
echo "5. Copy nginx/sushi-bonks.conf.example to /etc/nginx/conf.d/"
echo "   Edit server_name and run: sudo nginx -t && sudo systemctl reload nginx"
echo ""
echo "See DEPLOYMENT.md for full instructions."
