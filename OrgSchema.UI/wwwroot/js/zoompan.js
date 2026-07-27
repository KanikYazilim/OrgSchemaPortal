window.orgChartFunctions = {
    initZoomPan: function (wrapperId, containerId) {
        const wrapper = document.getElementById(wrapperId);
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
            // Kartlara veya ikonlara tıklandığında sürüklemeyi engelle
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

        // Mouse Wheel (Zoom)
        wrapper.addEventListener('wheel', (e) => {
            e.preventDefault(); // Sayfanın scroll olmasını engelle
            
            const delta = e.deltaY > 0 ? -0.1 : 0.1; // Scroll yönü
            const newScale = Math.min(Math.max(0.2, scale + delta), 4.0); // 0.2x ile 4.0x arası sınır
            
            if (scale === newScale) return;

            // Farenin wrapper içindeki pozisyonunu bul
            const rect = wrapper.getBoundingClientRect();
            const mouseX = e.clientX - rect.left;
            const mouseY = e.clientY - rect.top;

            // Zoom işleminin merkez noktasını fare imleci yapmak için translate hesaplaması
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
