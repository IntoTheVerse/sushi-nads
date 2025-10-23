/**
 * Global error handling middleware
 */

export function errorHandler(err, req, res, next) {
    console.error('❌ Error:', err);

    // Default error response
    let statusCode = 500;
    let error = 'Internal server error';
    let message = err.message;

    // Handle specific error types
    if (err.name === 'ValidationError') {
        statusCode = 400;
        error = 'Validation error';
    } else if (err.name === 'UnauthorizedError') {
        statusCode = 401;
        error = 'Unauthorized';
    } else if (err.code === 'CALL_EXCEPTION') {
        statusCode = 400;
        error = 'Blockchain call failed';
        message = 'Transaction reverted: ' + (err.reason || err.message);
    } else if (err.code === 'INSUFFICIENT_FUNDS') {
        statusCode = 400;
        error = 'Insufficient funds';
        message = 'Wallet has insufficient balance for transaction';
    } else if (err.code === 'NONCE_EXPIRED') {
        statusCode = 500;
        error = 'Nonce error';
        message = 'Transaction nonce conflict, please retry';
    }

    // Send error response
    res.status(statusCode).json({
        error,
        message,
        ...(process.env.NODE_ENV === 'development' && { stack: err.stack })
    });
}

/**
 * 404 handler for undefined routes
 */
export function notFoundHandler(req, res) {
    res.status(404).json({
        error: 'Not found',
        message: `Route ${req.method} ${req.path} not found`
    });
}

export default {
    errorHandler,
    notFoundHandler,
};

