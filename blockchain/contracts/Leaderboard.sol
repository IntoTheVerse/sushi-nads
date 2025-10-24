// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import "@openzeppelin/contracts/access/Ownable.sol";

/**
 * @title Leaderboard
 * @dev Manages player scores and rankings across seasons
 * Only GameCore contract can update scores
 */
contract Leaderboard is Ownable {
    
    // ============ State Variables ============
    
    address public gameCoreAddress;
    uint256 public currentSeason;
    
    // ============ Structs ============
    
    struct LeaderboardEntry {
        address player;
        uint256 highScore;
        uint256 sessionId;
        uint256 timestamp;
        uint256 rank; // Cached rank (updated periodically)
    }
    
    // ============ Mappings ============
    
    // season => player => entry
    mapping(uint256 => mapping(address => LeaderboardEntry)) public seasonScores;
    
    // season => sorted array of players (top players)
    mapping(uint256 => address[]) public seasonRankings;
    
    // season => player => index in rankings array
    mapping(uint256 => mapping(address => uint256)) private rankingsIndex;
    
    // season => total players with scores
    mapping(uint256 => uint256) public seasonPlayerCount;
    
    // ============ Events ============
    
    event ScoreUpdated(
        uint256 indexed season,
        address indexed player,
        uint256 newScore,
        uint256 oldScore,
        uint256 sessionId
    );
    event NewSeasonStarted(uint256 indexed season, uint256 timestamp);
    event GameCoreUpdated(address indexed newGameCore);
    
    // ============ Errors ============
    
    error UnauthorizedCaller();
    error InvalidAddress();
    error InvalidSeason();
    
    // ============ Modifiers ============
    
    modifier onlyGameCore() {
        if (msg.sender != gameCoreAddress) revert UnauthorizedCaller();
        _;
    }
    
    // ============ Constructor ============
    
    constructor() Ownable(msg.sender) {
        currentSeason = 1;
    }
    
    // ============ Core Functions ============
    
    /**
     * @notice Update player's score (called by GameCore)
     * @param player The player address
     * @param score The new score
     * @param sessionId The session ID that generated this score
     */
    function updateScore(
        address player,
        uint256 score,
        uint256 sessionId
    ) external onlyGameCore {
        LeaderboardEntry storage entry = seasonScores[currentSeason][player];
        uint256 oldScore = entry.highScore;
        
        // Only update if new score is higher
        if (score > oldScore) {
            // If first time scoring in this season
            if (oldScore == 0) {
                seasonPlayerCount[currentSeason]++;
            }
            
            entry.player = player;
            entry.highScore = score;
            entry.sessionId = sessionId;
            entry.timestamp = block.timestamp;
            
            // Update rankings (simplified approach - full resort would be gas-intensive)
            _updateRankings(player, score, oldScore);
            
            emit ScoreUpdated(currentSeason, player, score, oldScore, sessionId);
        }
    }
    
    /**
     * @notice Internal function to update rankings
     * @dev Simplified ranking update - maintains top N players
     * @param player The player
     * @param newScore The new score
     * @param oldScore The previous score
     */
    function _updateRankings(address player, uint256 newScore, uint256 oldScore) internal {
        address[] storage rankings = seasonRankings[currentSeason];
        uint256 rankingsLength = rankings.length;
        
        // If player already in rankings, remove them first
        if (oldScore > 0) {
            uint256 oldIndex = rankingsIndex[currentSeason][player];
            if (oldIndex < rankingsLength && rankings[oldIndex] == player) {
                // Remove player by shifting array
                for (uint256 i = oldIndex; i < rankingsLength - 1; i++) {
                    rankings[i] = rankings[i + 1];
                    rankingsIndex[currentSeason][rankings[i]] = i;
                }
                rankings.pop();
                rankingsLength--;
            }
        }
        
        // Find insertion position for new score (descending order)
        uint256 insertPosition = rankingsLength;
        for (uint256 i = 0; i < rankingsLength; i++) {
            address rankedPlayer = rankings[i];
            uint256 rankedScore = seasonScores[currentSeason][rankedPlayer].highScore;
            
            if (newScore > rankedScore) {
                insertPosition = i;
                break;
            }
        }
        
        // Insert player at correct position
        rankings.push(player); // Add to end first
        
        // Shift elements to make space at insertPosition
        for (uint256 i = rankingsLength; i > insertPosition; i--) {
            rankings[i] = rankings[i - 1];
            rankingsIndex[currentSeason][rankings[i]] = i;
        }
        
        rankings[insertPosition] = player;
        rankingsIndex[currentSeason][player] = insertPosition;
        
        // Update cached ranks for affected players
        for (uint256 i = insertPosition; i < rankings.length; i++) {
            seasonScores[currentSeason][rankings[i]].rank = i + 1;
        }
    }
    
    // ============ View Functions ============
    
    /**
     * @notice Get player's high score for current season
     * @param player The player address
     * @return High score
     */
    function getPlayerHighScore(address player) external view returns (uint256) {
        return seasonScores[currentSeason][player].highScore;
    }
    
    /**
     * @notice Get player's high score for a specific season
     * @param player The player address
     * @param season The season number
     * @return High score
     */
    function getPlayerHighScoreSeason(address player, uint256 season) 
        external 
        view 
        returns (uint256) 
    {
        return seasonScores[season][player].highScore;
    }
    
    /**
     * @notice Get player's leaderboard entry for current season
     * @param player The player address
     * @return LeaderboardEntry
     */
    function getPlayerEntry(address player) external view returns (LeaderboardEntry memory) {
        return seasonScores[currentSeason][player];
    }
    
    /**
     * @notice Get player's rank for current season
     * @param player The player address
     * @return Rank (1-indexed, 0 if not ranked)
     */
    function getPlayerRank(address player) external view returns (uint256) {
        LeaderboardEntry memory entry = seasonScores[currentSeason][player];
        if (entry.highScore == 0) return 0;
        return entry.rank;
    }
    
    /**
     * @notice Get top N players for current season
     * @param limit Maximum number of players to return
     * @return players Array of player addresses
     * @return scores Array of corresponding scores
     */
    function getTopPlayers(uint256 limit) 
        external 
        view 
        returns (address[] memory players, uint256[] memory scores) 
    {
        address[] storage rankings = seasonRankings[currentSeason];
        uint256 count = rankings.length < limit ? rankings.length : limit;
        
        players = new address[](count);
        scores = new uint256[](count);
        
        for (uint256 i = 0; i < count; i++) {
            players[i] = rankings[i];
            scores[i] = seasonScores[currentSeason][rankings[i]].highScore;
        }
        
        return (players, scores);
    }
    
    /**
     * @notice Get top N players with full entries for current season
     * @param limit Maximum number of entries to return
     * @return Array of LeaderboardEntry
     */
    function getTopPlayersDetailed(uint256 limit) 
        external 
        view 
        returns (LeaderboardEntry[] memory) 
    {
        address[] storage rankings = seasonRankings[currentSeason];
        uint256 count = rankings.length < limit ? rankings.length : limit;
        
        LeaderboardEntry[] memory entries = new LeaderboardEntry[](count);
        
        for (uint256 i = 0; i < count; i++) {
            entries[i] = seasonScores[currentSeason][rankings[i]];
        }
        
        return entries;
    }
    
    /**
     * @notice Get players in a specific rank range
     * @param startRank Starting rank (1-indexed)
     * @param endRank Ending rank (1-indexed, inclusive)
     * @return Array of LeaderboardEntry
     */
    function getPlayersInRankRange(uint256 startRank, uint256 endRank) 
        external 
        view 
        returns (LeaderboardEntry[] memory) 
    {
        if (startRank == 0 || endRank < startRank) revert InvalidSeason();
        
        address[] storage rankings = seasonRankings[currentSeason];
        uint256 rankingsLength = rankings.length;
        
        // Adjust to 0-indexed
        uint256 startIndex = startRank - 1;
        uint256 endIndex = endRank - 1;
        
        if (startIndex >= rankingsLength) {
            return new LeaderboardEntry[](0);
        }
        
        if (endIndex >= rankingsLength) {
            endIndex = rankingsLength - 1;
        }
        
        uint256 count = endIndex - startIndex + 1;
        LeaderboardEntry[] memory entries = new LeaderboardEntry[](count);
        
        for (uint256 i = 0; i < count; i++) {
            entries[i] = seasonScores[currentSeason][rankings[startIndex + i]];
        }
        
        return entries;
    }
    
    /**
     * @notice Get total number of ranked players for current season
     * @return Total players
     */
    function getTotalPlayers() external view returns (uint256) {
        return seasonPlayerCount[currentSeason];
    }
    
    /**
     * @notice Get total number of players in rankings array for current season
     * @return Rankings count
     */
    function getRankingsCount() external view returns (uint256) {
        return seasonRankings[currentSeason].length;
    }
    
    // ============ Admin Functions ============
    
    /**
     * @notice Set the GameCore contract address
     * @param _gameCoreAddress Address of the GameCore contract
     */
    function setGameCore(address _gameCoreAddress) external onlyOwner {
        if (_gameCoreAddress == address(0)) revert InvalidAddress();
        gameCoreAddress = _gameCoreAddress;
        emit GameCoreUpdated(_gameCoreAddress);
    }
    
    /**
     * @notice Start a new season
     * @dev Previous season data is preserved, new season starts fresh
     */
    function startNewSeason() external onlyOwner {
        currentSeason++;
        emit NewSeasonStarted(currentSeason, block.timestamp);
    }
    
    /**
     * @notice Get current season number
     * @return Season number
     */
    function getCurrentSeason() external view returns (uint256) {
        return currentSeason;
    }
}

