/**
 * Transaction Queue Manager
 * Prevents nonce conflicts by queuing transactions and processing them sequentially
 */

class TransactionQueue {
    constructor() {
        this.queue = [];
        this.processing = false;
        this.pendingNonce = null;
    }

    /**
     * Add a transaction to the queue
     * @param {Function} txFunction - Async function that returns a transaction
     * @param {string} description - Description for logging
     * @returns {Promise} - Resolves when transaction is complete
     */
    async add(txFunction, description = 'Transaction') {
        return new Promise((resolve, reject) => {
            this.queue.push({
                txFunction,
                description,
                resolve,
                reject,
                addedAt: Date.now()
            });

            // Start processing if not already running
            if (!this.processing) {
                this.processQueue();
            }
        });
    }

    /**
     * Process queued transactions one by one
     */
    async processQueue() {
        if (this.processing || this.queue.length === 0) {
            return;
        }

        this.processing = true;

        while (this.queue.length > 0) {
            const item = this.queue.shift();
            const { txFunction, description, resolve, reject, addedAt } = item;

            const queueTime = Date.now() - addedAt;
            console.log(`📤 Processing: ${description} (queued for ${queueTime}ms)`);

            try {
                // Execute transaction function
                const tx = await txFunction();
                console.log(`   Tx sent: ${tx.hash}`);
                
                // Wait for confirmation
                const receipt = await tx.wait();
                console.log(`   ✅ Confirmed: ${tx.hash} (block ${receipt.blockNumber})`);
                
                // Resolve promise
                resolve(receipt);
                
                // Small delay between transactions to avoid rate limiting
                await this.delay(100);
                
            } catch (error) {
                console.error(`   ❌ Failed: ${description}`, error.message);
                reject(error);
            }
        }

        this.processing = false;
    }

    /**
     * Delay helper
     */
    delay(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    /**
     * Get queue status
     */
    getStatus() {
        return {
            queueLength: this.queue.length,
            processing: this.processing
        };
    }

    /**
     * Clear queue (emergency use only)
     */
    clear() {
        const cleared = this.queue.length;
        this.queue = [];
        console.log(`🗑️  Cleared ${cleared} pending transactions`);
        return cleared;
    }
}

// Singleton instance
export const txQueue = new TransactionQueue();

/**
 * Wrapper function to queue any contract call
 * @param {Function} contractCall - Function that returns a transaction
 * @param {string} description - Description for logging
 * @returns {Promise<TransactionReceipt>}
 */
export async function queueTransaction(contractCall, description) {
    return txQueue.add(contractCall, description);
}

