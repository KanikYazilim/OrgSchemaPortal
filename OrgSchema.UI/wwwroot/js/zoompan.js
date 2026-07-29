window.orgChartFunctions = {
    initZoomPan: function (wrapperId, containerId) {
        console.log("initZoomPan called for", wrapperId); const wrapper = document.getElementById(wrapperId); if (wrapper.dataset.zoomInitialized) return; wrapper.dataset.zoomInitialized = "true";
        const container = document.getElementById(containerId);
        
        if (!wrapper || !container) {
            console.error("Zoom/Pan elements not found");
            return;
        }

        let isDragging = false;
        let startX, startY;
        let translateX = 0, translateY = 0;
        let scale = 1.0;

        // Reset transform
        container.style.transformOrigin = '0 0';
        updateTransform();

        wrapper.style.cursor = 'grab';

        // Mouse Down (Drag Start)
        wrapper.addEventListener('mousedown', (e) => {
            // Kartlara veya ikonlara tÄ±klandÄ±ÄŸÄ±nda sÃ¼rÃ¼klemeyi engelle
            if (e.target.closest('.card') || e.target.closest('button')) return;
            
            isDragging = true;
            wrapper.style.cursor = 'grabbing';
            startX = e.clientX - translateX;
            startY = e.clientY - translateY;
        });

        // Mouse Move (Dragging)
        window.addEventListener('mousemove', (e) => {
            if (!isDragging) return;
            e.preventDefault();
            translateX = e.clientX - startX;
            translateY = e.clientY - startY;
            updateTransform();
        });

        // Mouse Up (Drag End)
        window.addEventListener('mouseup', () => {
            if (isDragging) {
                isDragging = false;
                wrapper.style.cursor = 'grab';
            }
        });

        // Mouse Wheel (Google Maps Style Smooth Zoom)
        wrapper.addEventListener('wheel', (e) => {
            e.preventDefault(); 
            
            // Hassas zoom iÃ§in deltaY
            const zoomSensitivity = 0.0015;
            const delta = -e.deltaY * zoomSensitivity;
            
            // Eksponansiyel bÃ¼yÃ¼me ile pÃ¼rÃ¼zsÃ¼z zoom (0.1x ile 5.0x arasÄ± sÄ±nÄ±r)
            const newScale = Math.min(Math.max(0.1, scale * Math.exp(delta)), 5.0);
            
            if (scale === newScale) return;

            // Farenin wrapper iÃ§indeki kordinatlarÄ±
            const rect = wrapper.getBoundingClientRect();
            const mouseX = e.clientX - rect.left;
            const mouseY = e.clientY - rect.top;

            // Zoom iÅŸleminin merkez noktasÄ±nÄ± fare imleci yapmak iÃ§in matematik
            translateX = mouseX - (mouseX - translateX) * (newScale / scale);
            translateY = mouseY - (mouseY - translateY) * (newScale / scale);

            scale = newScale;
            updateTransform();
        }, { passive: false });

        function updateTransform() {
            container.style.transform = `translate(${translateX}px, ${translateY}px) scale(${scale})`;
        }
    }
};


