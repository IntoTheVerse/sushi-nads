import express from 'express';
import cors from 'cors';
import helmet from 'helmet';
import dotenv from 'dotenv';

// Load environment variables
dotenv.config();

// Import configuration
import { initializeFirebase, getDb } from './config/firebase.js';
import { checkNetworkConnection, activeNetwork } from './services/blockchain.service.js';

// Import middleware
import { rateLimitByIP } from './middleware/rateLimit.js';
import { errorHandler, notFoundHandler } from './middleware/errorHandler.js';

// Import routes
import chefRoutes from './routes/chef.routes.js';
import sessionRoutes from './routes/session.routes.js';
import gameplayRoutes from './routes/gameplay.routes.js';
import leaderboardRoutes from './routes/leaderboard.routes.js';
import configRoutes from './routes/config.routes.js';

// Initialize Firebase
try {
    initializeFirebase();
} catch (error) {
    console.error('⚠️  Firebase initialization failed. Some features may not work.');
}

// Check blockchain connection
checkNetworkConnection();

// Create Express app
const app = express();
const PORT = process.env.PORT || 3000;
const serverStartedAt = Date.now();

// Security middleware
app.use(helmet());

// CORS configuration
const allowedOrigins = process.env.ALLOWED_ORIGINS?.split(',') || ['*'];
app.use(cors({
    origin: (origin, callback) => {
        // Allow requests with no origin (like mobile apps or curl)
        if (!origin) return callback(null, true);
        
        if (allowedOrigins.includes('*') || allowedOrigins.includes(origin)) {
            callback(null, true);
        } else {
            callback(new Error('Not allowed by CORS'));
        }
    },
    credentials: true
}));

// Body parsing
app.use(express.json());
app.use(express.urlencoded({ extended: true }));

// Global rate limiting
app.use(rateLimitByIP);

// Request logging
app.use((req, res, next) => {
    console.log(`${req.method} ${req.path}`, req.body);
    next();
});

// Health checks
function buildBaseHealthPayload() {
    return {
        status: 'ok',
        service: 'sushi-bonks-api',
        environment: process.env.NODE_ENV || 'development',
        network: activeNetwork.name,
        timestamp: new Date().toISOString(),
        uptimeSeconds: Math.floor((Date.now() - serverStartedAt) / 1000),
    };
}

async function buildDeepHealthPayload() {
    const base = buildBaseHealthPayload();
    const firebaseEnabled = Boolean(process.env.FIREBASE_PROJECT_ID);
    const dependencies = {
        firebase: {
            enabled: firebaseEnabled,
            status: firebaseEnabled
                ? (getDb() ? 'ok' : 'unavailable')
                : 'disabled',
        },
        blockchain: {
            status: 'unknown',
        },
    };

    const networkOk = await checkNetworkConnection();
    dependencies.blockchain.status = networkOk ? 'ok' : 'error';

    const firebaseOkOrDisabled =
        dependencies.firebase.status === 'ok' ||
        dependencies.firebase.status === 'disabled';

    const overallOk =
        dependencies.blockchain.status === 'ok' &&
        firebaseOkOrDisabled;

    return {
        ...base,
        status: overallOk ? 'ok' : 'degraded',
        dependencies,
    };
}

app.get('/api/health', async (req, res) => {
    const deep = req.query.deep === '1' || req.query.deep === 'true';
    if (!deep) {
        return res.json(buildBaseHealthPayload());
    }

    try {
        const payload = await buildDeepHealthPayload();
        const statusCode = payload.status === 'ok' ? 200 : 503;
        return res.status(statusCode).json(payload);
    } catch (error) {
        return res.status(503).json({
            ...buildBaseHealthPayload(),
            status: 'error',
            error: 'Deep health check failed',
            message: error.message,
        });
    }
});

// Backward-compatible alias for infra checks.
app.get('/health', (req, res) => {
    res.json(buildBaseHealthPayload());
});

// API Routes
app.use('/api/chef', chefRoutes);
app.use('/api/session', sessionRoutes);
app.use('/api/gameplay', gameplayRoutes);
app.use('/api/leaderboard', leaderboardRoutes);
app.use('/api/config', configRoutes);

// 404 handler
app.use(notFoundHandler);

// Error handler (must be last)
app.use(errorHandler);

// Start server
app.listen(PORT, () => {
    console.log('');
    console.log('🍣 ====================================');
    console.log('   SUSHI BONKS API SERVER');
    console.log('   ====================================');
    console.log('');
    console.log('   🚀 Server running');
    console.log(`   📡 Port: ${PORT}`);
    console.log(`   🌍 Environment: ${process.env.NODE_ENV || 'development'}`);
    console.log(`   ⛓️  Network: ${activeNetwork.name}`);
    console.log(`   🔐 Privy App ID: ${process.env.PRIVY_APP_ID}`);
    console.log('');
    console.log('   📋 Contract Addresses:');
    console.log(`   ChefNFT:     ${activeNetwork.contracts.chefNFT}`);
    console.log(`   GameCore:    ${activeNetwork.contracts.gameCore}`);
    console.log(`   Leaderboard: ${activeNetwork.contracts.leaderboard}`);
    console.log('');
    console.log('   📚 API Endpoints:');
    console.log('   POST   /api/chef/mint');
    console.log('   GET    /api/chef/profile');
    console.log('   POST   /api/session/start');
    console.log('   POST   /api/session/end');
    console.log('   POST   /api/gameplay/move');
    console.log('   POST   /api/gameplay/recipe-delivered');
    console.log('   GET    /api/leaderboard/top');
    console.log('');
    console.log('   ✅ Ready to accept requests!');
    console.log('   ====================================');
    console.log('');
});

// Graceful shutdown
process.on('SIGTERM', () => {
    console.log('⚠️  SIGTERM received, shutting down gracefully');
    process.exit(0);
});

process.on('SIGINT', () => {
    console.log('\n⚠️  SIGINT received, shutting down gracefully');
    process.exit(0);
});

export default app;

