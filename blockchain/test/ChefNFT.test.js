const { expect } = require("chai");
const { ethers } = require("hardhat");
const { loadFixture } = require("@nomicfoundation/hardhat-toolbox/network-helpers");

describe("ChefNFT", function () {
  // Fixture to deploy contracts
  async function deployChefNFTFixture() {
    const [owner, player1, player2, gameCore] = await ethers.getSigners();

    // Deploy mock USDC token
    const MockUSDC = await ethers.getContractFactory("MockERC20");
    const usdc = await MockUSDC.deploy("USD Coin", "USDC", 6);
    await usdc.waitForDeployment();
    const usdcAddress = await usdc.getAddress();

    // Deploy ChefNFT
    const energyRefillCost = ethers.parseUnits("0.1", 6); // 0.1 USDC
    const ChefNFT = await ethers.getContractFactory("ChefNFT");
    const chefNFT = await ChefNFT.deploy(usdcAddress, energyRefillCost);
    await chefNFT.waitForDeployment();

    // Set GameCore address
    await chefNFT.setGameCore(gameCore.address);

    // Mint USDC to players for testing
    await usdc.mint(player1.address, ethers.parseUnits("100", 6));
    await usdc.mint(player2.address, ethers.parseUnits("100", 6));

    return { chefNFT, usdc, owner, player1, player2, gameCore, energyRefillCost };
  }

  describe("Deployment", function () {
    it("Should set the right owner", async function () {
      const { chefNFT, owner } = await loadFixture(deployChefNFTFixture);
      expect(await chefNFT.owner()).to.equal(owner.address);
    });

    it("Should set the correct USDC token address", async function () {
      const { chefNFT, usdc } = await loadFixture(deployChefNFTFixture);
      expect(await chefNFT.usdcToken()).to.equal(await usdc.getAddress());
    });

    it("Should set the correct energy refill cost", async function () {
      const { chefNFT, energyRefillCost } = await loadFixture(deployChefNFTFixture);
      expect(await chefNFT.energyRefillCost()).to.equal(energyRefillCost);
    });
  });

  describe("Minting Chef NFT", function () {
    it("Should mint a chef NFT with valid name", async function () {
      const { chefNFT, player1 } = await loadFixture(deployChefNFTFixture);
      const name = "ChefMaster";

      await expect(chefNFT.connect(player1).mintChef(name))
        .to.emit(chefNFT, "ChefMinted")
        .withArgs(player1.address, 1, name);

      expect(await chefNFT.hasChef(player1.address)).to.equal(true);
      expect(await chefNFT.playerToTokenId(player1.address)).to.equal(1);
      expect(await chefNFT.chefNames(1)).to.equal(name);
    });

    it("Should fail to mint with short name", async function () {
      const { chefNFT, player1 } = await loadFixture(deployChefNFTFixture);
      await expect(chefNFT.connect(player1).mintChef("AB"))
        .to.be.revertedWithCustomError(chefNFT, "InvalidName");
    });

    it("Should fail to mint with long name", async function () {
      const { chefNFT, player1 } = await loadFixture(deployChefNFTFixture);
      const longName = "A".repeat(21);
      await expect(chefNFT.connect(player1).mintChef(longName))
        .to.be.revertedWithCustomError(chefNFT, "InvalidName");
    });

    it("Should fail to mint second chef from same wallet", async function () {
      const { chefNFT, player1 } = await loadFixture(deployChefNFTFixture);
      await chefNFT.connect(player1).mintChef("Chef1");
      await expect(chefNFT.connect(player1).mintChef("Chef2"))
        .to.be.revertedWithCustomError(chefNFT, "AlreadyHasChef");
    });

    it("Should start with zero energy", async function () {
      const { chefNFT, player1 } = await loadFixture(deployChefNFTFixture);
      await chefNFT.connect(player1).mintChef("TestChef");
      expect(await chefNFT.getPlayerEnergy(player1.address)).to.equal(0);
    });
  });

  describe("Name Updates", function () {
    it("Should update chef name", async function () {
      const { chefNFT, player1 } = await loadFixture(deployChefNFTFixture);
      await chefNFT.connect(player1).mintChef("OldName");
      const tokenId = await chefNFT.playerToTokenId(player1.address);

      const newName = "NewName";
      await expect(chefNFT.connect(player1).updateChefName(newName))
        .to.emit(chefNFT, "NameUpdated")
        .withArgs(tokenId, newName);

      expect(await chefNFT.chefNames(tokenId)).to.equal(newName);
    });

    it("Should fail to update name without chef", async function () {
      const { chefNFT, player1 } = await loadFixture(deployChefNFTFixture);
      await expect(chefNFT.connect(player1).updateChefName("NewName"))
        .to.be.revertedWithCustomError(chefNFT, "NoChefMinted");
    });
  });

  describe("Energy System", function () {
    it("Should refill energy with USDC payment", async function () {
      const { chefNFT, usdc, player1, owner, energyRefillCost } = await loadFixture(deployChefNFTFixture);
      
      await chefNFT.connect(player1).mintChef("TestChef");
      await usdc.connect(player1).approve(await chefNFT.getAddress(), energyRefillCost);

      const initialBalance = await usdc.balanceOf(player1.address);
      const ownerInitialBalance = await usdc.balanceOf(owner.address);

      await expect(chefNFT.connect(player1).refillEnergy())
        .to.emit(chefNFT, "EnergyRefilled")
        .withArgs(player1.address, 300, 300);

      expect(await chefNFT.getPlayerEnergy(player1.address)).to.equal(300);
      expect(await usdc.balanceOf(player1.address)).to.equal(initialBalance - energyRefillCost);
      expect(await usdc.balanceOf(owner.address)).to.equal(ownerInitialBalance + energyRefillCost);
    });

    it("Should accumulate energy on multiple refills", async function () {
      const { chefNFT, usdc, player1, energyRefillCost } = await loadFixture(deployChefNFTFixture);
      
      await chefNFT.connect(player1).mintChef("TestChef");
      await usdc.connect(player1).approve(await chefNFT.getAddress(), energyRefillCost * 2n);

      await chefNFT.connect(player1).refillEnergy();
      await chefNFT.connect(player1).refillEnergy();

      expect(await chefNFT.getPlayerEnergy(player1.address)).to.equal(600);
    });

    it("Should fail to refill without USDC approval", async function () {
      const { chefNFT, player1 } = await loadFixture(deployChefNFTFixture);
      
      await chefNFT.connect(player1).mintChef("TestChef");

      // Will fail with ERC20 error, not our custom error
      await expect(chefNFT.connect(player1).refillEnergy())
        .to.be.reverted;
    });

    it("Should consume energy by GameCore", async function () {
      const { chefNFT, usdc, player1, gameCore, energyRefillCost } = await loadFixture(deployChefNFTFixture);
      
      await chefNFT.connect(player1).mintChef("TestChef");
      await usdc.connect(player1).approve(await chefNFT.getAddress(), energyRefillCost);
      await chefNFT.connect(player1).refillEnergy();

      await expect(chefNFT.connect(gameCore).consumeEnergy(player1.address, 50))
        .to.emit(chefNFT, "EnergyConsumed")
        .withArgs(player1.address, 50, 250);

      expect(await chefNFT.getPlayerEnergy(player1.address)).to.equal(250);
    });

    it("Should fail to consume more energy than available", async function () {
      const { chefNFT, usdc, player1, gameCore, energyRefillCost } = await loadFixture(deployChefNFTFixture);
      
      await chefNFT.connect(player1).mintChef("TestChef");
      await usdc.connect(player1).approve(await chefNFT.getAddress(), energyRefillCost);
      await chefNFT.connect(player1).refillEnergy();

      await expect(chefNFT.connect(gameCore).consumeEnergy(player1.address, 500))
        .to.be.revertedWithCustomError(chefNFT, "InsufficientEnergy");
    });

    it("Should fail to consume energy by non-GameCore address", async function () {
      const { chefNFT, player1, player2 } = await loadFixture(deployChefNFTFixture);
      
      await expect(chefNFT.connect(player2).consumeEnergy(player1.address, 10))
        .to.be.revertedWithCustomError(chefNFT, "UnauthorizedCaller");
    });
  });

  describe("Admin Functions", function () {
    it("Should allow owner to set GameCore address", async function () {
      const { chefNFT, owner, player1 } = await loadFixture(deployChefNFTFixture);
      
      await expect(chefNFT.connect(owner).setGameCore(player1.address))
        .to.emit(chefNFT, "GameCoreUpdated")
        .withArgs(player1.address);

      expect(await chefNFT.gameCoreAddress()).to.equal(player1.address);
    });

    it("Should allow owner to update energy refill cost", async function () {
      const { chefNFT, owner } = await loadFixture(deployChefNFTFixture);
      const newCost = ethers.parseUnits("0.2", 6);

      await expect(chefNFT.connect(owner).setEnergyRefillCost(newCost))
        .to.emit(chefNFT, "EnergyRefillCostUpdated")
        .withArgs(newCost);

      expect(await chefNFT.energyRefillCost()).to.equal(newCost);
    });

    it("Should allow owner to emergency add energy", async function () {
      const { chefNFT, owner, player1 } = await loadFixture(deployChefNFTFixture);
      
      await chefNFT.connect(player1).mintChef("TestChef");
      
      await expect(chefNFT.connect(owner).emergencyAddEnergy(player1.address, 1000))
        .to.emit(chefNFT, "EnergyRefilled")
        .withArgs(player1.address, 1000, 1000);

      expect(await chefNFT.getPlayerEnergy(player1.address)).to.equal(1000);
    });
  });

  describe("NFT Transfers (Soulbound)", function () {
    it("Should prevent transfers between wallets", async function () {
      const { chefNFT, player1, player2 } = await loadFixture(deployChefNFTFixture);
      
      await chefNFT.connect(player1).mintChef("TestChef");
      const tokenId = await chefNFT.playerToTokenId(player1.address);

      await expect(
        chefNFT.connect(player1).transferFrom(player1.address, player2.address, tokenId)
      ).to.be.revertedWithCustomError(chefNFT, "TransferNotAllowed");
    });
  });

  describe("View Functions", function () {
    it("Should return correct total chefs", async function () {
      const { chefNFT, player1, player2 } = await loadFixture(deployChefNFTFixture);
      
      expect(await chefNFT.totalChefs()).to.equal(0);
      
      await chefNFT.connect(player1).mintChef("Chef1");
      expect(await chefNFT.totalChefs()).to.equal(1);
      
      await chefNFT.connect(player2).mintChef("Chef2");
      expect(await chefNFT.totalChefs()).to.equal(2);
    });

    it("Should return correct player has chef status", async function () {
      const { chefNFT, player1, player2 } = await loadFixture(deployChefNFTFixture);
      
      expect(await chefNFT.playerHasChef(player1.address)).to.equal(false);
      
      await chefNFT.connect(player1).mintChef("TestChef");
      
      expect(await chefNFT.playerHasChef(player1.address)).to.equal(true);
      expect(await chefNFT.playerHasChef(player2.address)).to.equal(false);
    });
  });
});

// Mock ERC20 contract for testing
const MockERC20Source = `
// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import "@openzeppelin/contracts/token/ERC20/ERC20.sol";

contract MockERC20 is ERC20 {
    uint8 private _decimals;

    constructor(
        string memory name,
        string memory symbol,
        uint8 decimalsValue
    ) ERC20(name, symbol) {
        _decimals = decimalsValue;
    }

    function decimals() public view virtual override returns (uint8) {
        return _decimals;
    }

    function mint(address to, uint256 amount) external {
        _mint(to, amount);
    }
}
`;

// Save mock contract for compilation
const fs = require("fs");
const path = require("path");
const mockContractPath = path.join(__dirname, "..", "contracts", "MockERC20.sol");
if (!fs.existsSync(mockContractPath)) {
  fs.writeFileSync(mockContractPath, MockERC20Source);
}

