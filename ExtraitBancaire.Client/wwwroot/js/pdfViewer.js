// Fonction pour mettre à jour la vue du PDF
window.updatePdfView = function (frameId, pdfUrl, page, zoom) {
    const frame = document.getElementById(frameId);
    if (frame) {
        // Construire l'URL avec les paramètres de page et de zoom
        const url = `${pdfUrl}#page=${page}&zoom=${zoom * 100}`;
        frame.src = url;
    }
};

// Fonction pour obtenir le nombre total de pages
window.getTotalPages = function (frameId) {
    const frame = document.getElementById(frameId);
    if (frame) {
        frame.onload = function () {
            try {
                // Tenter d'accéder au nombre de pages via l'API PDF.js
                const pdfViewer = frame.contentWindow.PDFViewerApplication;
                if (pdfViewer) {
                    const totalPages = pdfViewer.pagesCount;
                    DotNet.invokeMethodAsync('ExtraitBancaire.Client', 'UpdateTotalPages', totalPages);
                }
            } catch (error) {
                console.error('Erreur lors de la récupération du nombre de pages:', error);
            }
        };
    }
}; 