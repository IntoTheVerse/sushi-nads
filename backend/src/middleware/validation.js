/**
 * Validation middleware for request data
 */

export function validateMintChef(req, res, next) {
    const { playerName } = req.body;
    
    if (!playerName || typeof playerName !== 'string') {
        return res.status(400).json({ error: 'playerName is required and must be a string' });
    }
    
    if (playerName.length < 3 || playerName.length > 20) {
        return res.status(400).json({ error: 'Name must be 3-20 characters' });
    }
    
    // Sanitize name (alphanumeric + spaces only)
    if (!/^[a-zA-Z0-9\s]+$/.test(playerName)) {
        return res.status(400).json({ error: 'Name can only contain letters, numbers, and spaces' });
    }
    
    // Trim and normalize spaces
    req.body.playerName = playerName.trim().replace(/\s+/g, ' ');
    
    next();
}

export function validateSessionId(req, res, next) {
    const sessionId = req.body.sessionId || req.query.sessionId || req.params.sessionId;
    
    if (!sessionId) {
        return res.status(400).json({ error: 'sessionId is required' });
    }
    
    const parsedSessionId = parseInt(sessionId);
    
    if (isNaN(parsedSessionId) || parsedSessionId < 1) {
        return res.status(400).json({ error: 'Invalid sessionId' });
    }
    
    req.sessionId = parsedSessionId;
    next();
}

export function validateUpdateName(req, res, next) {
    const { newName } = req.body;
    
    if (!newName || typeof newName !== 'string') {
        return res.status(400).json({ error: 'newName is required and must be a string' });
    }
    
    if (newName.length < 3 || newName.length > 20) {
        return res.status(400).json({ error: 'Name must be 3-20 characters' });
    }
    
    if (!/^[a-zA-Z0-9\s]+$/.test(newName)) {
        return res.status(400).json({ error: 'Name can only contain letters, numbers, and spaces' });
    }
    
    req.body.newName = newName.trim().replace(/\s+/g, ' ');
    
    next();
}

export default {
    validateMintChef,
    validateSessionId,
    validateUpdateName,
};

