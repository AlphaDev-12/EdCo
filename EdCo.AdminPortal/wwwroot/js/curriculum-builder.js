/**
 * EdCo Admin Portal - Curriculum Builder Module
 * Handles drag-and-drop reordering of chapters and units, notes editing, video uploads, and quiz creation.
 */

(function () {
    // 1. Setup SortableJS for Chapters and Units
    document.addEventListener('DOMContentLoaded', function () {
        const chapterList = document.getElementById('chapterList');
        if (chapterList && typeof Sortable !== 'undefined') {
            new Sortable(chapterList, {
                animation: 250,
                handle: '.card-header',
                ghostClass: 'sortable-ghost',
                chosenClass: 'sortable-chosen',
                onEnd: function () {
                    syncOrder();
                }
            });
        }

        document.querySelectorAll('.unit-list').forEach(function (el) {
            if (typeof Sortable !== 'undefined') {
                new Sortable(el, {
                    group: 'units',
                    animation: 250,
                    handle: '.drag-handle',
                    ghostClass: 'sortable-ghost',
                    chosenClass: 'sortable-chosen',
                    onEnd: function () {
                        syncOrder();
                    }
                });
            }
        });
    });

    // 2. Order Synchronization
    function syncOrder() {
        const indicator = document.getElementById('saveIndicator');
        if (indicator) indicator.classList.remove('d-none');

        const chapterCards = document.querySelectorAll('.chapter-card');
        const chapterOrder = [];
        const unitOrder = [];

        chapterCards.forEach(function (card) {
            const chapterId = parseInt(card.dataset.chapterId, 10);
            chapterOrder.push(chapterId);

            const unitItems = card.querySelectorAll('.unit-item');
            const unitIds = [];
            unitItems.forEach(function (unit) {
                unitIds.push(parseInt(unit.dataset.unitId, 10));
            });
            unitOrder.push({ chapterId: chapterId, unitIds: unitIds });
        });

        fetch('/Curriculum/UpdateOrder', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ chapterOrder: chapterOrder, unitOrder: unitOrder })
        })
        .then(r => r.json())
        .then(data => {
            if (indicator) {
                indicator.classList.add('d-none');
                if (data.success) {
                    indicator.textContent = '✓ Saved';
                    indicator.classList.remove('d-none', 'bg-dark');
                    indicator.classList.add('bg-success');
                    setTimeout(() => {
                        indicator.classList.add('d-none');
                        indicator.classList.remove('bg-success');
                        indicator.classList.add('bg-dark');
                        indicator.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i> Saving...';
                    }, 1500);
                }
            }
        });
    }

    // 3. Modal Helpers & Logic
    function openAddUnitModal(chapterId) {
        document.getElementById('addUnitChapterId').value = chapterId;
        new bootstrap.Modal(document.getElementById('addUnitModal')).show();
    }

    function openNotesModal(unitId) {
        document.getElementById('notesUnitId').value = unitId;
        document.getElementById('notesMarkdown').value = '';
        document.getElementById('notesFile').value = '';
        document.getElementById('attachedDocumentInfo').classList.add('d-none');

        fetch('/Curriculum/GetNotes?unitId=' + unitId)
            .then(r => r.json())
            .then(data => {
                document.getElementById('notesMarkdown').value = data.markdown || '';
                if (data.documentUrl) {
                    document.getElementById('attachedDocumentInfo').classList.remove('d-none');
                    document.getElementById('attachedFileName').textContent = data.documentFileName;
                    document.getElementById('attachedFileLink').href = data.documentUrl;
                }
            });

        new bootstrap.Modal(document.getElementById('notesModal')).show();
    }

    function saveNotes() {
        const unitId = parseInt(document.getElementById('notesUnitId').value, 10);
        const markdown = document.getElementById('notesMarkdown').value;
        const flashcardCount = parseInt(document.getElementById('flashcardCount').value, 10) || 0;

        fetch('/Curriculum/SaveNotes', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ unitId: unitId, markdown: markdown, flashcardCount: flashcardCount })
        })
        .then(r => r.json())
        .then(data => {
            if (data.success) {
                bootstrap.Modal.getInstance(document.getElementById('notesModal')).hide();
                location.reload();
            }
        });
    }

    function uploadDocument() {
        const unitId = document.getElementById('notesUnitId').value;
        const fileInput = document.getElementById('notesFile');
        if (fileInput.files.length === 0) {
            alert('Please select a file to upload first.');
            return;
        }

        const formData = new FormData();
        formData.append('unitId', unitId);
        formData.append('file', fileInput.files[0]);
        formData.append('flashcardCount', parseInt(document.getElementById('flashcardCount').value, 10) || 0);

        fetch('/Curriculum/UploadNotesDocument', {
            method: 'POST',
            body: formData
        })
        .then(r => r.json())
        .then(data => {
            if (data.success) {
                document.getElementById('attachedDocumentInfo').classList.remove('d-none');
                document.getElementById('attachedFileName').textContent = data.fileName;
                document.getElementById('attachedFileLink').href = data.fileUrl;
                fileInput.value = '';
                alert('Document uploaded and attached successfully!');
            } else {
                alert(data.message || 'Upload failed.');
            }
        });
    }

    function removeDocument() {
        if (!confirm('Are you sure you want to remove this attached document?')) return;
        const unitId = parseInt(document.getElementById('notesUnitId').value, 10);

        fetch('/Curriculum/RemoveNotesDocument', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ unitId: unitId })
        })
        .then(r => r.json())
        .then(data => {
            if (data.success) {
                document.getElementById('attachedDocumentInfo').classList.add('d-none');
                document.getElementById('attachedFileName').textContent = '';
                document.getElementById('attachedFileLink').href = '#';
                alert('Document removed successfully.');
            }
        });
    }

    function openVideoModal(unitId, existingId, duration) {
        document.getElementById('videoUnitId').value = unitId;
        document.getElementById('videoFile').value = '';

        const existsInfo = document.getElementById('videoExistsInfo');
        if (existingId && existingId.length > 0) {
            existsInfo.classList.remove('d-none');
        } else {
            existsInfo.classList.add('d-none');
        }

        document.getElementById('videoUploadProgress').classList.add('d-none');
        document.getElementById('btnUploadVideo').disabled = false;

        new bootstrap.Modal(document.getElementById('videoModal')).show();
    }

    function resetUploadUI() {
        document.getElementById('videoUploadProgress').classList.add('d-none');
        document.getElementById('btnUploadVideo').disabled = false;
        document.getElementById('videoProgressBar').classList.remove('bg-info');
    }

    function uploadVideo() {
        const unitId = document.getElementById('videoUnitId').value;
        const fileInput = document.getElementById('videoFile');

        if (fileInput.files.length === 0) {
            alert('Please select a video file first.');
            return;
        }

        const formData = new FormData();
        formData.append('unitId', unitId);
        formData.append('videoFile', fileInput.files[0]);

        document.getElementById('videoUploadProgress').classList.remove('d-none');
        document.getElementById('btnUploadVideo').disabled = true;
        document.getElementById('videoProgressBar').style.width = '0%';
        document.getElementById('videoUploadPercent').textContent = '0%';
        document.getElementById('videoUploadStatusText').textContent = 'Uploading to Server...';

        const xhr = new XMLHttpRequest();
        xhr.open('POST', '/Curriculum/UploadVideo', true);

        xhr.upload.onprogress = function (e) {
            if (e.lengthComputable) {
                const percentComplete = Math.round((e.loaded / e.total) * 100);
                document.getElementById('videoProgressBar').style.width = percentComplete + '%';
                document.getElementById('videoUploadPercent').textContent = percentComplete + '%';

                if (percentComplete === 100) {
                    document.getElementById('videoUploadStatusText').textContent = 'Processing and sending to Bunny.net...';
                    document.getElementById('videoProgressBar').classList.add('bg-info');
                }
            }
        };

        xhr.onload = function () {
            if (xhr.status === 200) {
                try {
                    const data = JSON.parse(xhr.responseText);
                    if (data.success) {
                        document.getElementById('videoUploadStatusText').textContent = 'Upload Complete!';
                        document.getElementById('videoProgressBar').classList.remove('progress-bar-animated');
                        setTimeout(() => {
                            bootstrap.Modal.getInstance(document.getElementById('videoModal')).hide();
                            location.reload();
                        }, 500);
                    } else {
                        alert('Upload failed: ' + (data.message || 'Unknown error'));
                        resetUploadUI();
                    }
                } catch (e) {
                    alert('Invalid response from server.');
                    resetUploadUI();
                }
            } else {
                alert('Upload failed with status: ' + xhr.status);
                resetUploadUI();
            }
        };

        xhr.onerror = function () {
            alert('An error occurred during upload.');
            resetUploadUI();
        };

        xhr.send(formData);
    }

    function openQuizModal(unitId, existingQuizId) {
        document.getElementById('quizUnitId').value = unitId;
        document.getElementById('quizExistingId').value = existingQuizId || 0;
        document.getElementById('quizTitle').value = '';

        const actionBtn = document.getElementById('quizActionBtn');
        if (existingQuizId && existingQuizId > 0) {
            document.getElementById('quizExistsInfo').classList.remove('d-none');
            document.getElementById('editQuizLink').href = '/QuizStudio/Edit/' + existingQuizId;
            actionBtn.textContent = 'Update Title';
            actionBtn.onclick = function () { updateQuizTitle(); };
        } else {
            document.getElementById('quizExistsInfo').classList.add('d-none');
            actionBtn.innerHTML = '<i class="fa-solid fa-plus me-1"></i> Create Quiz';
            actionBtn.onclick = function () { createQuiz(); };
        }

        new bootstrap.Modal(document.getElementById('quizModal')).show();
    }

    function createQuiz() {
        const unitId = parseInt(document.getElementById('quizUnitId').value, 10);
        const title = document.getElementById('quizTitle').value;

        fetch('/QuizStudio/CreateForUnit', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ unitId: unitId, title: title })
        })
        .then(r => r.json())
        .then(data => {
            if (data.success) {
                bootstrap.Modal.getInstance(document.getElementById('quizModal')).hide();
                location.reload();
            } else {
                alert(data.message || 'Failed to create quiz.');
            }
        });
    }

    function updateQuizTitle() {
        const quizId = parseInt(document.getElementById('quizExistingId').value, 10);
        const title = document.getElementById('quizTitle').value;

        fetch('/QuizStudio/UpdateTitle', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ quizId: quizId, title: title })
        })
        .then(r => r.json())
        .then(data => {
            if (data.success) {
                bootstrap.Modal.getInstance(document.getElementById('quizModal')).hide();
            }
        });
    }

    // Expose window globals for inline onclick handlers
    window.openAddUnitModal = openAddUnitModal;
    window.openNotesModal = openNotesModal;
    window.saveNotes = saveNotes;
    window.uploadDocument = uploadDocument;
    window.removeDocument = removeDocument;
    window.openVideoModal = openVideoModal;
    window.uploadVideo = uploadVideo;
    window.openQuizModal = openQuizModal;
    window.createQuiz = createQuiz;
    window.updateQuizTitle = updateQuizTitle;
})();
