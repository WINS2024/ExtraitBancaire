// Fonction pour obtenir le nombre total de pages d'un PDF
window.getTotalPages = async function (base64Content) {
    try {
        // Convertir le contenu base64 en ArrayBuffer
        const binaryString = window.atob(base64Content);
        const bytes = new Uint8Array(binaryString.length);
        for (let i = 0; i < binaryString.length; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }
        
        // Charger le PDF avec pdf.js
        const loadingTask = pdfjsLib.getDocument({ data: bytes });
        const pdf = await loadingTask.promise;
        
        return pdf.numPages;
    } catch (error) {
        console.error('Erreur lors de la récupération du nombre de pages:', error);
        return 0;
    }
};

// Fonction pour naviguer vers une page spécifique
window.navigateToPage = function (iframeId, pageNumber) {
    const iframe = document.getElementById(iframeId);
    if (!iframe) return;

    try {
        // Accéder au document PDF via l'iframe
        const pdfViewer = iframe.contentWindow.PDFViewerApplication;
        if (pdfViewer) {
            pdfViewer.pdfViewer.currentPageNumber = pageNumber;
        }
    } catch (error) {
        console.error('Erreur lors de la navigation vers la page:', error);
    }
};

// Fonction pour télécharger un fichier
window.downloadFileFromStream = function (fileName, base64Content) {
    const linkSource = `data:application/pdf;base64,${base64Content}`;
    const downloadLink = document.createElement("a");
    downloadLink.href = linkSource;
    downloadLink.download = fileName;
    downloadLink.click();
};

// Fonction pour mettre à jour l'affichage du PDF
window.updatePdfView = function (iframeId, pdfSrc) {
    const iframe = document.getElementById(iframeId);
    if (iframe) {
        iframe.src = pdfSrc;
    }
}; 