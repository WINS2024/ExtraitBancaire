window.pdfThumbnails = {
    async generateThumbnails(base64Pdf) {
        const pdfjsLib = window['pdfjsLib'] || window['pdfjs-dist/build/pdf'];
        if (!pdfjsLib) throw new Error('pdfjsLib non chargé');
        // Décodage base64
        const raw = atob(base64Pdf);
        const uint8Array = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) uint8Array[i] = raw.charCodeAt(i);
        const pdf = await pdfjsLib.getDocument({ data: uint8Array }).promise;
        const thumbnails = [];
        for (let pageNum = 1; pageNum <= pdf.numPages; pageNum++) {
            const page = await pdf.getPage(pageNum);
            const viewport = page.getViewport({ scale: 0.2 });
            const canvas = document.createElement('canvas');
            canvas.width = viewport.width;
            canvas.height = viewport.height;
            await page.render({ canvasContext: canvas.getContext('2d'), viewport }).promise;
            thumbnails.push(canvas.toDataURL());
        }
        return thumbnails;
    }
}; 