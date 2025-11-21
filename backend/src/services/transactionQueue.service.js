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
                
                // Minimal delay between transactions (reduced from 100ms for speed)
                // Note: Nonce management ensures transactions are sent in order
                await this.delay(10); // 10ms instead of 100ms = 10x faster
                
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

/**
 * Send multiple transactions in parallel batches with proper nonce management
 * Much faster than sequential processing (50-100x speedup)
 * @param {Array<{txFunction: Function, description: string}>} transactions - Array of transaction functions
 * @param {Object} signer - Ethers signer instance (must be exported from blockchain.service)
 * @param {number} batchSize - Number of transactions to send in parallel (default: 50)
 * @returns {Promise<Array<{success: boolean, receipt?: TransactionReceipt, error?: Error, description: string, txHash?: string}>>}
 */
export async function sendParallelTransactions(transactions, signer, batchSize = 50) {
    const total = transactions.length;
    
    if (total === 0) {
        return [];
    }
    
    console.log(`🚀 Sending ${total} transactions in parallel batches (batch size: ${batchSize}) with proper nonce management...`);
    
    // Get current nonce from signer (use 'pending' to include in-flight transactions)
    const startNonce = BigInt(await signer.getNonce('pending'));
    console.log(`   📍 Starting nonce: ${startNonce}`);
    
    const allResults = [];
    
    // Process in batches to avoid overwhelming the RPC
    for (let i = 0; i < transactions.length; i += batchSize) {
        const batch = transactions.slice(i, i + batchSize);
        const batchNum = Math.floor(i / batchSize) + 1;
        const totalBatches = Math.ceil(transactions.length / batchSize);
        
        console.log(`📦 Batch ${batchNum}/${totalBatches}: Preparing ${batch.length} transactions with explicit nonces...`);
        
        // Step 1: Populate all transactions in batch
        // The txFunction should return a populated transaction request (from populateTransaction)
        const populatedTxs = await Promise.all(
            batch.map(async ({ txFunction, description }, index) => {
                const globalIndex = i + index;
                try {
                    // Call the transaction function - it should return a populated transaction request
                    const txRequest = await txFunction();
                    
                    // Verify it's a valid transaction request
                    if (!txRequest || !txRequest.to || !txRequest.data) {
                        throw new Error(`Invalid transaction request for ${description}`);
                    }
                    
                    return {
                        txRequest,
                        description,
                        index: globalIndex
                    };
                } catch (error) {
                    console.error(`   ❌ Error populating ${globalIndex + 1}/${total}: ${description}`, error.message);
                    throw error;
                }
            })
        );
        
        // Step 2: Set explicit nonces and sign all transactions in batch
        const signedTxs = await Promise.all(
            populatedTxs.map(async ({ txRequest, description, index }, batchIndex) => {
                try {
                    // Create a proper transaction request with explicit nonce
                    const explicitNonce = startNonce + BigInt(i + batchIndex);
                    
                    // Get current gas price if not provided (use EIP-1559)
                    let maxFeePerGas = txRequest.maxFeePerGas;
                    let maxPriorityFeePerGas = txRequest.maxPriorityFeePerGas;
                    
                    if (!maxFeePerGas && !txRequest.gasPrice) {
                        const feeData = await signer.provider.getFeeData();
                        maxFeePerGas = feeData.maxFeePerGas;
                        maxPriorityFeePerGas = feeData.maxPriorityFeePerGas;
                    }
                    
                    // Get chain ID to ensure proper transaction serialization
                    const network = await signer.provider.getNetwork();
                    const chainId = Number(network.chainId);
                    
                    // Convert gasLimit to BigInt if it's a string/hex
                    let gasLimit = txRequest.gasLimit || txRequest.gas;
                    if (typeof gasLimit === 'string') {
                        gasLimit = BigInt(gasLimit);
                    } else if (typeof gasLimit === 'number') {
                        gasLimit = BigInt(gasLimit);
                    }
                    gasLimit = gasLimit || 300000n;
                    
                    // Create transaction request - explicitly avoid blob transactions
                    // Only include fields needed for EIP-1559 transactions
                    const finalTxRequest = {
                        to: txRequest.to,
                        data: txRequest.data,
                        value: txRequest.value || 0n,
                        gasLimit: gasLimit,
                        nonce: Number(explicitNonce), // Convert BigInt to number for nonce
                        chainId: chainId, // Explicitly set chain ID
                        type: 2, // Explicitly set to EIP-1559 (Base doesn't support blob transactions)
                        // Explicitly set EIP-1559 fee fields to prevent blob transaction creation
                        maxFeePerGas: maxFeePerGas || 0n,
                        maxPriorityFeePerGas: maxPriorityFeePerGas || 0n
                    };
                    
                    // Only use legacy gasPrice if explicitly provided (type 0)
                    if (txRequest.gasPrice && !maxFeePerGas) {
                        finalTxRequest.type = 0;
                        finalTxRequest.gasPrice = txRequest.gasPrice;
                        delete finalTxRequest.maxFeePerGas;
                        delete finalTxRequest.maxPriorityFeePerGas;
                    }
                    
                    // CRITICAL: Remove any blob-related fields that might cause ethers.js to create blob transactions
                    delete finalTxRequest.blobs;
                    delete finalTxRequest.blobVersionedHashes;
                    delete finalTxRequest.maxFeePerBlobGas;
                    
                    // Ensure we're not accidentally including blob transaction type hints
                    // Explicitly construct as EIP-1559 transaction
                    if (finalTxRequest.type !== 0) {
                        finalTxRequest.type = 2; // Force EIP-1559
                    }
                    
                    // Sign the transaction with explicit type 2 (EIP-1559)
                    const signedTx = await signer.signTransaction(finalTxRequest);
                    
                    const nonceNum = Number(explicitNonce);
                    
                    return {
                        signedTx,
                        description,
                        index,
                        nonce: nonceNum
                    };
                } catch (error) {
                    console.error(`   ❌ Error signing ${index + 1}/${total}: ${description}`, error.message);
                    throw error;
                }
            })
        );
        
        // Step 3: Send all signed transactions in parallel
        console.log(`   📤 Sending ${signedTxs.length} signed transactions in parallel...`);
        
        // Send all transactions first (fast, no RPC rate limit issues)
        const txResponses = [];
        for (const { signedTx, description, index, nonce } of signedTxs) {
            try {
                const provider = signer.provider;
                const txResponse = await provider.broadcastTransaction(signedTx);
                console.log(`   ✅ Tx ${index + 1}/${total} sent: ${txResponse.hash.substring(0, 10)}... (nonce: ${nonce}, ${description})`);
                txResponses.push({
                    txResponse,
                    description,
                    index,
                    nonce
                });
            } catch (error) {
                console.error(`   ❌ Error sending ${index + 1}/${total}: ${description}`, error.message);
                // Add to results immediately for send failures
                const errorResult = {
                    success: false,
                    error,
                    description,
                    nonce
                };
                // We'll add this to allResults after initialization
                txResponses.push({ error: errorResult, description, index, nonce });
            }
        }
        
        // Step 4: Wait for confirmations with RPC rate limiting
        // Process confirmations in small batches to avoid rate limits (QuickNode: 25 req/sec)
        const CONFIRMATION_BATCH_SIZE = 5; // Process 5 confirmations at a time
        const CONFIRMATION_DELAY = 250; // 250ms delay between batches (50ms per tx = ~20/sec, under limit)
        
        const batchResults = [];
        
        for (let i = 0; i < txResponses.length; i += CONFIRMATION_BATCH_SIZE) {
            const confirmationBatch = txResponses.slice(i, i + CONFIRMATION_BATCH_SIZE);
            
            // Handle errors from sending phase
            const validBatch = confirmationBatch.filter(item => !item.error);
            const errorItems = confirmationBatch.filter(item => item.error);
            
            // Add error results
            errorItems.forEach(item => batchResults.push(item.error));
            
            if (validBatch.length === 0) {
                // All items in batch had errors, continue to next batch
                if (i + CONFIRMATION_BATCH_SIZE < txResponses.length) {
                    await new Promise(resolve => setTimeout(resolve, CONFIRMATION_DELAY));
                }
                continue;
            }
            
            // Wait for this batch of confirmations (max 5 at a time)
            const batchPromises = validBatch.map(async ({ txResponse, description, index, nonce }) => {
                try {
                    // Add retry logic for rate limit errors
                    let retries = 3;
                    let lastError = null;
                    
                    while (retries > 0) {
                        try {
                            const receipt = await txResponse.wait();
                            console.log(`   ✅ Confirmed ${index + 1}/${total}: ${txResponse.hash.substring(0, 10)}... (block ${receipt.blockNumber})`);
                            return {
                                success: true,
                                receipt,
                                description,
                                txHash: txResponse.hash,
                                nonce
                            };
                        } catch (error) {
                            lastError = error;
                            // Check if it's a rate limit error
                            const isRateLimit = error.message && (
                                error.message.includes('rate limit') || 
                                error.message.includes('request limit') || 
                                error.code === -32007 ||
                                error.error?.code === -32007
                            );
                            
                            if (isRateLimit) {
                                retries--;
                                if (retries > 0) {
                                    const delay = (4 - retries) * 1000; // Exponential backoff: 1s, 2s, 3s
                                    console.log(`   ⏳ Rate limit hit, retrying in ${delay}ms... (${retries} retries left)`);
                                    await new Promise(resolve => setTimeout(resolve, delay));
                                    continue;
                                }
                            }
                            // Not a rate limit error or retries exhausted, throw
                            throw error;
                        }
                    }
                    
                    // All retries exhausted
                    console.error(`   ❌ Confirmation failed after retries ${index + 1}/${total}: ${description}`, lastError?.message);
                    return {
                        success: false,
                        error: lastError,
                        description,
                        txHash: txResponse.hash,
                        nonce
                    };
                } catch (error) {
                    console.error(`   ❌ Confirmation failed ${index + 1}/${total}: ${description}`, error.message);
                    return {
                        success: false,
                        error,
                        description,
                        txHash: txResponse.hash,
                        nonce
                    };
                }
            });
            
            // Wait for this batch (max 5 confirmations at once)
            const settledResults = await Promise.allSettled(batchPromises);
            
            // Convert settled results
            const formattedBatch = settledResults.map((result) => {
                if (result.status === 'fulfilled') {
                    return result.value;
                } else {
                    return {
                        success: false,
                        error: result.reason,
                        description: 'Unknown'
                    };
                }
            });
            
            batchResults.push(...formattedBatch);
            
            // Delay between confirmation batches to respect RPC rate limits
            if (i + CONFIRMATION_BATCH_SIZE < txResponses.length) {
                await new Promise(resolve => setTimeout(resolve, CONFIRMATION_DELAY));
            }
        }
        
        // Add batch results to allResults
        allResults.push(...batchResults);
        
        // Small delay between batches to avoid overwhelming the RPC
        if (i + batchSize < transactions.length) {
            await new Promise(resolve => setTimeout(resolve, 50)); // 50ms between batches
        }
    }
    
    const successCount = allResults.filter(r => r.success).length;
    const failCount = allResults.filter(r => !r.success).length;
    
    console.log(`✅ Parallel processing complete: ${successCount} succeeded, ${failCount} failed out of ${total}`);
    
    return allResults;
}

