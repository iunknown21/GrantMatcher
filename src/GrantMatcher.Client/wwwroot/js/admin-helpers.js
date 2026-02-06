// Admin helper functions for file downloads and other utilities

/**
 * Downloads a file from base64 data
 * @param {string} filename - The name of the file to download
 * @param {string} base64Data - The base64 encoded file data
 */
window.downloadFile = function (filename, base64Data) {
    try {
        // Convert base64 to blob
        const byteCharacters = atob(base64Data);
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray], { type: 'application/octet-stream' });

        // Create download link
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;

        // Trigger download
        document.body.appendChild(link);
        link.click();

        // Cleanup
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
    } catch (error) {
        console.error('Error downloading file:', error);
    }
};

/**
 * Shows a confirmation dialog
 * @param {string} message - The message to display
 * @returns {boolean} True if confirmed, false otherwise
 */
window.confirmAction = function (message) {
    return confirm(message);
};

/**
 * Copies text to clipboard
 * @param {string} text - The text to copy
 * @returns {Promise<boolean>} True if successful, false otherwise
 */
window.copyToClipboard = async function (text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch (error) {
        console.error('Error copying to clipboard:', error);
        return false;
    }
};

/**
 * Reads a file as text
 * @param {File} file - The file to read
 * @returns {Promise<string>} The file contents as text
 */
window.readFileAsText = function (file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = (e) => resolve(e.target.result);
        reader.onerror = (e) => reject(e);
        reader.readAsText(file);
    });
};

/**
 * Shows a toast notification
 * @param {string} message - The message to display
 * @param {string} type - The type of toast (success, error, warning, info)
 * @param {number} duration - How long to show the toast in milliseconds
 */
window.showToast = function (message, type = 'info', duration = 3000) {
    // Create toast element
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.innerHTML = `
        <div class="p-4">
            <div class="flex items-start">
                <div class="flex-1">
                    <p class="text-sm font-medium text-gray-900">${message}</p>
                </div>
                <button onclick="this.parentElement.parentElement.parentElement.remove()" class="ml-4 text-gray-400 hover:text-gray-500">
                    <svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                    </svg>
                </button>
            </div>
        </div>
    `;

    // Add to DOM
    document.body.appendChild(toast);

    // Auto-remove after duration
    setTimeout(() => {
        toast.remove();
    }, duration);
};

/**
 * Formats a number as currency
 * @param {number} amount - The amount to format
 * @param {string} currency - The currency code (default: USD)
 * @returns {string} The formatted currency string
 */
window.formatCurrency = function (amount, currency = 'USD') {
    return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: currency,
        minimumFractionDigits: 0,
        maximumFractionDigits: 0
    }).format(amount);
};

/**
 * Formats a date
 * @param {string|Date} date - The date to format
 * @param {string} format - The format style (short, medium, long, full)
 * @returns {string} The formatted date string
 */
window.formatDate = function (date, format = 'medium') {
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    return new Intl.DateTimeFormat('en-US', {
        dateStyle: format
    }).format(dateObj);
};

/**
 * Validates a URL
 * @param {string} url - The URL to validate
 * @returns {boolean} True if valid, false otherwise
 */
window.isValidUrl = function (url) {
    try {
        new URL(url);
        return true;
    } catch {
        return false;
    }
};

/**
 * Debounce function for search inputs
 * @param {Function} func - The function to debounce
 * @param {number} wait - The wait time in milliseconds
 * @returns {Function} The debounced function
 */
window.debounce = function (func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
};

console.log('Admin helper functions loaded');
