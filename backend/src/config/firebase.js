import admin from 'firebase-admin';

let firebaseInitialized = false;

export function initializeFirebase() {
    if (firebaseInitialized) {
        console.log('⚠️  Firebase already initialized');
        return;
    }

    try {
        const serviceAccount = JSON.parse(process.env.FIREBASE_CREDENTIALS || '{}');

        admin.initializeApp({
            credential: admin.credential.cert(serviceAccount),
            projectId: process.env.FIREBASE_PROJECT_ID
        });

        firebaseInitialized = true;
        console.log('✅ Firebase initialized successfully');
    } catch (error) {
        console.error('❌ Firebase initialization failed:', error.message);
        throw error;
    }
}

export const db = admin.firestore();
export const auth = admin.auth();

export default admin;

