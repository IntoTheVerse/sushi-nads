import { chefNFT, gameCore, leaderboard } from '../src/services/blockchain.service.js';

function toNumber(value) {
    if (value === undefined || value === null) {
        return 0;
    }

    try {
        return Number(value);
    } catch {
        return 0;
    }
}

async function getChefRoster() {
    try {
        const totalChefs = Number(await chefNFT.totalChefs());
        console.log(`\n👩‍🍳 Total minted chefs: ${totalChefs}\n`);

        if (totalChefs === 0) {
            console.log('No chefs minted yet.');
            return;
        }

        const rows = [];

        for (let tokenId = 1; tokenId <= totalChefs; tokenId++) {
            try {
                const [owner, name] = await Promise.all([
                    chefNFT.ownerOf(tokenId),
                    chefNFT.chefNames(tokenId)
                ]);

                const [energy, stats, sessionIds, entry] = await Promise.all([
                    chefNFT.getPlayerEnergy(owner).catch(() => 0),
                    gameCore.getPlayerGlobalStats(owner).catch(() => null),
                    gameCore.getPlayerSessionHistory(owner).catch(() => []),
                    leaderboard.getPlayerEntry(owner).catch(() => null)
                ]);

                const lastSessionId = Array.isArray(sessionIds) && sessionIds.length
                    ? Number(sessionIds[sessionIds.length - 1])
                    : 0;

                rows.push({
                    tokenId,
                    owner,
                    name: name || '(unnamed)',
                    energy: toNumber(energy),
                    sessions: stats ? toNumber(stats.totalSessionsPlayed) : 0,
                    recipesDelivered: stats ? toNumber(stats.totalRecipesDelivered) : 0,
                    recipesDiscarded: stats ? toNumber(stats.totalRecipesDiscarded) : 0,
                    bestScore: entry ? toNumber(entry.highScore) : 0,
                    lastSession: lastSessionId || '—',
                    lastUpdated: entry && entry.timestamp ? new Date(Number(entry.timestamp) * 1000).toISOString() : '—'
                });
            } catch (error) {
                console.warn(`⚠️  Unable to read chef #${tokenId}: ${error.message}`);
            }
        }

        if (!rows.length) {
            console.log('No chef entries could be fetched.');
            return;
        }

        console.table(rows);
    } catch (error) {
        console.error('❌ Failed to fetch chef roster:', error);
        process.exit(1);
    }
}

getChefRoster().then(() => process.exit(0));


