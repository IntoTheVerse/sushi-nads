// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import "@openzeppelin/contracts/token/ERC721/ERC721.sol";
import "@openzeppelin/contracts/token/ERC20/IERC20.sol";
import "@openzeppelin/contracts/access/Ownable.sol";
import "@openzeppelin/contracts/utils/ReentrancyGuard.sol";

/**
 * @title ChefNFT
 * @dev NFT contract for Sushi Bonks game chefs with energy management
 * Each wallet can only mint one chef NFT
 */
contract ChefNFT is ERC721, Ownable, ReentrancyGuard {
    
    // ============ State Variables ============
    
    uint256 private _nextTokenId;
    
    // USDC token for energy refills (6 decimals on Base)
    IERC20 public immutable usdcToken;
    
    // Energy system constants
    uint256 public constant ENERGY_REFILL_AMOUNT = 300;
    uint256 public energyRefillCost; // in USDC (with 6 decimals)
    
    // GameCore contract address (authorized to consume energy)
    address public gameCoreAddress;
    
    // Mappings
    mapping(address => uint256) public playerToTokenId; // wallet => tokenId
    mapping(uint256 => string) public chefNames; // tokenId => name
    mapping(address => uint256) public playerEnergy; // wallet => energy amount
    mapping(address => bool) public hasChef; // wallet => has minted
    
    // ============ Events ============
    
    event ChefMinted(address indexed player, uint256 indexed tokenId, string name);
    event NameUpdated(uint256 indexed tokenId, string newName);
    event EnergyRefilled(address indexed player, uint256 amount, uint256 newTotal);
    event EnergyConsumed(address indexed player, uint256 amount, uint256 remaining);
    event GameCoreUpdated(address indexed newGameCore);
    event EnergyRefillCostUpdated(uint256 newCost);
    
    // ============ Errors ============
    
    error AlreadyHasChef();
    error NoChefMinted();
    error InvalidName();
    error InsufficientEnergy();
    error InsufficientUSDC();
    error InvalidGameCore();
    error UnauthorizedCaller();
    error TransferNotAllowed();
    
    // ============ Modifiers ============
    
    modifier onlyGameCore() {
        if (msg.sender != gameCoreAddress) revert UnauthorizedCaller();
        _;
    }
    
    // ============ Constructor ============
    
    /**
     * @param _usdcAddress USDC token address on Base Sepolia
     * @param _initialEnergyCost Initial cost in USDC (with 6 decimals, e.g., 100000 = 0.1 USDC)
     */
    constructor(
        address _usdcAddress,
        uint256 _initialEnergyCost
    ) ERC721("Sushi Bonks Chef", "CHEF") Ownable(msg.sender) {
        usdcToken = IERC20(_usdcAddress);
        energyRefillCost = _initialEnergyCost;
        _nextTokenId = 1; // Start from token ID 1
    }
    
    // ============ External Functions ============
    
    /**
     * @notice Mint a chef NFT with a name (one per wallet, free except gas)
     * @param name The name for the chef (3-20 characters)
     */
    function mintChef(string memory name) external nonReentrant {
        if (hasChef[msg.sender]) revert AlreadyHasChef();
        if (bytes(name).length < 3 || bytes(name).length > 20) revert InvalidName();
        
        uint256 tokenId = _nextTokenId++;
        
        _safeMint(msg.sender, tokenId);
        
        playerToTokenId[msg.sender] = tokenId;
        chefNames[tokenId] = name;
        hasChef[msg.sender] = true;
        playerEnergy[msg.sender] = 0; // Start with 0 energy, must refill to play
        
        emit ChefMinted(msg.sender, tokenId, name);
    }
    
    /**
     * @notice Update the chef's name
     * @param newName New name for the chef (3-20 characters)
     */
    function updateChefName(string memory newName) external {
        if (!hasChef[msg.sender]) revert NoChefMinted();
        if (bytes(newName).length < 3 || bytes(newName).length > 20) revert InvalidName();
        
        uint256 tokenId = playerToTokenId[msg.sender];
        chefNames[tokenId] = newName;
        
        emit NameUpdated(tokenId, newName);
    }
    
    /**
     * @notice Refill energy by paying USDC
     * @dev Player must have approved this contract to spend USDC
     */
    function refillEnergy() external nonReentrant {
        if (!hasChef[msg.sender]) revert NoChefMinted();
        
        // Transfer USDC from player to owner (treasury)
        bool success = usdcToken.transferFrom(msg.sender, owner(), energyRefillCost);
        if (!success) revert InsufficientUSDC();
        
        // Add energy
        playerEnergy[msg.sender] += ENERGY_REFILL_AMOUNT;
        
        emit EnergyRefilled(msg.sender, ENERGY_REFILL_AMOUNT, playerEnergy[msg.sender]);
    }
    
    /**
     * @notice Consume energy (called by GameCore contract)
     * @param player The player whose energy to consume
     * @param amount Amount of energy to consume
     */
    function consumeEnergy(address player, uint256 amount) external onlyGameCore {
        if (playerEnergy[player] < amount) revert InsufficientEnergy();
        
        playerEnergy[player] -= amount;
        
        emit EnergyConsumed(player, amount, playerEnergy[player]);
    }
    
    // ============ View Functions ============
    
    /**
     * @notice Get player's current energy
     * @param player The player address
     * @return Current energy amount
     */
    function getPlayerEnergy(address player) external view returns (uint256) {
        return playerEnergy[player];
    }
    
    /**
     * @notice Get chef token ID for a player
     * @param player The player address
     * @return Token ID (0 if no chef)
     */
    function getChefTokenId(address player) external view returns (uint256) {
        return playerToTokenId[player];
    }
    
    /**
     * @notice Get chef name by token ID
     * @param tokenId The token ID
     * @return Chef name
     */
    function getChefName(uint256 tokenId) external view returns (string memory) {
        return chefNames[tokenId];
    }
    
    /**
     * @notice Check if player has a chef
     * @param player The player address
     * @return True if player has minted a chef
     */
    function playerHasChef(address player) external view returns (bool) {
        return hasChef[player];
    }
    
    /**
     * @notice Get total number of chefs minted
     * @return Total chefs
     */
    function totalChefs() external view returns (uint256) {
        return _nextTokenId - 1;
    }
    
    // ============ Admin Functions ============
    
    /**
     * @notice Set the GameCore contract address
     * @param _gameCoreAddress Address of the GameCore contract
     */
    function setGameCore(address _gameCoreAddress) external onlyOwner {
        if (_gameCoreAddress == address(0)) revert InvalidGameCore();
        gameCoreAddress = _gameCoreAddress;
        emit GameCoreUpdated(_gameCoreAddress);
    }
    
    /**
     * @notice Update energy refill cost
     * @param newCost New cost in USDC (with 6 decimals)
     */
    function setEnergyRefillCost(uint256 newCost) external onlyOwner {
        energyRefillCost = newCost;
        emit EnergyRefillCostUpdated(newCost);
    }
    
    /**
     * @notice Emergency function to add energy to a player (only owner)
     * @param player The player address
     * @param amount Amount of energy to add
     */
    function emergencyAddEnergy(address player, uint256 amount) external onlyOwner {
        playerEnergy[player] += amount;
        emit EnergyRefilled(player, amount, playerEnergy[player]);
    }
    
    // ============ Override Functions ============
    
    /**
     * @dev Override transfer functions to make NFT soulbound (non-transferable)
     */
    function _update(
        address to,
        uint256 tokenId,
        address auth
    ) internal virtual override returns (address) {
        address from = _ownerOf(tokenId);
        
        // Allow minting (from == address(0)) but block transfers
        if (from != address(0) && to != address(0)) {
            revert TransferNotAllowed();
        }
        
        return super._update(to, tokenId, auth);
    }
}

