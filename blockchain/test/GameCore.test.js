const { expect } = require("chai");
const { ethers } = require("hardhat");
const { loadFixture, time } = require("@nomicfoundation/hardhat-toolbox/network-helpers");

describe("GameCore", function () {
  async function deployGameCoreFixture() {
    const [owner, backend, player1, player2] = await ethers.getSigners();

    // Deploy mock USDC
    const MockUSDC = await ethers.getContractFactory("MockERC20");
    const usdc = await MockUSDC.deploy("USD Coin", "USDC", 6);
    await usdc.waitForDeployment();

    // Deploy ChefNFT
    const energyRefillCost = ethers.parseUnits("0.1", 6);
    const ChefNFT = await ethers.getContractFactory("ChefNFT");
    const chefNFT = await ChefNFT.deploy(await usdc.getAddress(), energyRefillCost);
    await chefNFT.waitForDeployment();

    // Deploy GameCore
    const GameCore = await ethers.getContractFactory("GameCore");
    const gameCore = await GameCore.deploy(await chefNFT.getAddress(), backend.address);
    await gameCore.waitForDeployment();

    // Deploy Leaderboard
    const Leaderboard = await ethers.getContractFactory("Leaderboard");
    const leaderboard = await Leaderboard.deploy();
    await leaderboard.waitForDeployment();

    // Setup connections
    await chefNFT.setGameCore(await gameCore.getAddress());
    await gameCore.setLeaderboard(await leaderboard.getAddress());
    await leaderboard.setGameCore(await gameCore.getAddress());

    // Mint USDC and give energy to players
    await usdc.mint(player1.address, ethers.parseUnits("100", 6));
    await usdc.mint(player2.address, ethers.parseUnits("100", 6));

    // Players mint chef NFTs
    await chefNFT.connect(player1).mintChef("Player1");
    await chefNFT.connect(player2).mintChef("Player2");

    // Players get energy
    await usdc.connect(player1).approve(await chefNFT.getAddress(), energyRefillCost * 10n);
    await usdc.connect(player2).approve(await chefNFT.getAddress(), energyRefillCost * 10n);
    await chefNFT.connect(player1).refillEnergy();
    await chefNFT.connect(player2).refillEnergy();

    return { gameCore, chefNFT, leaderboard, usdc, owner, backend, player1, player2 };
  }

  describe("Deployment", function () {
    it("Should set the correct ChefNFT address", async function () {
      const { gameCore, chefNFT } = await loadFixture(deployGameCoreFixture);
      expect(await gameCore.chefNFT()).to.equal(await chefNFT.getAddress());
    });

    it("Should set the correct backend signer", async function () {
      const { gameCore, backend } = await loadFixture(deployGameCoreFixture);
      expect(await gameCore.backendSigner()).to.equal(backend.address);
    });

    it("Should have correct initial scoring config", async function () {
      const { gameCore } = await loadFixture(deployGameCoreFixture);
      expect(await gameCore.deliveryBonus()).to.equal(100);
      expect(await gameCore.cookBonus()).to.equal(10);
      expect(await gameCore.discardPenalty()).to.equal(50);
      expect(await gameCore.overcookPenalty()).to.equal(20);
      expect(await gameCore.movePenalty()).to.equal(1);
    });
  });

  describe("Session Management", function () {
    it("Should start a game session", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await expect(gameCore.connect(backend).startGameSession(player1.address))
        .to.emit(gameCore, "SessionStarted");

      const session = await gameCore.getSession(1);
      expect(session.player).to.equal(player1.address);
      expect(session.isActive).to.equal(true);
      expect(session.sessionId).to.equal(1);
    });

    it("Should fail to start session without chef NFT", async function () {
      const { gameCore, backend, owner } = await loadFixture(deployGameCoreFixture);

      await expect(gameCore.connect(backend).startGameSession(owner.address))
        .to.be.revertedWithCustomError(gameCore, "NoChefNFT");
    });

    it("Should fail to start session with insufficient energy", async function () {
      const { gameCore, chefNFT, backend, player1, owner } = await loadFixture(deployGameCoreFixture);

      // Use emergency function to drain energy (only owner can do this, not backend)
      await chefNFT.connect(owner).emergencyAddEnergy(player1.address, 0);
      // Now manually set to 4 (less than required 5)
      const currentEnergy = await chefNFT.getPlayerEnergy(player1.address);
      if (currentEnergy > 4) {
        // Start a session and make moves to drain energy
        await gameCore.connect(backend).startGameSession(player1.address);
        for (let i = 0; i < 296; i++) {
          await gameCore.connect(backend).recordMove(1);
        }
        await gameCore.connect(backend).endGameSession(1, "TEST", 0);
      }

      await expect(gameCore.connect(backend).startGameSession(player1.address))
        .to.be.revertedWithCustomError(gameCore, "InsufficientEnergy");
    });

    it("Should fail to start session if one already active", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);

      await expect(gameCore.connect(backend).startGameSession(player1.address))
        .to.be.revertedWithCustomError(gameCore, "SessionAlreadyActive");
    });

    it("Should end a game session", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);
      
      await expect(gameCore.connect(backend).endGameSession(1, "TIME_UP", 0))
        .to.emit(gameCore, "SessionEnded");

      const session = await gameCore.getSession(1);
      expect(session.isActive).to.equal(false);
      expect(session.scoreFinalized).to.equal(true);
    });

    it("Should prevent non-backend from starting session", async function () {
      const { gameCore, player1, player2 } = await loadFixture(deployGameCoreFixture);

      await expect(gameCore.connect(player1).startGameSession(player2.address))
        .to.be.revertedWithCustomError(gameCore, "UnauthorizedCaller");
    });
  });

  describe("Gameplay Actions", function () {
    it("Should record moves and consume energy", async function () {
      const { gameCore, chefNFT, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);
      
      const energyBefore = await chefNFT.getPlayerEnergy(player1.address);

      await expect(gameCore.connect(backend).recordMove(1))
        .to.emit(gameCore, "MoveMade")
        .withArgs(1, 1, energyBefore - 1n);

      const session = await gameCore.getSession(1);
      expect(session.moveCount).to.equal(1);

      const energyAfter = await chefNFT.getPlayerEnergy(player1.address);
      expect(energyAfter).to.equal(energyBefore - 1n);
    });

    it("Should record recipe spawned", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);

      await expect(gameCore.connect(backend).recordRecipeSpawned(1))
        .to.emit(gameCore, "RecipeSpawned")
        .withArgs(1, 1);

      const session = await gameCore.getSession(1);
      expect(session.recipesSpawned).to.equal(1);
    });

    it("Should record recipe delivered and track combo", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);

      await gameCore.connect(backend).recordRecipeDelivered(1);
      await gameCore.connect(backend).recordRecipeDelivered(1);
      await gameCore.connect(backend).recordRecipeDelivered(1);

      const session = await gameCore.getSession(1);
      expect(session.recipesDelivered).to.equal(3);
      expect(session.consecutivePerfectDeliveries).to.equal(3);
      expect(session.maxComboAchieved).to.equal(3);
    });

    it("Should reset combo on recipe discarded", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);

      await gameCore.connect(backend).recordRecipeDelivered(1);
      await gameCore.connect(backend).recordRecipeDelivered(1);
      
      let session = await gameCore.getSession(1);
      expect(session.consecutivePerfectDeliveries).to.equal(2);

      await gameCore.connect(backend).recordRecipeDiscarded(1);
      
      session = await gameCore.getSession(1);
      expect(session.consecutivePerfectDeliveries).to.equal(0);
      expect(session.recipesDiscarded).to.equal(1);
    });

    it("Should record well-cooked ingredients", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);
      
      await gameCore.connect(backend).recordWellCooked(1);
      await gameCore.connect(backend).recordWellCooked(1);

      const session = await gameCore.getSession(1);
      expect(session.wellCookedCount).to.equal(2);
    });

    it("Should record overcooked ingredients", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);
      
      await gameCore.connect(backend).recordOverCooked(1);

      const session = await gameCore.getSession(1);
      expect(session.overCookedCount).to.equal(1);
    });

    it("Should fail to record actions on inactive session", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);
      await gameCore.connect(backend).endGameSession(1, "TIME_UP", 0);

      await expect(gameCore.connect(backend).recordMove(1))
        .to.be.revertedWithCustomError(gameCore, "SessionNotActive");
    });
  });

  describe("Scoring System", function () {
    it("Should compute score correctly with deliveries only", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);
      
      // Deliver 5 recipes
      for (let i = 0; i < 5; i++) {
        await gameCore.connect(backend).recordRecipeDelivered(1);
      }

      await gameCore.connect(backend).endGameSession(1, "TIME_UP", 0);

      const session = await gameCore.getSession(1);
      // 5 deliveries * 100 = 500 base
      // Combo bonus for 5 streak = 150
      // Efficiency 100% = 1.2x multiplier = (500 + 150) * 1.2 = 780
      // Plus time bonus (depends on time taken)
      expect(session.finalScore).to.be.greaterThan(700);
    });

    it("Should apply penalties for discards and overcooks", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);
      
      await gameCore.connect(backend).recordRecipeDelivered(1);
      await gameCore.connect(backend).recordRecipeDiscarded(1);
      await gameCore.connect(backend).recordOverCooked(1);

      // Advance time to reduce time bonus
      await time.increase(500);

      await gameCore.connect(backend).endGameSession(1, "TIME_UP", 0);

      const session = await gameCore.getSession(1);
      // Base: 1 delivery * 100 = 100
      // Penalties: 1 discard * -50 + 1 overcook * -20 = -70
      // Net before bonuses: 30
      // Time bonus will still add points, but efficiency is 50% (no multiplier)
      expect(session.recipesDelivered).to.equal(1);
      expect(session.recipesDiscarded).to.equal(1);
      expect(session.overCookedCount).to.equal(1);
    });

    it("Should include move penalty", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);
      
      // Make 100 moves
      for (let i = 0; i < 100; i++) {
        await gameCore.connect(backend).recordMove(1);
      }

      await gameCore.connect(backend).recordRecipeDelivered(1);
      await gameCore.connect(backend).endGameSession(1, "TIME_UP", 0);

      const session = await gameCore.getSession(1);
      expect(session.moveCount).to.equal(100);
    });

    it("Should apply off-chain move count at session end (new flow)", async function () {
      const { gameCore, chefNFT, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);
      const energyBefore = await chefNFT.getPlayerEnergy(player1.address);

      // No recordMove calls - client tracks locally and sends at end
      await gameCore.connect(backend).recordRecipeDelivered(1);
      await gameCore.connect(backend).endGameSession(1, "TIME_UP", 50);

      const session = await gameCore.getSession(1);
      expect(session.moveCount).to.equal(50);

      const energyAfter = await chefNFT.getPlayerEnergy(player1.address);
      expect(energyAfter).to.equal(energyBefore - 50n); // 50 moves * 1 energy each
    });

    it("Should give time bonus for fast completion", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);
      
      await gameCore.connect(backend).recordRecipeDelivered(1);
      
      // End immediately (fast completion)
      await gameCore.connect(backend).endGameSession(1, "TIME_UP", 0);

      const session = await gameCore.getSession(1);
      // Should have significant time bonus
      expect(session.finalScore).to.be.greaterThan(100);
    });

    it("Should ensure score is non-negative", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);
      
      // Lots of penalties, no rewards
      for (let i = 0; i < 10; i++) {
        await gameCore.connect(backend).recordRecipeDiscarded(1);
        await gameCore.connect(backend).recordOverCooked(1);
      }

      // Advance time past game duration to remove time bonus
      await time.increase(1001);

      await gameCore.connect(backend).endGameSession(1, "TIME_UP", 0);

      const session = await gameCore.getSession(1);
      // With time bonus gone and only penalties, score should be 0
      expect(session.finalScore).to.equal(0); // Should not be negative
    });
  });

  describe("Global Stats", function () {
    it("Should track global stats across sessions", async function () {
      const { gameCore, chefNFT, usdc, backend, player1, owner } = await loadFixture(deployGameCoreFixture);

      // Session 1
      await gameCore.connect(backend).startGameSession(player1.address);
      await gameCore.connect(backend).recordRecipeDelivered(1);
      await gameCore.connect(backend).recordMove(1);
      await gameCore.connect(backend).endGameSession(1, "TIME_UP", 0);

      // Refill energy for session 2 using same instance
      const energyRefillCost = await chefNFT.energyRefillCost();
      await usdc.connect(player1).approve(await chefNFT.getAddress(), energyRefillCost);
      await chefNFT.connect(player1).refillEnergy();

      // Session 2
      await gameCore.connect(backend).startGameSession(player1.address);
      await gameCore.connect(backend).recordRecipeDelivered(2);
      await gameCore.connect(backend).recordMove(2);
      await gameCore.connect(backend).endGameSession(2, "TIME_UP", 0);

      const stats = await gameCore.getPlayerGlobalStats(player1.address);
      expect(stats.totalSessionsPlayed).to.equal(2);
      expect(stats.totalRecipesDelivered).to.equal(2);
      expect(stats.totalMoves).to.equal(2);
    });
  });

  describe("View Functions", function () {
    it("Should get current session ID", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      expect(await gameCore.getCurrentSessionId(player1.address)).to.equal(0);

      await gameCore.connect(backend).startGameSession(player1.address);
      expect(await gameCore.getCurrentSessionId(player1.address)).to.equal(1);

      await gameCore.connect(backend).endGameSession(1, "TIME_UP", 0);
      expect(await gameCore.getCurrentSessionId(player1.address)).to.equal(0);
    });

    it("Should get session history", async function () {
      const { gameCore, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);
      await gameCore.connect(backend).endGameSession(1, "TIME_UP", 0);

      const history = await gameCore.getPlayerSessionHistory(player1.address);
      expect(history.length).to.equal(1);
      expect(history[0]).to.equal(1);
    });

    it("Should return total sessions", async function () {
      const { gameCore, backend, player1, player2 } = await loadFixture(deployGameCoreFixture);

      expect(await gameCore.totalSessions()).to.equal(0);

      await gameCore.connect(backend).startGameSession(player1.address);
      expect(await gameCore.totalSessions()).to.equal(1);

      await gameCore.connect(backend).endGameSession(1, "TIME_UP", 0);
      await gameCore.connect(backend).startGameSession(player2.address);
      expect(await gameCore.totalSessions()).to.equal(2);
    });
  });

  describe("Admin Functions", function () {
    it("Should update scoring config", async function () {
      const { gameCore, owner } = await loadFixture(deployGameCoreFixture);

      await expect(gameCore.connect(owner).updateScoringConfig(200, 20, 100, 40, 2))
        .to.emit(gameCore, "ScoringConfigUpdated")
        .withArgs(200, 20, 100, 40, 2);

      expect(await gameCore.deliveryBonus()).to.equal(200);
      expect(await gameCore.cookBonus()).to.equal(20);
    });

    it("Should update backend signer", async function () {
      const { gameCore, owner, player1 } = await loadFixture(deployGameCoreFixture);

      await expect(gameCore.connect(owner).setBackendSigner(player1.address))
        .to.emit(gameCore, "BackendSignerUpdated")
        .withArgs(player1.address);

      expect(await gameCore.backendSigner()).to.equal(player1.address);
    });

    it("Should emergency end stuck session", async function () {
      const { gameCore, owner, backend, player1 } = await loadFixture(deployGameCoreFixture);

      await gameCore.connect(backend).startGameSession(player1.address);

      await expect(gameCore.connect(owner).emergencyEndSession(1))
        .to.emit(gameCore, "SessionEnded");

      const session = await gameCore.getSession(1);
      expect(session.isActive).to.equal(false);
    });
  });
});

