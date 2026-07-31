window.orgChartFunctions = {
    initZoomPan: function (wrapperId, containerId) {
        console.log('initZoomPan called for', wrapperId); 
        const wrapper = document.getElementById(wrapperId); 
        if (wrapper.dataset.zoomInitialized) return; 
        wrapper.dataset.zoomInitialized = 'true';
        const container = document.getElementById(containerId);
        
        if (!wrapper || !container) {
            console.error('Zoom/Pan elements not found');
            return;
        }

        let isDragging = false;
        let startX, startY;
        let translateX = 0, translateY = 0;
        let scale = 1.0;

        container.style.transformOrigin = '0 0';

        // Auto-center and fit
        setTimeout(() => {
            const wrapperRect = wrapper.getBoundingClientRect();
            // Reset transform to get true width
            container.style.transform = 'none';
            const containerRect = container.getBoundingClientRect();
            
            // Calculate scale to fit width
            if (containerRect.width > wrapperRect.width) {
                scale = (wrapperRect.width - 60) / containerRect.width;
                if (scale < 0.2) scale = 0.2;
            } else {
                scale = 1.0;
            }
            
            translateX = (wrapperRect.width - (containerRect.width * scale)) / 2;
            translateY = 40; // top padding
            
            updateTransform();
        }, 100);

        wrapper.style.cursor = 'grab';

        wrapper.addEventListener('mousedown', (e) => {
            if (e.target.closest('.card') || e.target.closest('button')) return;
            isDragging = true;
            wrapper.style.cursor = 'grabbing';
            startX = e.clientX - translateX;
            startY = e.clientY - translateY;
        });

        window.addEventListener('mousemove', (e) => {
            if (!isDragging) return;
            e.preventDefault();
            translateX = e.clientX - startX;
            translateY = e.clientY - startY;
            updateTransform();
        });

        window.addEventListener('mouseup', () => {
            if (isDragging) {
                isDragging = false;
                wrapper.style.cursor = 'grab';
            }
        });

        wrapper.addEventListener('wheel', (e) => {
            e.preventDefault(); 
            const zoomSensitivity = 0.0015;
            const delta = -e.deltaY * zoomSensitivity;
            const newScale = Math.min(Math.max(0.1, scale * Math.exp(delta)), 5.0);
            
            if (scale === newScale) return;

            const rect = wrapper.getBoundingClientRect();
            const mouseX = e.clientX - rect.left;
            const mouseY = e.clientY - rect.top;

            translateX = mouseX - (mouseX - translateX) * (newScale / scale);
            translateY = mouseY - (mouseY - translateY) * (newScale / scale);

            scale = newScale;
            updateTransform();
        }, { passive: false });

        function updateTransform() {
            container.style.transform = "translate(" + translateX + "px, " + translateY + "px) scale(" + scale + ")";
        }
        
        window.orgChartFunctions.resetZoomPan = function() {
            const wrapperRect = wrapper.getBoundingClientRect();
            container.style.transform = 'none';
            const containerRect = container.getBoundingClientRect();
            
            if (containerRect.width > wrapperRect.width) {
                scale = (wrapperRect.width - 60) / containerRect.width;
                if (scale < 0.2) scale = 0.2;
            } else {
                scale = 1.0;
            }
            translateX = (wrapperRect.width - (containerRect.width * scale)) / 2;
            translateY = 40;
            updateTransform();
        };
    }
};