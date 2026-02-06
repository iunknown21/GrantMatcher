// Lazy loading utilities using Intersection Observer API

const observers = new Map();

export function observeElement(element, dotNetHelper, callbackId, threshold = 0.1) {
    if (!element || observers.has(callbackId)) {
        return;
    }

    const observer = new IntersectionObserver(
        (entries) => {
            entries.forEach(async (entry) => {
                if (entry.isIntersecting) {
                    try {
                        await dotNetHelper.invokeMethodAsync('OnVisible');
                    } catch (error) {
                        console.error('Error invoking lazy load callback:', error);
                    }
                    // Unobserve after first intersection
                    observer.unobserve(entry.target);
                }
            });
        },
        {
            threshold: threshold,
            rootMargin: '50px' // Start loading 50px before element is visible
        }
    );

    observer.observe(element);
    observers.set(callbackId, { observer, element });
}

export function unobserveElement(callbackId) {
    const data = observers.get(callbackId);
    if (data) {
        data.observer.unobserve(data.element);
        data.observer.disconnect();
        observers.delete(callbackId);
    }
}

// Lazy load images
export function initLazyImages() {
    const images = document.querySelectorAll('img[data-src]');

    if (!('IntersectionObserver' in window)) {
        // Fallback for browsers that don't support IntersectionObserver
        images.forEach(img => {
            img.src = img.dataset.src;
        });
        return;
    }

    const imageObserver = new IntersectionObserver((entries, observer) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const img = entry.target;
                img.src = img.dataset.src;
                img.removeAttribute('data-src');
                observer.unobserve(img);
            }
        });
    }, {
        rootMargin: '50px'
    });

    images.forEach(img => imageObserver.observe(img));
}

// Auto-initialize on page load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initLazyImages);
} else {
    initLazyImages();
}

// Debounce utility
export function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// Virtual scrolling helper
export function setupVirtualScroll(container, itemHeight, totalItems, renderCallback) {
    if (!container) return null;

    let scrollTop = 0;
    const viewportHeight = container.clientHeight;
    const totalHeight = itemHeight * totalItems;

    const updateVisibleItems = debounce(() => {
        const firstVisibleIndex = Math.floor(scrollTop / itemHeight);
        const visibleItemCount = Math.ceil(viewportHeight / itemHeight);
        const lastVisibleIndex = Math.min(firstVisibleIndex + visibleItemCount, totalItems - 1);

        renderCallback.invokeMethodAsync('OnScrollUpdate', firstVisibleIndex, lastVisibleIndex);
    }, 16); // ~60fps

    const onScroll = () => {
        scrollTop = container.scrollTop;
        updateVisibleItems();
    };

    container.addEventListener('scroll', onScroll);

    // Initial render
    updateVisibleItems();

    return {
        dispose: () => {
            container.removeEventListener('scroll', onScroll);
        }
    };
}
