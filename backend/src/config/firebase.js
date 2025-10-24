import admin from 'firebase-admin';

let firebaseInitialized = false;
let db = null;
let auth = null;

export function initializeFirebase() {
    if (firebaseInitialized) {
        console.log('⚠️  Firebase already initialized');
        return true;
    }

    // Check if Firebase credentials are provided
    if (!process.env.FIREBASE_CREDENTIALS || process.env.FIREBASE_CREDENTIALS === '{}') {
        console.warn('⚠️  Firebase credentials not provided - caching disabled');
        return false;
    }

    try {
        const serviceAccount = JSON.parse(process.env.FIREBASE_CREDENTIALS);

        admin.initializeApp({
            credential: admin.credential.cert(serviceAccount),
            projectId: process.env.FIREBASE_PROJECT_ID
        });

        db = admin.firestore();
        auth = admin.auth();
        
        firebaseInitialized = true;
        console.log('✅ Firebase initialized successfully');
        return true;
    } catch (error) {
        console.error('❌ Firebase initialization failed:', error.message);
        return false;
    }
}

// Export getters that return null if not initialized
export function getDb() {
    return db;
}

export function getAuth() {
    return auth;
}

export { db, auth };
export default admin;


