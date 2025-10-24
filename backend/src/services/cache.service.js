import { getDb } from '../config/firebase.js';
import { CACHE_TTL } from '../config/constants.js';

/**
 * Cache player energy
 */
export async function getCachedEnergy(address) {
    const db = getDb();
    if (!db) return null; // Firebase not initialized
    
    try {
        const doc = await db.collection('players').doc(address).get();
        
        if (doc.exists) {
            const data = doc.data();
            const age = Date.now() - data.timestamp;
            
            // Cache valid for 1 minute
            if (age < CACHE_TTL.energy) {
                return data.energy;
            }
        }
        
        return null;
    } catch (error) {
        console.error('Get cached energy error:', error);
        return null;
    }
}

export async function setCachedEnergy(address, energy) {
    const db = getDb();
    if (!db) return; // Firebase not initialized
    
    try {
        await db.collection('players').doc(address).set({
            energy,
            timestamp: Date.now()
        }, { merge: true });
    } catch (error) {
        console.error('Set cached energy error:', error);
    }
}

/**
 * Cache session state
 */
export async function getCachedSession(sessionId) {
    const db = getDb();
    if (!db) return null; // Firebase not initialized
    
    try {
        const doc = await db.collection('sessions').doc(sessionId.toString()).get();
        return doc.exists ? doc.data() : null;
    } catch (error) {
        console.error('Get cached session error:', error);
        return null;
    }
}

export async function setCachedSession(sessionId, data) {
    const db = getDb();
    if (!db) return; // Firebase not initialized
    
    try {
        await db.collection('sessions').doc(sessionId.toString()).set({
            ...data,
            updatedAt: Date.now()
        }, { merge: true });
    } catch (error) {
        console.error('Set cached session error:', error);
    }
}

export async function deleteCachedSession(sessionId) {
    const db = getDb();
    if (!db) return; // Firebase not initialized
    
    try {
        await db.collection('sessions').doc(sessionId.toString()).delete();
    } catch (error) {
        console.error('Delete cached session error:', error);
    }
}

/**
 * Cache leaderboard
 */
export async function getCachedLeaderboard(season, limit) {
    const db = getDb();
    if (!db) return null; // Firebase not initialized
    
    try {
        const cacheKey = `leaderboard_${season}_${limit}`;
        const doc = await db.collection('cache').doc(cacheKey).get();
        
        if (doc.exists) {
            const data = doc.data();
            const age = Date.now() - data.timestamp;
            
            // Cache valid for 5 minutes
            if (age < CACHE_TTL.leaderboard) {
                return data.rankings;
            }
        }
        
        return null;
    } catch (error) {
        console.error('Get cached leaderboard error:', error);
        return null;
    }
}

export async function setCachedLeaderboard(season, limit, rankings) {
    const db = getDb();
    if (!db) return; // Firebase not initialized
    
    try {
        const cacheKey = `leaderboard_${season}_${limit}`;
        await db.collection('cache').doc(cacheKey).set({
            rankings,
            timestamp: Date.now()
        });
    } catch (error) {
        console.error('Set cached leaderboard error:', error);
    }
}

/**
 * Cache player profile
 */
export async function getCachedProfile(address) {
    const db = getDb();
    if (!db) return null; // Firebase not initialized
    
    try {
        const doc = await db.collection('profiles').doc(address).get();
        
        if (doc.exists) {
            const data = doc.data();
            const age = Date.now() - data.timestamp;
            
            if (age < CACHE_TTL.profile) {
                return data.profile;
            }
        }
        
        return null;
    } catch (error) {
        console.error('Get cached profile error:', error);
        return null;
    }
}

export async function setCachedProfile(address, profile) {
    const db = getDb();
    if (!db) return; // Firebase not initialized
    
    try {
        await db.collection('profiles').doc(address).set({
            profile,
            timestamp: Date.now()
        });
    } catch (error) {
        console.error('Set cached profile error:', error);
    }
}

export default {
    getCachedEnergy,
    setCachedEnergy,
    getCachedSession,
    setCachedSession,
    deleteCachedSession,
    getCachedLeaderboard,
    setCachedLeaderboard,
    getCachedProfile,
    setCachedProfile,
};

