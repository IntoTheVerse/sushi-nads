const { expect } = require("chai");
const { ethers } = require("hardhat");
const { loadFixture } = require("@nomicfoundation/hardhat-toolbox/network-helpers");

describe("Leaderboard", function () {
  async function deployLeaderboardFixture() {
    const [owner, gameCore, player1, player2, player3, player4, player5] = await ethers.getSigners();

    const Leaderboard = await ethers.getContractFactory("Leaderboard");
    const leaderboard = await Leaderboard.deploy();
    await leaderboard.waitForDeployment();

    // Set GameCore address
    await leaderboard.setGameCore(gameCore.address);

    return { leaderboard, owner, gameCore, player1, player2, player3, player4, player5 };
  }

  describe("Deployment", function () {
    it("Should set the right owner", async function () {
      const { leaderboard, owner } = await loadFixture(deployLeaderboardFixture);
      expect(await leaderboard.owner()).to.equal(owner.address);
    });

    it("Should start with season 1", async function () {
      const { leaderboard } = await loadFixture(deployLeaderboardFixture);
      expect(await leaderboard.currentSeason()).to.equal(1);
    });

    it("Should set GameCore address", async function () {
      const { leaderboard, gameCore } = await loadFixture(deployLeaderboardFixture);
      expect(await leaderboard.gameCoreAddress()).to.equal(gameCore.address);
    });
  });

  describe("Score Updates", function () {
    it("Should update player score", async function () {
      const { leaderboard, gameCore, player1 } = await loadFixture(deployLeaderboardFixture);

      await expect(leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1))
        .to.emit(leaderboard, "ScoreUpdated")
        .withArgs(1, player1.address, 1000, 0, 1);

      expect(await leaderboard.getPlayerHighScore(player1.address)).to.equal(1000);
    });

    it("Should only update if new score is higher", async function () {
      const { leaderboard, gameCore, player1 } = await loadFixture(deployLeaderboardFixture);

      await leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1);
      await leaderboard.connect(gameCore).updateScore(player1.address, 500, 2);

      expect(await leaderboard.getPlayerHighScore(player1.address)).to.equal(1000);
    });

    it("Should update if new score is higher", async function () {
      const { leaderboard, gameCore, player1 } = await loadFixture(deployLeaderboardFixture);

      await leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1);
      
      await expect(leaderboard.connect(gameCore).updateScore(player1.address, 1500, 2))
        .to.emit(leaderboard, "ScoreUpdated")
        .withArgs(1, player1.address, 1500, 1000, 2);

      expect(await leaderboard.getPlayerHighScore(player1.address)).to.equal(1500);
    });

    it("Should fail to update score from non-GameCore address", async function () {
      const { leaderboard, player1, player2 } = await loadFixture(deployLeaderboardFixture);

      await expect(leaderboard.connect(player1).updateScore(player2.address, 1000, 1))
        .to.be.revertedWithCustomError(leaderboard, "UnauthorizedCaller");
    });

    it("Should increment player count for first score", async function () {
      const { leaderboard, gameCore, player1, player2 } = await loadFixture(deployLeaderboardFixture);

      expect(await leaderboard.getTotalPlayers()).to.equal(0);

      await leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1);
      expect(await leaderboard.getTotalPlayers()).to.equal(1);

      await leaderboard.connect(gameCore).updateScore(player2.address, 900, 2);
      expect(await leaderboard.getTotalPlayers()).to.equal(2);

      // Updating same player shouldn't increase count
      await leaderboard.connect(gameCore).updateScore(player1.address, 1200, 3);
      expect(await leaderboard.getTotalPlayers()).to.equal(2);
    });
  });

  describe("Rankings", function () {
    it("Should maintain rankings in descending order", async function () {
      const { leaderboard, gameCore, player1, player2, player3 } = await loadFixture(deployLeaderboardFixture);

      await leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1);
      await leaderboard.connect(gameCore).updateScore(player2.address, 1500, 2);
      await leaderboard.connect(gameCore).updateScore(player3.address, 800, 3);

      const [players, scores] = await leaderboard.getTopPlayers(3);

      expect(players[0]).to.equal(player2.address);
      expect(scores[0]).to.equal(1500);
      expect(players[1]).to.equal(player1.address);
      expect(scores[1]).to.equal(1000);
      expect(players[2]).to.equal(player3.address);
      expect(scores[2]).to.equal(800);
    });

    it("Should update rankings when player improves score", async function () {
      const { leaderboard, gameCore, player1, player2, player3 } = await loadFixture(deployLeaderboardFixture);

      await leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1);
      await leaderboard.connect(gameCore).updateScore(player2.address, 1500, 2);
      await leaderboard.connect(gameCore).updateScore(player3.address, 800, 3);

      // Player3 improves to top score
      await leaderboard.connect(gameCore).updateScore(player3.address, 2000, 4);

      const [players, scores] = await leaderboard.getTopPlayers(3);

      expect(players[0]).to.equal(player3.address);
      expect(scores[0]).to.equal(2000);
      expect(players[1]).to.equal(player2.address);
      expect(scores[1]).to.equal(1500);
    });

    it("Should calculate player rank correctly", async function () {
      const { leaderboard, gameCore, player1, player2, player3 } = await loadFixture(deployLeaderboardFixture);

      await leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1);
      await leaderboard.connect(gameCore).updateScore(player2.address, 1500, 2);
      await leaderboard.connect(gameCore).updateScore(player3.address, 800, 3);

      expect(await leaderboard.getPlayerRank(player2.address)).to.equal(1);
      expect(await leaderboard.getPlayerRank(player1.address)).to.equal(2);
      expect(await leaderboard.getPlayerRank(player3.address)).to.equal(3);
    });

    it("Should return 0 rank for player with no score", async function () {
      const { leaderboard, player1 } = await loadFixture(deployLeaderboardFixture);
      expect(await leaderboard.getPlayerRank(player1.address)).to.equal(0);
    });
  });

  describe("View Functions", function () {
    it("Should get top N players", async function () {
      const { leaderboard, gameCore, player1, player2, player3, player4, player5 } = await loadFixture(deployLeaderboardFixture);

      await leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1);
      await leaderboard.connect(gameCore).updateScore(player2.address, 1500, 2);
      await leaderboard.connect(gameCore).updateScore(player3.address, 800, 3);
      await leaderboard.connect(gameCore).updateScore(player4.address, 1200, 4);
      await leaderboard.connect(gameCore).updateScore(player5.address, 600, 5);

      const [players, scores] = await leaderboard.getTopPlayers(3);

      expect(players.length).to.equal(3);
      expect(scores[0]).to.be.greaterThan(scores[1]);
      expect(scores[1]).to.be.greaterThan(scores[2]);
    });

    it("Should handle limit greater than total players", async function () {
      const { leaderboard, gameCore, player1, player2 } = await loadFixture(deployLeaderboardFixture);

      await leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1);
      await leaderboard.connect(gameCore).updateScore(player2.address, 900, 2);

      const [players] = await leaderboard.getTopPlayers(10);

      expect(players.length).to.equal(2);
    });

    it("Should get top players with detailed info", async function () {
      const { leaderboard, gameCore, player1, player2 } = await loadFixture(deployLeaderboardFixture);

      await leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1);
      await leaderboard.connect(gameCore).updateScore(player2.address, 1500, 2);

      const entries = await leaderboard.getTopPlayersDetailed(2);

      expect(entries.length).to.equal(2);
      expect(entries[0].player).to.equal(player2.address);
      expect(entries[0].highScore).to.equal(1500);
      expect(entries[0].sessionId).to.equal(2);
      expect(entries[1].player).to.equal(player1.address);
    });

    it("Should get players in rank range", async function () {
      const { leaderboard, gameCore, player1, player2, player3, player4 } = await loadFixture(deployLeaderboardFixture);

      await leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1);
      await leaderboard.connect(gameCore).updateScore(player2.address, 1500, 2);
      await leaderboard.connect(gameCore).updateScore(player3.address, 800, 3);
      await leaderboard.connect(gameCore).updateScore(player4.address, 1200, 4);

      const entries = await leaderboard.getPlayersInRankRange(2, 3);

      expect(entries.length).to.equal(2);
      expect(entries[0].rank).to.equal(2);
      expect(entries[1].rank).to.equal(3);
    });

    it("Should get player entry", async function () {
      const { leaderboard, gameCore, player1 } = await loadFixture(deployLeaderboardFixture);

      await leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1);

      const entry = await leaderboard.getPlayerEntry(player1.address);

      expect(entry.player).to.equal(player1.address);
      expect(entry.highScore).to.equal(1000);
      expect(entry.sessionId).to.equal(1);
      expect(entry.rank).to.equal(1);
    });

    it("Should get player score for specific season", async function () {
      const { leaderboard, gameCore, player1, owner } = await loadFixture(deployLeaderboardFixture);

      await leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1);

      expect(await leaderboard.getPlayerHighScoreSeason(player1.address, 1)).to.equal(1000);

      // Start new season
      await leaderboard.connect(owner).startNewSeason();

      await leaderboard.connect(gameCore).updateScore(player1.address, 1500, 2);

      expect(await leaderboard.getPlayerHighScoreSeason(player1.address, 1)).to.equal(1000);
      expect(await leaderboard.getPlayerHighScoreSeason(player1.address, 2)).to.equal(1500);
    });

    it("Should get rankings count", async function () {
      const { leaderboard, gameCore, player1, player2, player3 } = await loadFixture(deployLeaderboardFixture);

      expect(await leaderboard.getRankingsCount()).to.equal(0);

      await leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1);
      expect(await leaderboard.getRankingsCount()).to.equal(1);

      await leaderboard.connect(gameCore).updateScore(player2.address, 1500, 2);
      await leaderboard.connect(gameCore).updateScore(player3.address, 800, 3);
      expect(await leaderboard.getRankingsCount()).to.equal(3);
    });
  });

  describe("Season Management", function () {
    it("Should start a new season", async function () {
      const { leaderboard, owner } = await loadFixture(deployLeaderboardFixture);

      expect(await leaderboard.currentSeason()).to.equal(1);

      await expect(leaderboard.connect(owner).startNewSeason())
        .to.emit(leaderboard, "NewSeasonStarted")
        .withArgs(2, await ethers.provider.getBlock("latest").then(b => b.timestamp + 1));

      expect(await leaderboard.currentSeason()).to.equal(2);
    });

    it("Should preserve previous season data", async function () {
      const { leaderboard, gameCore, player1, owner } = await loadFixture(deployLeaderboardFixture);

      await leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1);

      await leaderboard.connect(owner).startNewSeason();

      // Season 1 data should still exist
      expect(await leaderboard.getPlayerHighScoreSeason(player1.address, 1)).to.equal(1000);

      // Season 2 should be fresh
      expect(await leaderboard.getPlayerHighScore(player1.address)).to.equal(0);
    });

    it("Should allow separate rankings per season", async function () {
      const { leaderboard, gameCore, player1, player2, owner } = await loadFixture(deployLeaderboardFixture);

      // Season 1
      await leaderboard.connect(gameCore).updateScore(player1.address, 1000, 1);
      await leaderboard.connect(gameCore).updateScore(player2.address, 500, 2);

      const [season1Players] = await leaderboard.getTopPlayers(2);

      // Start Season 2
      await leaderboard.connect(owner).startNewSeason();

      // Season 2 - different rankings
      await leaderboard.connect(gameCore).updateScore(player2.address, 2000, 3);
      await leaderboard.connect(gameCore).updateScore(player1.address, 800, 4);

      const [season2Players] = await leaderboard.getTopPlayers(2);

      // Rankings should be different per season
      expect(season1Players[0]).to.equal(player1.address);
      expect(season2Players[0]).to.equal(player2.address);
    });

    it("Should only allow owner to start new season", async function () {
      const { leaderboard, player1 } = await loadFixture(deployLeaderboardFixture);

      await expect(leaderboard.connect(player1).startNewSeason())
        .to.be.revertedWithCustomError(leaderboard, "OwnableUnauthorizedAccount");
    });
  });

  describe("Admin Functions", function () {
    it("Should allow owner to set GameCore address", async function () {
      const { leaderboard, owner, player1 } = await loadFixture(deployLeaderboardFixture);

      await expect(leaderboard.connect(owner).setGameCore(player1.address))
        .to.emit(leaderboard, "GameCoreUpdated")
        .withArgs(player1.address);

      expect(await leaderboard.gameCoreAddress()).to.equal(player1.address);
    });

    it("Should fail to set zero address as GameCore", async function () {
      const { leaderboard, owner } = await loadFixture(deployLeaderboardFixture);

      await expect(leaderboard.connect(owner).setGameCore(ethers.ZeroAddress))
        .to.be.revertedWithCustomError(leaderboard, "InvalidAddress");
    });

    it("Should only allow owner to set GameCore", async function () {
      const { leaderboard, player1 } = await loadFixture(deployLeaderboardFixture);

      await expect(leaderboard.connect(player1).setGameCore(player1.address))
        .to.be.revertedWithCustomError(leaderboard, "OwnableUnauthorizedAccount");
    });
  });

  describe("Complex Scenarios", function () {
    it("Should handle 10+ players correctly", async function () {
      const { leaderboard, gameCore } = await loadFixture(deployLeaderboardFixture);

      const signers = await ethers.getSigners();
      const players = signers.slice(2, 12); // Get 10 players

      // Add scores for 10 players with varying scores
      for (let i = 0; i < players.length; i++) {
        await leaderboard.connect(gameCore).updateScore(
          players[i].address,
          1000 + (i * 100),
          i + 1
        );
      }

      const [topPlayers, topScores] = await leaderboard.getTopPlayers(10);

      expect(topPlayers.length).to.equal(10);
      // Verify descending order
      for (let i = 0; i < topScores.length - 1; i++) {
        expect(topScores[i]).to.be.greaterThan(topScores[i + 1]);
      }
    });

    it("Should handle multiple score updates efficiently", async function () {
      const { leaderboard, gameCore, player1 } = await loadFixture(deployLeaderboardFixture);

      // Update same player multiple times
      for (let i = 0; i < 5; i++) {
        await leaderboard.connect(gameCore).updateScore(player1.address, 1000 + (i * 100), i + 1);
      }

      expect(await leaderboard.getPlayerHighScore(player1.address)).to.equal(1400);
      expect(await leaderboard.getTotalPlayers()).to.equal(1);
    });
  });
});

