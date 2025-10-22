// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import "@openzeppelin/contracts/access/Ownable.sol";
import "@openzeppelin/contracts/utils/ReentrancyGuard.sol";
import "./ChefNFT.sol";
import "./Leaderboard.sol";

/**
 * @title GameCore
 * @dev Main game logic contract for Sushi Bonks
 * Handles game sessions, moves, recipe tracking, cooking quality, and scoring
 */
contract GameCore is Ownable, ReentrancyGuard {
    
    // ============ State Variables ============
    
    ChefNFT public immutable chefNFT;
    Leaderboard public leaderboard;
    
    // Backend signer address (authorized to call gameplay functions)
    address public backendSigner;
    
    // Game constants
    uint256 public constant GAME_DURATION = 1000; // seconds
    uint256 public constant MOVE_COST = 1; // energy per move
    
    // Scoring configuration (can be adjusted by owner)
    int256 public deliveryBonus = 100;
    int256 public cookBonus = 10;
    int256 public discardPenalty = 50;
    int256 public overcookPenalty = 20;
    int256 public movePenalty = 1;
    
    // Session tracking
    uint256 private _nextSessionId;
    
    // ============ Structs ============
    
    struct GameSession {
        uint256 sessionId;
        address player;
        uint256 startTime;
        uint256 endTime;
        bool isActive;
        
        // Per-session counters
        uint256 moveCount;
        uint256 recipesSpawned;
        uint256 recipesDelivered;
        uint256 recipesDiscarded;
        uint256 wellCookedCount;
        uint256 overCookedCount;
        
        // Combo tracking
        uint256 consecutivePerfectDeliveries;
        uint256 maxComboAchieved;
        uint256 lastActionWasDiscard; // 0 = no, 1 = yes
        
        // Scoring
        uint256 finalScore;
        bool scoreFinalized;
    }
    
    struct PlayerGlobalStats {
        uint256 totalRecipesSpawned;
        uint256 totalRecipesDelivered;
        uint256 totalRecipesDiscarded;
        uint256 totalWellCooked;
        uint256 totalOverCooked;
        uint256 totalMoves;
        uint256 totalSessionsPlayed;
    }
    
    // ============ Mappings ============
    
    mapping(uint256 => GameSession) public sessions;
    mapping(address => uint256) public currentSessionId; // player => active session ID
    mapping(address => uint256[]) public playerSessionHistory;
    mapping(address => PlayerGlobalStats) public globalStats;
    
    // ============ Events ============
    
    event SessionStarted(uint256 indexed sessionId, address indexed player, uint256 startTime);
    event SessionEnded(uint256 indexed sessionId, address indexed player, uint256 endTime, string reason);
    event MoveMade(uint256 indexed sessionId, uint256 moveCount, uint256 energyRemaining);
    event RecipeSpawned(uint256 indexed sessionId, uint256 totalSpawned);
    event RecipeDelivered(uint256 indexed sessionId, uint256 totalDelivered, uint256 combo);
    event RecipeDiscarded(uint256 indexed sessionId, uint256 totalDiscarded);
    event WellCooked(uint256 indexed sessionId, uint256 totalWellCooked);
    event OverCooked(uint256 indexed sessionId, uint256 totalOverCooked);
    event ScoreFinalized(uint256 indexed sessionId, address indexed player, uint256 score);
    event BackendSignerUpdated(address indexed newSigner);
    event LeaderboardUpdated(address indexed newLeaderboard);
    event ScoringConfigUpdated(int256 delivery, int256 cook, int256 discard, int256 overcook, int256 move);
    
    // ============ Errors ============
    
    error NoChefNFT();
    error InsufficientEnergy();
    error SessionAlreadyActive();
    error SessionNotActive();
    error SessionNotFound();
    error ScoreAlreadyFinalized();
    error UnauthorizedCaller();
    error InvalidAddress();
    error InvalidSessionState();
    
    // ============ Modifiers ============
    
    modifier onlyBackend() {
        if (msg.sender != backendSigner) revert UnauthorizedCaller();
        _;
    }
    
    modifier hasChef(address player) {
        if (!chefNFT.playerHasChef(player)) revert NoChefNFT();
        _;
    }
    
    modifier sessionActive(uint256 sessionId) {
        if (!sessions[sessionId].isActive) revert SessionNotActive();
        _;
    }
    
    // ============ Constructor ============
    
    constructor(
        address _chefNFTAddress,
        address _backendSigner
    ) Ownable(msg.sender) {
        if (_chefNFTAddress == address(0) || _backendSigner == address(0)) {
            revert InvalidAddress();
        }
        
        chefNFT = ChefNFT(_chefNFTAddress);
        backendSigner = _backendSigner;
        _nextSessionId = 1;
    }
    
    // ============ Session Management ============
    
    /**
     * @notice Start a new game session
     * @param player The player starting the session
     * @return sessionId The new session ID
     */
    function startGameSession(address player) 
        external 
        onlyBackend 
        nonReentrant 
        hasChef(player) 
        returns (uint256) 
    {
        // Check if player already has an active session
        uint256 existingSessionId = currentSessionId[player];
        if (existingSessionId != 0 && sessions[existingSessionId].isActive) {
            revert SessionAlreadyActive();
        }
        
        // Check if player has enough energy for at least a few moves
        uint256 playerEnergy = chefNFT.getPlayerEnergy(player);
        if (playerEnergy < MOVE_COST * 5) revert InsufficientEnergy(); // Require at least 5 moves worth
        
        uint256 sessionId = _nextSessionId++;
        
        sessions[sessionId] = GameSession({
            sessionId: sessionId,
            player: player,
            startTime: block.timestamp,
            endTime: 0,
            isActive: true,
            moveCount: 0,
            recipesSpawned: 0,
            recipesDelivered: 0,
            recipesDiscarded: 0,
            wellCookedCount: 0,
            overCookedCount: 0,
            consecutivePerfectDeliveries: 0,
            maxComboAchieved: 0,
            lastActionWasDiscard: 0,
            finalScore: 0,
            scoreFinalized: false
        });
        
        currentSessionId[player] = sessionId;
        playerSessionHistory[player].push(sessionId);
        globalStats[player].totalSessionsPlayed++;
        
        emit SessionStarted(sessionId, player, block.timestamp);
        
        return sessionId;
    }
    
    /**
     * @notice End a game session and compute score
     * @param sessionId The session to end
     * @param reason Reason for ending (e.g., "TIME_UP", "OUT_OF_ENERGY")
     */
    function endGameSession(uint256 sessionId, string memory reason) 
        external 
        onlyBackend 
        nonReentrant 
        sessionActive(sessionId) 
    {
        GameSession storage session = sessions[sessionId];
        
        session.endTime = block.timestamp;
        session.isActive = false;
        
        // Compute and finalize score
        _computeAndFinalizeScore(sessionId);
        
        emit SessionEnded(sessionId, session.player, block.timestamp, reason);
    }
    
    // ============ Gameplay Actions ============
    
    /**
     * @notice Record a player move
     * @param sessionId The active session ID
     */
    function recordMove(uint256 sessionId) 
        external 
        onlyBackend 
        sessionActive(sessionId) 
    {
        GameSession storage session = sessions[sessionId];
        
        // Check and consume energy
        uint256 currentEnergy = chefNFT.getPlayerEnergy(session.player);
        if (currentEnergy < MOVE_COST) revert InsufficientEnergy();
        
        chefNFT.consumeEnergy(session.player, MOVE_COST);
        
        // Record move
        session.moveCount++;
        globalStats[session.player].totalMoves++;
        
        emit MoveMade(sessionId, session.moveCount, currentEnergy - MOVE_COST);
    }
    
    /**
     * @notice Record a recipe spawned
     * @param sessionId The active session ID
     */
    function recordRecipeSpawned(uint256 sessionId) 
        external 
        onlyBackend 
        sessionActive(sessionId) 
    {
        GameSession storage session = sessions[sessionId];
        session.recipesSpawned++;
        globalStats[session.player].totalRecipesSpawned++;
        
        emit RecipeSpawned(sessionId, session.recipesSpawned);
    }
    
    /**
     * @notice Record a recipe successfully delivered
     * @param sessionId The active session ID
     */
    function recordRecipeDelivered(uint256 sessionId) 
        external 
        onlyBackend 
        sessionActive(sessionId) 
    {
        GameSession storage session = sessions[sessionId];
        session.recipesDelivered++;
        globalStats[session.player].totalRecipesDelivered++;
        
        // Update combo tracking
        if (session.lastActionWasDiscard == 0) {
            // Continue or start combo
            session.consecutivePerfectDeliveries++;
            if (session.consecutivePerfectDeliveries > session.maxComboAchieved) {
                session.maxComboAchieved = session.consecutivePerfectDeliveries;
            }
        } else {
            // Reset combo after discard
            session.consecutivePerfectDeliveries = 1;
            session.lastActionWasDiscard = 0;
        }
        
        emit RecipeDelivered(sessionId, session.recipesDelivered, session.consecutivePerfectDeliveries);
    }
    
    /**
     * @notice Record a recipe discarded
     * @param sessionId The active session ID
     */
    function recordRecipeDiscarded(uint256 sessionId) 
        external 
        onlyBackend 
        sessionActive(sessionId) 
    {
        GameSession storage session = sessions[sessionId];
        session.recipesDiscarded++;
        globalStats[session.player].totalRecipesDiscarded++;
        
        // Break combo
        session.consecutivePerfectDeliveries = 0;
        session.lastActionWasDiscard = 1;
        
        emit RecipeDiscarded(sessionId, session.recipesDiscarded);
    }
    
    /**
     * @notice Record a well-cooked ingredient
     * @param sessionId The active session ID
     */
    function recordWellCooked(uint256 sessionId) 
        external 
        onlyBackend 
        sessionActive(sessionId) 
    {
        GameSession storage session = sessions[sessionId];
        session.wellCookedCount++;
        globalStats[session.player].totalWellCooked++;
        
        emit WellCooked(sessionId, session.wellCookedCount);
    }
    
    /**
     * @notice Record an overcooked ingredient
     * @param sessionId The active session ID
     */
    function recordOverCooked(uint256 sessionId) 
        external 
        onlyBackend 
        sessionActive(sessionId) 
    {
        GameSession storage session = sessions[sessionId];
        session.overCookedCount++;
        globalStats[session.player].totalOverCooked++;
        
        emit OverCooked(sessionId, session.overCookedCount);
    }
    
    // ============ Scoring ============
    
    /**
     * @notice Internal function to compute and finalize score
     * @param sessionId The session to score
     */
    function _computeAndFinalizeScore(uint256 sessionId) internal {
        GameSession storage session = sessions[sessionId];
        
        if (session.scoreFinalized) revert ScoreAlreadyFinalized();
        
        // Calculate time bonus
        uint256 timeTaken = session.endTime - session.startTime;
        int256 timeBonus = 0;
        
        if (timeTaken <= GAME_DURATION) {
            uint256 timeRemaining = GAME_DURATION - timeTaken;
            timeBonus = int256(timeRemaining * 2); // 2 points per second saved
        }
        
        // Calculate combo bonus
        int256 comboBonus = _calculateComboBonus(session.maxComboAchieved);
        
        // Calculate base score
        int256 score = int256(session.recipesDelivered) * deliveryBonus
                     + int256(session.wellCookedCount) * cookBonus
                     - int256(session.recipesDiscarded) * discardPenalty
                     - int256(session.overCookedCount) * overcookPenalty
                     - int256(session.moveCount) * movePenalty
                     + timeBonus
                     + comboBonus;
        
        // Apply efficiency multiplier
        uint256 totalRecipes = session.recipesDelivered + session.recipesDiscarded;
        if (totalRecipes > 0) {
            uint256 efficiencyPct = (session.recipesDelivered * 100) / totalRecipes;
            if (efficiencyPct >= 90) {
                score = score * 120 / 100; // 20% bonus for 90%+ efficiency
            } else if (efficiencyPct >= 75) {
                score = score * 110 / 100; // 10% bonus for 75%+ efficiency
            }
        }
        
        // Ensure score is non-negative
        session.finalScore = score > 0 ? uint256(score) : 0;
        session.scoreFinalized = true;
        
        // Update leaderboard if set
        if (address(leaderboard) != address(0)) {
            leaderboard.updateScore(session.player, session.finalScore, sessionId);
        }
        
        emit ScoreFinalized(sessionId, session.player, session.finalScore);
    }
    
    /**
     * @notice Calculate combo bonus based on max combo achieved
     * @param maxCombo Maximum consecutive deliveries
     * @return Bonus points
     */
    function _calculateComboBonus(uint256 maxCombo) internal pure returns (int256) {
        if (maxCombo >= 10) return 500;  // 10+ streak
        if (maxCombo >= 7) return 300;   // 7-9 streak
        if (maxCombo >= 5) return 150;   // 5-6 streak
        if (maxCombo >= 3) return 50;    // 3-4 streak
        return 0;
    }
    
    // ============ View Functions ============
    
    /**
     * @notice Get session data
     * @param sessionId The session ID
     * @return Session data
     */
    function getSession(uint256 sessionId) external view returns (GameSession memory) {
        return sessions[sessionId];
    }
    
    /**
     * @notice Get player's current active session ID
     * @param player The player address
     * @return Session ID (0 if no active session)
     */
    function getCurrentSessionId(address player) external view returns (uint256) {
        uint256 sessionId = currentSessionId[player];
        if (sessionId != 0 && sessions[sessionId].isActive) {
            return sessionId;
        }
        return 0;
    }
    
    /**
     * @notice Get player's session history
     * @param player The player address
     * @return Array of session IDs
     */
    function getPlayerSessionHistory(address player) external view returns (uint256[] memory) {
        return playerSessionHistory[player];
    }
    
    /**
     * @notice Get player's global stats
     * @param player The player address
     * @return Global stats
     */
    function getPlayerGlobalStats(address player) external view returns (PlayerGlobalStats memory) {
        return globalStats[player];
    }
    
    /**
     * @notice Check if a session is active
     * @param sessionId The session ID
     * @return True if active
     */
    function isSessionActive(uint256 sessionId) external view returns (bool) {
        return sessions[sessionId].isActive;
    }
    
    /**
     * @notice Get total sessions created
     * @return Total sessions
     */
    function totalSessions() external view returns (uint256) {
        return _nextSessionId - 1;
    }
    
    // ============ Admin Functions ============
    
    /**
     * @notice Set the leaderboard contract
     * @param _leaderboardAddress Address of the Leaderboard contract
     */
    function setLeaderboard(address _leaderboardAddress) external onlyOwner {
        if (_leaderboardAddress == address(0)) revert InvalidAddress();
        leaderboard = Leaderboard(_leaderboardAddress);
        emit LeaderboardUpdated(_leaderboardAddress);
    }
    
    /**
     * @notice Update backend signer address
     * @param _newSigner New backend signer address
     */
    function setBackendSigner(address _newSigner) external onlyOwner {
        if (_newSigner == address(0)) revert InvalidAddress();
        backendSigner = _newSigner;
        emit BackendSignerUpdated(_newSigner);
    }
    
    /**
     * @notice Update scoring configuration
     * @param _deliveryBonus Points per recipe delivered
     * @param _cookBonus Points per well-cooked ingredient
     * @param _discardPenalty Penalty per recipe discarded
     * @param _overcookPenalty Penalty per overcooked ingredient
     * @param _movePenalty Penalty per move
     */
    function updateScoringConfig(
        int256 _deliveryBonus,
        int256 _cookBonus,
        int256 _discardPenalty,
        int256 _overcookPenalty,
        int256 _movePenalty
    ) external onlyOwner {
        deliveryBonus = _deliveryBonus;
        cookBonus = _cookBonus;
        discardPenalty = _discardPenalty;
        overcookPenalty = _overcookPenalty;
        movePenalty = _movePenalty;
        
        emit ScoringConfigUpdated(_deliveryBonus, _cookBonus, _discardPenalty, _overcookPenalty, _movePenalty);
    }
    
    /**
     * @notice Emergency function to manually end a stuck session
     * @param sessionId The session to end
     */
    function emergencyEndSession(uint256 sessionId) external onlyOwner {
        GameSession storage session = sessions[sessionId];
        if (!session.isActive) revert SessionNotActive();
        
        session.endTime = block.timestamp;
        session.isActive = false;
        
        _computeAndFinalizeScore(sessionId);
        
        emit SessionEnded(sessionId, session.player, block.timestamp, "EMERGENCY_END");
    }
}

